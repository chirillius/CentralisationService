using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CentralServer.Models;
using CentralisationService.Entities.Models.Access;
using CentralisationService.Entities.Models.Catalog;
using Microsoft.Extensions.Options;

namespace CentralServer.Services;

public sealed class AccessStoreService
{
    private const string CompanyAdminRoleKey = "company-admin";
    private const string CompanyOperatorRoleKey = "company-operator";
    private const string AccessStatusActive = "active";
    private const string AccessStatusSuspended = "suspended";
    private const string AccessStatusDisabled = "disabled";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly string[] CompanyAdminPermissions =
    [
        "sites.read",
        "cameras.read",
        "zones.manage",
        "detection-profiles.manage",
        "archive.read",
        "users.manage",
    ];

    private static readonly string[] CompanyOperatorPermissions =
    [
        "sites.read",
        "cameras.read",
        "archive.read",
    ];

    private readonly AccessOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AccessStoreService(IOptions<AccessOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public Task<List<CompanyAccessRecord>> GetCompaniesAsync(CancellationToken cancellationToken) =>
        ReadAsync<CompanyAccessRecord>("companies.json", cancellationToken);

    public async Task<CompanyAccessRecord?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        (await GetCompaniesAsync(cancellationToken)).FirstOrDefault(item => item.Id == companyId);

    public async Task<CompanyAccessRecord?> GetCompanyByKeyAsync(string companyKey, CancellationToken cancellationToken) =>
        (await GetCompaniesAsync(cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Key, companyKey, StringComparison.OrdinalIgnoreCase));

    public Task<List<CompanySiteBinding>> GetCompanySitesAsync(CancellationToken cancellationToken) =>
        ReadAsync<CompanySiteBinding>("company-sites.json", cancellationToken);

    public async Task<List<CompanySiteBinding>> GetCompanySitesAsync(Guid companyId, CancellationToken cancellationToken) =>
        (await GetCompanySitesAsync(cancellationToken))
            .Where(item => item.CompanyId == companyId && item.DisabledAtUtc is null)
            .ToList();

    public async Task<CompanySiteBinding> UpsertCompanySiteAsync(CompanySiteBinding site, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var sites = await GetCompanySitesAsync(cancellationToken);
            var index = sites.FindIndex(item =>
                item.Id == site.Id
                || string.Equals(item.CompanyKey, site.CompanyKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.SiteKey, site.SiteKey, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                sites[index] = site;
            }
            else
            {
                sites.Add(site);
            }
            await WriteAsync("company-sites.json", sites, cancellationToken);
            return site;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<CompanyAccessRecord> UpsertCompanyAsync(CompanyAccessRecord company, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var companies = await GetCompaniesAsync(cancellationToken);
            var index = companies.FindIndex(item => item.Id == company.Id || string.Equals(item.Key, company.Key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                companies[index] = company;
            }
            else
            {
                companies.Add(company);
            }
            await WriteAsync("companies.json", companies, cancellationToken);
            return company;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<(CompanyInvitation Invitation, string Token)> CreateInvitationAsync(
        Guid companyId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new CompanyInvitation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name.Trim(),
            TokenHash = HashToken(token),
            RoleKey = NormalizeRoleKey(request.RoleKey),
            Permissions = GetDefaultPermissions(NormalizeRoleKey(request.RoleKey)),
            ExpiresAtUtc = request.ExpiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var invitations = await ReadAsync<CompanyInvitation>("invitations.json", cancellationToken);
            invitations.Add(invitation);
            await WriteAsync("invitations.json", invitations, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        return (invitation, token);
    }

    public async Task<(string SessionToken, AuthenticatedCompanyContext Context)> ActivateInvitationAsync(
        ActivateInvitationRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        ValidateCredentials(request.Login, request.Password);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var invitations = await ReadAsync<CompanyInvitation>("invitations.json", cancellationToken);
            var invitationHash = HashToken(request.InvitationToken.Trim());
            var invitationIndex = invitations.FindIndex(item => CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(item.TokenHash),
                Convert.FromHexString(invitationHash)));
            if (invitationIndex < 0)
            {
                throw new AccessDeniedException("invalid_invitation", "Токен приглашения неверный.");
            }

            var invitation = invitations[invitationIndex];
            if (invitation.UsedAtUtc.HasValue || invitation.RevokedAtUtc.HasValue)
            {
                throw new AccessDeniedException("invitation_unavailable", "Приглашение уже использовано или закрыто.");
            }
            if (invitation.ExpiresAtUtc.HasValue && invitation.ExpiresAtUtc <= now)
            {
                throw new AccessDeniedException("invitation_expired", "Срок действия приглашения истёк.");
            }

            var company = await RequireActiveCompanyAsync(invitation.CompanyId, now, cancellationToken);
            var accounts = await ReadAsync<AccountRecord>("accounts.json", cancellationToken);
            if (accounts.Any(item => string.Equals(item.Login, request.Login.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new AccessDeniedException("login_exists", "Пользователь с таким логином уже существует.");
            }

            var (passwordHash, passwordSalt) = HashPassword(request.Password);
            var account = new AccountRecord
            {
                Id = Guid.NewGuid(),
                Login = request.Login.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Login.Trim() : request.DisplayName.Trim(),
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                CreatedAtUtc = now,
                LastLoginAtUtc = now,
                LastLoginIp = NormalizeIpAddress(ipAddress),
            };
            accounts.Add(account);

            var grants = await ReadAsync<CompanyAccessGrant>("grants.json", cancellationToken);
            var grant = new CompanyAccessGrant
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                AccountId = account.Id,
                RoleKey = invitation.RoleKey,
                Status = AccessStatusActive,
                Permissions = invitation.Permissions,
                ExpiresAtUtc = invitation.ExpiresAtUtc,
                CreatedAtUtc = now,
            };
            grants.Add(grant);

            invitations[invitationIndex] = new CompanyInvitation
            {
                Id = invitation.Id,
                CompanyId = invitation.CompanyId,
                Name = invitation.Name,
                TokenHash = invitation.TokenHash,
                RoleKey = invitation.RoleKey,
                Permissions = invitation.Permissions,
                ExpiresAtUtc = invitation.ExpiresAtUtc,
                UsedAtUtc = now,
                UsedByAccountId = account.Id,
                RevokedAtUtc = invitation.RevokedAtUtc,
                CreatedAtUtc = invitation.CreatedAtUtc,
            };

            await WriteAsync("accounts.json", accounts, cancellationToken);
            await WriteAsync("grants.json", grants, cancellationToken);
            await WriteAsync("invitations.json", invitations, cancellationToken);
            return await CreateSessionUnderLockAsync(company, account, grant, now, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<(string SessionToken, AuthenticatedCompanyContext Context)> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var accounts = await ReadAsync<AccountRecord>("accounts.json", cancellationToken);
            var accountIndex = accounts.FindIndex(item => string.Equals(item.Login, request.Login.Trim(), StringComparison.OrdinalIgnoreCase));
            var account = accountIndex >= 0 ? accounts[accountIndex] : null;
            if (account is null || !account.IsEnabled || !VerifyPassword(request.Password, account.PasswordHash, account.PasswordSalt))
            {
                throw new AccessDeniedException("invalid_credentials", "Неверный логин или пароль.");
            }

            var grant = (await ReadAsync<CompanyAccessGrant>("grants.json", cancellationToken))
                .FirstOrDefault(item => item.AccountId == account.Id && item.IsEnabled && IsAccessStatusActive(item.Status));
            if (grant is null || grant.ExpiresAtUtc.HasValue && grant.ExpiresAtUtc <= now)
            {
                throw new AccessDeniedException("account_access_expired", "Доступ пользователя отключён или срок доступа истёк.");
            }

            var company = await RequireActiveCompanyAsync(grant.CompanyId, now, cancellationToken);
            accounts[accountIndex] = CopyAccount(account, lastLoginAtUtc: now, lastLoginIp: NormalizeIpAddress(ipAddress));
            await WriteAsync("accounts.json", accounts, cancellationToken);
            return await CreateSessionUnderLockAsync(company, account, grant, now, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<(string SessionToken, PlatformAdminSession Session)> LoginPlatformAdminAsync(
        PlatformAdminLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PlatformAdminLogin) || string.IsNullOrWhiteSpace(_options.PlatformAdminPassword))
        {
            throw new AccessDeniedException("platform_admin_not_configured", "Учётные данные администратора платформы не настроены.");
        }

        var loginMatches = string.Equals(request.Login.Trim(), _options.PlatformAdminLogin, StringComparison.OrdinalIgnoreCase);
        var passwordMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Password),
            Encoding.UTF8.GetBytes(_options.PlatformAdminPassword));
        if (!loginMatches || !passwordMatches)
        {
            throw new AccessDeniedException("invalid_platform_credentials", "Неверный логин или пароль администратора платформы.");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var session = new PlatformAdminSession
            {
                Id = Guid.NewGuid(),
                Login = _options.PlatformAdminLogin,
                TokenHash = HashToken(token),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddHours(Math.Max(1, _options.PlatformSessionLifetimeHours)),
                LastUsedAtUtc = now,
            };
            var sessions = await ReadAsync<PlatformAdminSession>("platform-sessions.json", cancellationToken);
            sessions.Add(session);
            await WriteAsync("platform-sessions.json", sessions, cancellationToken);
            return (token, session);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<PlatformAdminSession?> ResolvePlatformAdminSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(token);
        var sessions = await ReadAsync<PlatformAdminSession>("platform-sessions.json", cancellationToken);
        return sessions.FirstOrDefault(item =>
            item.RevokedAtUtc is null
            && item.ExpiresAtUtc > now
            && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(item.TokenHash), Convert.FromHexString(tokenHash)));
    }

    public async Task<AuthenticatedCompanyContext?> ResolveSessionAsync(string token, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tokenHash = HashToken(token);
        var sessions = await ReadAsync<AccessSession>("sessions.json", cancellationToken);
        var session = sessions.FirstOrDefault(item =>
            item.RevokedAtUtc is null
            && item.ExpiresAtUtc > now
            && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(item.TokenHash), Convert.FromHexString(tokenHash)));
        if (session is null)
        {
            return null;
        }

        var company = await RequireActiveCompanyAsync(session.CompanyId, now, cancellationToken);
        var account = (await ReadAsync<AccountRecord>("accounts.json", cancellationToken)).FirstOrDefault(item => item.Id == session.AccountId);
        var grant = (await ReadAsync<CompanyAccessGrant>("grants.json", cancellationToken)).FirstOrDefault(item => item.Id == session.GrantId);
        if (account is null
            || !account.IsEnabled
            || grant is null
            || !grant.IsEnabled
            || !IsAccessStatusActive(grant.Status)
            || grant.ExpiresAtUtc.HasValue && grant.ExpiresAtUtc <= now)
        {
            throw new AccessDeniedException("account_access_expired", "Доступ пользователя отключён или срок доступа истёк.");
        }

        return BuildContext(company, account, grant, session.Id);
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var sessions = await ReadAsync<AccessSession>("sessions.json", cancellationToken);
            var index = sessions.FindIndex(item => item.Id == sessionId);
            if (index < 0)
            {
                return;
            }
            var session = sessions[index];
            sessions[index] = new AccessSession
            {
                Id = session.Id,
                CompanyId = session.CompanyId,
                AccountId = session.AccountId,
                GrantId = session.GrantId,
                TokenHash = session.TokenHash,
                CreatedAtUtc = session.CreatedAtUtc,
                ExpiresAtUtc = session.ExpiresAtUtc,
                RevokedAtUtc = DateTime.UtcNow,
                LastUsedAtUtc = session.LastUsedAtUtc,
            };
            await WriteAsync("sessions.json", sessions, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RevokeCompanySessionsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var sessions = await ReadAsync<AccessSession>("sessions.json", cancellationToken);
            sessions = sessions.Select(session => session.CompanyId == companyId && session.RevokedAtUtc is null
                ? new AccessSession
                {
                    Id = session.Id,
                    CompanyId = session.CompanyId,
                    AccountId = session.AccountId,
                    GrantId = session.GrantId,
                    TokenHash = session.TokenHash,
                    CreatedAtUtc = session.CreatedAtUtc,
                    ExpiresAtUtc = session.ExpiresAtUtc,
                    RevokedAtUtc = now,
                    LastUsedAtUtc = session.LastUsedAtUtc,
                }
                : session).ToList();
            await WriteAsync("sessions.json", sessions, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<CompanyAccountView>> GetCompanyAccountsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var accounts = await ReadAsync<AccountRecord>("accounts.json", cancellationToken);
        var grants = (await ReadAsync<CompanyAccessGrant>("grants.json", cancellationToken))
            .Where(item => item.CompanyId == companyId)
            .ToList();

        return grants
            .Join(accounts, grant => grant.AccountId, account => account.Id, (grant, account) => new CompanyAccountView
            {
                AccountId = account.Id,
                GrantId = grant.Id,
                Login = account.Login,
                DisplayName = account.DisplayName,
                RoleKey = grant.RoleKey,
                Status = NormalizeAccountStatus(grant.Status),
                Permissions = grant.Permissions,
                AccessExpiresAtUtc = grant.ExpiresAtUtc,
                IsEnabled = account.IsEnabled && grant.IsEnabled && IsAccessStatusActive(grant.Status),
                CreatedAtUtc = account.CreatedAtUtc,
                LastLoginAtUtc = account.LastLoginAtUtc,
                LastLoginIp = account.LastLoginIp,
            })
            .OrderBy(item => item.Login)
            .ToList();
    }

    public async Task<CompanyAccountView?> GetCompanyAccountAsync(Guid companyId, Guid accountId, CancellationToken cancellationToken) =>
        (await GetCompanyAccountsAsync(companyId, cancellationToken)).FirstOrDefault(item => item.AccountId == accountId);

    public async Task<CompanyAccountView?> UpdateCompanyAccountAccessAsync(
        Guid companyId,
        Guid accountId,
        string status,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizeAccountStatus(status);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var grants = await ReadAsync<CompanyAccessGrant>("grants.json", cancellationToken);
            var grantIndex = grants.FindIndex(item => item.CompanyId == companyId && item.AccountId == accountId);
            if (grantIndex < 0)
            {
                return null;
            }

            var grant = grants[grantIndex];
            grants[grantIndex] = new CompanyAccessGrant
            {
                Id = grant.Id,
                CompanyId = grant.CompanyId,
                AccountId = grant.AccountId,
                RoleKey = grant.RoleKey,
                Status = normalizedStatus,
                Permissions = grant.Permissions,
                ExpiresAtUtc = grant.ExpiresAtUtc,
                IsEnabled = grant.IsEnabled,
                CreatedAtUtc = grant.CreatedAtUtc,
            };
            await WriteAsync("grants.json", grants, cancellationToken);

            if (!IsAccessStatusActive(normalizedStatus))
            {
                await RevokeAccountSessionsUnderLockAsync(companyId, accountId, cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }

        return await GetCompanyAccountAsync(companyId, accountId, cancellationToken);
    }

    public async Task<CompanyAccountView?> ChangeCompanyAccountPasswordAsync(
        Guid companyId,
        Guid accountId,
        string password,
        CancellationToken cancellationToken)
    {
        ValidatePassword(password);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var grants = await ReadAsync<CompanyAccessGrant>("grants.json", cancellationToken);
            if (!grants.Any(item => item.CompanyId == companyId && item.AccountId == accountId))
            {
                return null;
            }

            var accounts = await ReadAsync<AccountRecord>("accounts.json", cancellationToken);
            var accountIndex = accounts.FindIndex(item => item.Id == accountId);
            if (accountIndex < 0)
            {
                return null;
            }

            var account = accounts[accountIndex];
            var (passwordHash, passwordSalt) = HashPassword(password);
            accounts[accountIndex] = CopyAccount(account, passwordHash: passwordHash, passwordSalt: passwordSalt);
            await WriteAsync("accounts.json", accounts, cancellationToken);
            await RevokeAccountSessionsUnderLockAsync(companyId, accountId, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        return await GetCompanyAccountAsync(companyId, accountId, cancellationToken);
    }

    public async Task<List<CompanyInvitationView>> GetCompanyInvitationsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return (await ReadAsync<CompanyInvitation>("invitations.json", cancellationToken))
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new CompanyInvitationView
            {
                Id = item.Id,
                Name = item.Name,
                RoleKey = item.RoleKey,
                Permissions = item.Permissions,
                ExpiresAtUtc = item.ExpiresAtUtc,
                UsedAtUtc = item.UsedAtUtc,
                UsedByAccountId = item.UsedByAccountId,
                RevokedAtUtc = item.RevokedAtUtc,
                CreatedAtUtc = item.CreatedAtUtc,
                IsActive = item.UsedAtUtc is null
                    && item.RevokedAtUtc is null
                    && (!item.ExpiresAtUtc.HasValue || item.ExpiresAtUtc > now),
            })
            .ToList();
    }

    public async Task RevokeCompanyInvitationsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var invitations = await ReadAsync<CompanyInvitation>("invitations.json", cancellationToken);
            invitations = invitations.Select(invitation =>
                invitation.CompanyId == companyId && invitation.RevokedAtUtc is null && invitation.UsedAtUtc is null
                    ? new CompanyInvitation
                    {
                        Id = invitation.Id,
                        CompanyId = invitation.CompanyId,
                        Name = invitation.Name,
                        TokenHash = invitation.TokenHash,
                        RoleKey = invitation.RoleKey,
                        Permissions = invitation.Permissions,
                        ExpiresAtUtc = invitation.ExpiresAtUtc,
                        UsedAtUtc = invitation.UsedAtUtc,
                        UsedByAccountId = invitation.UsedByAccountId,
                        RevokedAtUtc = now,
                        CreatedAtUtc = invitation.CreatedAtUtc,
                    }
                    : invitation).ToList();
            await WriteAsync("invitations.json", invitations, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await GetCompanyAsync(companyId, cancellationToken);
        return company is not null
            && company.Status == CompanyStatus.Active
            && (!company.AccessExpiresAtUtc.HasValue || company.AccessExpiresAtUtc > DateTime.UtcNow);
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<(string SessionToken, AuthenticatedCompanyContext Context)> CreateSessionUnderLockAsync(
        CompanyAccessRecord company,
        AccountRecord account,
        CompanyAccessGrant grant,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new AccessSession
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            AccountId = account.Id,
            GrantId = grant.Id,
            TokenHash = HashToken(token),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(Math.Max(1, _options.SessionLifetimeHours)),
            LastUsedAtUtc = now,
        };
        var sessions = await ReadAsync<AccessSession>("sessions.json", cancellationToken);
        sessions.Add(session);
        await WriteAsync("sessions.json", sessions, cancellationToken);
        return (token, BuildContext(company, account, grant, session.Id));
    }

    private async Task<CompanyAccessRecord> RequireActiveCompanyAsync(Guid companyId, DateTime now, CancellationToken cancellationToken)
    {
        var company = await GetCompanyAsync(companyId, cancellationToken)
            ?? throw new AccessDeniedException("company_not_found", "Компания не найдена.");
        if (company.Status != CompanyStatus.Active)
        {
            throw new AccessDeniedException($"company_{company.Status.ToString().ToLowerInvariant()}", "Доступ компании заблокирован.");
        }
        if (company.AccessExpiresAtUtc.HasValue && company.AccessExpiresAtUtc <= now)
        {
            throw new AccessDeniedException("company_access_expired", "Срок доступа компании истёк.");
        }
        return company;
    }

    private static AuthenticatedCompanyContext BuildContext(
        CompanyAccessRecord company,
        AccountRecord account,
        CompanyAccessGrant grant,
        Guid sessionId) =>
        new()
        {
            CompanyId = company.Id,
            CompanyKey = company.Key,
            CompanyName = company.Name,
            AccountId = account.Id,
            Login = account.Login,
            DisplayName = account.DisplayName,
            RoleKey = grant.RoleKey,
            Permissions = grant.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase),
            SessionId = sessionId,
            AccessExpiresAtUtc = grant.ExpiresAtUtc,
        };

    private async Task RevokeAccountSessionsUnderLockAsync(Guid companyId, Guid accountId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await ReadAsync<AccessSession>("sessions.json", cancellationToken);
        sessions = sessions.Select(session => session.CompanyId == companyId && session.AccountId == accountId && session.RevokedAtUtc is null
            ? new AccessSession
            {
                Id = session.Id,
                CompanyId = session.CompanyId,
                AccountId = session.AccountId,
                GrantId = session.GrantId,
                TokenHash = session.TokenHash,
                CreatedAtUtc = session.CreatedAtUtc,
                ExpiresAtUtc = session.ExpiresAtUtc,
                RevokedAtUtc = now,
                LastUsedAtUtc = session.LastUsedAtUtc,
            }
            : session).ToList();
        await WriteAsync("sessions.json", sessions, cancellationToken);
    }

    private static AccountRecord CopyAccount(
        AccountRecord account,
        string? passwordHash = null,
        string? passwordSalt = null,
        DateTime? lastLoginAtUtc = null,
        string? lastLoginIp = null) =>
        new()
        {
            Id = account.Id,
            Login = account.Login,
            DisplayName = account.DisplayName,
            PasswordHash = passwordHash ?? account.PasswordHash,
            PasswordSalt = passwordSalt ?? account.PasswordSalt,
            IsEnabled = account.IsEnabled,
            CreatedAtUtc = account.CreatedAtUtc,
            LastLoginAtUtc = lastLoginAtUtc ?? account.LastLoginAtUtc,
            LastLoginIp = lastLoginIp ?? account.LastLoginIp,
        };

    private static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    private static bool VerifyPassword(string password, string expectedHash, string salt)
    {
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), 210_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(expectedHash));
    }

    private static void ValidateCredentials(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Trim().Length < 3)
        {
            throw new AccessDeniedException("invalid_login", "Логин должен содержать минимум три символа.");
        }
        ValidatePassword(password);
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8)
        {
            throw new AccessDeniedException("weak_password", "Пароль должен содержать минимум восемь символов.");
        }
    }

    private static string NormalizeRoleKey(string roleKey)
    {
        var normalized = roleKey.Trim().ToLowerInvariant();
        return normalized == CompanyAdminRoleKey ? CompanyAdminRoleKey : CompanyOperatorRoleKey;
    }

    private static IReadOnlyList<string> GetDefaultPermissions(string roleKey) =>
        string.Equals(roleKey, CompanyAdminRoleKey, StringComparison.OrdinalIgnoreCase)
            ? CompanyAdminPermissions
            : CompanyOperatorPermissions;

    private static string NormalizeAccountStatus(string status)
    {
        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            AccessStatusActive => AccessStatusActive,
            AccessStatusSuspended => AccessStatusSuspended,
            AccessStatusDisabled => AccessStatusDisabled,
            _ => throw new AccessDeniedException("invalid_account_status", "Некорректный статус пользователя."),
        };
    }

    private static bool IsAccessStatusActive(string status) =>
        string.Equals(NormalizeAccountStatus(status), AccessStatusActive, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeIpAddress(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();

    private async Task<List<T>> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        var path = EnsurePath(fileName);
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, "[]", cancellationToken);
        }
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task WriteAsync<T>(string fileName, List<T> values, CancellationToken cancellationToken)
    {
        var path = EnsurePath(fileName);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, values, JsonOptions, cancellationToken);
        }
        File.Move(temporaryPath, path, overwrite: true);
    }

    private string EnsurePath(string fileName)
    {
        var root = Path.IsPathRooted(_options.ConfigurationDirectory)
            ? _options.ConfigurationDirectory
            : Path.Combine(_environment.ContentRootPath, _options.ConfigurationDirectory);
        Directory.CreateDirectory(root);
        return Path.Combine(root, fileName);
    }
}

public sealed class AccessDeniedException : Exception
{
    public AccessDeniedException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

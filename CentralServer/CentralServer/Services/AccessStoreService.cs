using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CentralServer.Models;
using CentralisationService.Entities.Models.Access;
using CentralisationService.Entities.Models.Catalog;
using Microsoft.Extensions.Options;
using Npgsql;

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
    private readonly StoreCatalogOptions _storeOptions;
    private readonly PostgreSqlOptions _postgreSqlOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly NpgsqlDataSource _dataSource;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _seeded;

    public AccessStoreService(
        IOptions<AccessOptions> options,
        IOptions<StoreCatalogOptions> storeOptions,
        IOptions<PostgreSqlOptions> postgreSqlOptions,
        IWebHostEnvironment environment,
        NpgsqlDataSource dataSource)
    {
        _options = options.Value;
        _storeOptions = storeOptions.Value;
        _postgreSqlOptions = postgreSqlOptions.Value;
        _environment = environment;
        _dataSource = dataSource;
    }

    public async Task<List<CompanyAccessRecord>> GetCompaniesAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        var result = new List<CompanyAccessRecord>();
        const string sql = """
            SELECT id, key, name, status, access_expires_at_utc, disabled_at_utc, disabled_reason, updated_at_utc
            FROM platform.companies
            ORDER BY name;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadCompany(reader));
        }

        return result;
    }

    public async Task<CompanyAccessRecord?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT id, key, name, status, access_expires_at_utc, disabled_at_utc, disabled_reason, updated_at_utc
            FROM platform.companies
            WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCompany(reader) : null;
    }

    public async Task<CompanyAccessRecord?> GetCompanyByKeyAsync(string companyKey, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT id, key, name, status, access_expires_at_utc, disabled_at_utc, disabled_reason, updated_at_utc
            FROM platform.companies
            WHERE lower(key) = lower(@key);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("key", companyKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCompany(reader) : null;
    }

    public async Task<List<CompanySiteBinding>> GetCompanySitesAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT
                site.id,
                company.id AS company_id,
                company.key AS company_key,
                site.key AS site_key,
                site.name AS site_name,
                server.base_url,
                COALESCE(server.connector_access_token, '') AS connector_access_token,
                site.cleaning_day,
                site.created_at_utc,
                GREATEST(site.updated_at_utc, server.updated_at_utc) AS updated_at_utc,
                CASE WHEN site.status = 'active' AND server.is_enabled THEN NULL ELSE GREATEST(site.updated_at_utc, server.updated_at_utc) END AS disabled_at_utc
            FROM catalog.sites site
            JOIN platform.companies company ON company.id = site.company_id
            JOIN catalog.server_nodes server ON server.site_id = site.id
            ORDER BY company.name, site.name;
            """;

        var result = new List<CompanySiteBinding>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadCompanySiteBinding(reader));
        }

        return result;
    }

    public async Task<List<CompanySiteBinding>> GetCompanySitesAsync(Guid companyId, CancellationToken cancellationToken) =>
        (await GetCompanySitesAsync(cancellationToken))
        .Where(site => site.CompanyId == companyId && site.DisabledAtUtc is null)
        .ToList();

    public async Task<List<RemoteCameraState>> GetCompanyCamerasAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT
                company.key AS company_key,
                site.key AS site_key,
                site.name AS site_name,
                camera.global_camera_key,
                camera.source_camera_key,
                camera.name AS camera_name,
                server.base_url,
                COALESCE(server.connector_access_token, '') AS connector_access_token,
                COALESCE(camera.last_seen_at_utc, camera.updated_at_utc) AS last_sync_at_utc,
                camera.is_enabled AND site.status = 'active' AND server.is_enabled AS is_available
            FROM catalog.cameras camera
            JOIN catalog.sites site ON site.id = camera.site_id
            JOIN platform.companies company ON company.id = camera.company_id
            JOIN catalog.server_nodes server ON server.id = camera.server_node_id
            WHERE camera.company_id = @company_id
            ORDER BY site.name, camera.name;
            """;

        var result = new List<RemoteCameraState>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RemoteCameraState
            {
                CompanyKey = reader.GetString(0),
                SiteKey = reader.GetString(1),
                SiteName = reader.GetString(2),
                CameraKey = reader.GetString(3),
                SourceCameraKey = reader.GetString(4),
                CameraId = int.TryParse(reader.GetString(4), out var cameraId) ? cameraId : null,
                CameraName = reader.GetString(5),
                ServerBaseUrl = reader.GetString(6),
                ConnectorAccessToken = reader.GetString(7),
                LastSyncUtc = reader.GetDateTime(8),
                IsAvailable = reader.GetBoolean(9),
            });
        }

        return result;
    }

    public async Task<CompanySiteBinding> UpsertCompanySiteAsync(CompanySiteBinding site, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var siteId = site.Id == Guid.Empty ? Guid.NewGuid() : site.Id;
            const string siteSql = """
                INSERT INTO catalog.sites (id, company_id, key, name, cleaning_day, status, created_at_utc, updated_at_utc)
                VALUES (@id, @company_id, @key, @name, @cleaning_day, 'active', @created_at_utc, @updated_at_utc)
                ON CONFLICT (company_id, key) DO UPDATE SET
                    name = EXCLUDED.name,
                    cleaning_day = EXCLUDED.cleaning_day,
                    status = 'active',
                    updated_at_utc = EXCLUDED.updated_at_utc
                RETURNING id, created_at_utc;
                """;
            await using (var command = new NpgsqlCommand(siteSql, connection, transaction))
            {
                command.Parameters.AddWithValue("id", siteId);
                command.Parameters.AddWithValue("company_id", site.CompanyId);
                command.Parameters.AddWithValue("key", site.SiteKey.Trim());
                command.Parameters.AddWithValue("name", site.SiteName.Trim());
                command.Parameters.AddWithValue("cleaning_day", site.CleaningDay);
                command.Parameters.AddWithValue("created_at_utc", site.CreatedAtUtc == default ? now : site.CreatedAtUtc);
                command.Parameters.AddWithValue("updated_at_utc", now);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    siteId = reader.GetGuid(0);
                }
            }

            const string serverSql = """
                INSERT INTO catalog.server_nodes (
                    company_id,
                    site_id,
                    connector_id,
                    base_url,
                    connector_access_token,
                    connector_token_hash,
                    is_enabled,
                    created_at_utc,
                    updated_at_utc
                )
                VALUES (
                    @company_id,
                    @site_id,
                    @connector_id,
                    @base_url,
                    @connector_access_token,
                    @connector_token_hash,
                    true,
                    @created_at_utc,
                    @updated_at_utc
                )
                ON CONFLICT (site_id, base_url) DO UPDATE SET
                    connector_access_token = EXCLUDED.connector_access_token,
                    connector_token_hash = EXCLUDED.connector_token_hash,
                    is_enabled = true,
                    updated_at_utc = EXCLUDED.updated_at_utc;
                """;
            await using (var command = new NpgsqlCommand(serverSql, connection, transaction))
            {
                command.Parameters.AddWithValue("company_id", site.CompanyId);
                command.Parameters.AddWithValue("site_id", siteId);
                command.Parameters.AddWithValue("connector_id", site.SiteKey.Trim());
                command.Parameters.AddWithValue("base_url", site.ServerBaseUrl.TrimEnd('/'));
                command.Parameters.AddWithValue("connector_access_token", (object?)site.ConnectorAccessToken ?? DBNull.Value);
                command.Parameters.AddWithValue("connector_token_hash", string.IsNullOrWhiteSpace(site.ConnectorAccessToken) ? DBNull.Value : (object)HashToken(site.ConnectorAccessToken));
                command.Parameters.AddWithValue("created_at_utc", site.CreatedAtUtc == default ? now : site.CreatedAtUtc);
                command.Parameters.AddWithValue("updated_at_utc", now);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new CompanySiteBinding
            {
                Id = siteId,
                CompanyId = site.CompanyId,
                CompanyKey = site.CompanyKey,
                SiteKey = site.SiteKey,
                SiteName = site.SiteName,
                ServerBaseUrl = site.ServerBaseUrl,
                ConnectorAccessToken = site.ConnectorAccessToken ?? string.Empty,
                CleaningDay = site.CleaningDay,
                CreatedAtUtc = site.CreatedAtUtc == default ? now : site.CreatedAtUtc,
                UpdatedAtUtc = now,
                DisabledAtUtc = site.DisabledAtUtc,
            };
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<CompanyAccessRecord> UpsertCompanyAsync(CompanyAccessRecord company, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        var now = DateTime.UtcNow;
        const string sql = """
            INSERT INTO platform.companies (id, key, name, status, access_expires_at_utc, disabled_at_utc, disabled_reason, updated_at_utc)
            VALUES (@id, @key, @name, @status, @access_expires_at_utc, @disabled_at_utc, @disabled_reason, @updated_at_utc)
            ON CONFLICT (key) DO UPDATE SET
                name = EXCLUDED.name,
                status = EXCLUDED.status,
                access_expires_at_utc = EXCLUDED.access_expires_at_utc,
                disabled_at_utc = EXCLUDED.disabled_at_utc,
                disabled_reason = EXCLUDED.disabled_reason,
                updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", company.Id == Guid.Empty ? Guid.NewGuid() : company.Id);
        command.Parameters.AddWithValue("key", company.Key.Trim());
        command.Parameters.AddWithValue("name", company.Name.Trim());
        command.Parameters.AddWithValue("status", ToDbCompanyStatus(company.Status));
        command.Parameters.AddWithValue("access_expires_at_utc", (object?)company.AccessExpiresAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("disabled_at_utc", (object?)company.DisabledAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("disabled_reason", (object?)company.DisabledReason ?? DBNull.Value);
        command.Parameters.AddWithValue("updated_at_utc", company.UpdatedAtUtc == default ? now : company.UpdatedAtUtc);
        var id = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? company.Id);
        return new CompanyAccessRecord
        {
            Id = id,
            Key = company.Key,
            Name = company.Name,
            Status = company.Status,
            AccessExpiresAtUtc = company.AccessExpiresAtUtc,
            DisabledAtUtc = company.DisabledAtUtc,
            DisabledReason = company.DisabledReason,
            UpdatedAtUtc = company.UpdatedAtUtc == default ? now : company.UpdatedAtUtc,
        };
    }

    public async Task<(CompanyInvitation Invitation, string Token)> CreateInvitationAsync(
        Guid companyId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        var roleKey = NormalizeRoleKey(request.RoleKey);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new CompanyInvitation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name.Trim(),
            TokenHash = HashToken(token),
            RoleKey = roleKey,
            Permissions = GetDefaultPermissions(roleKey),
            ExpiresAtUtc = request.ExpiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
        };

        const string sql = """
            INSERT INTO access.company_invitations (id, company_id, name, token_hash, role_id, expires_at_utc, created_at_utc)
            SELECT @id, @company_id, @name, @token_hash, role.id, @expires_at_utc, @created_at_utc
            FROM access.roles role
            WHERE role.key = @role_key;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", invitation.Id);
        command.Parameters.AddWithValue("company_id", invitation.CompanyId);
        command.Parameters.AddWithValue("name", invitation.Name);
        command.Parameters.AddWithValue("token_hash", invitation.TokenHash);
        command.Parameters.AddWithValue("role_key", invitation.RoleKey);
        command.Parameters.AddWithValue("expires_at_utc", (object?)invitation.ExpiresAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at_utc", invitation.CreatedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (invitation, token);
    }

    public async Task<(string SessionToken, AuthenticatedCompanyContext Context)> ActivateInvitationAsync(
        ActivateInvitationRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        ValidateCredentials(request.Login, request.Password);
        await EnsureSeededAsync(cancellationToken);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var invitation = await GetInvitationByHashAsync(HashToken(request.InvitationToken.Trim()), cancellationToken)
                ?? throw new AccessDeniedException("invalid_invitation", "Токен приглашения неверный.");
            if (invitation.UsedAtUtc.HasValue || invitation.RevokedAtUtc.HasValue)
            {
                throw new AccessDeniedException("invitation_unavailable", "Приглашение уже использовано или закрыто.");
            }
            if (invitation.ExpiresAtUtc.HasValue && invitation.ExpiresAtUtc <= now)
            {
                throw new AccessDeniedException("invitation_expired", "Срок действия приглашения истёк.");
            }

            var company = await RequireActiveCompanyAsync(invitation.CompanyId, now, cancellationToken);
            if (await AccountExistsAsync(request.Login.Trim(), cancellationToken))
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
            await InsertAccountAsync(account, cancellationToken);

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
            await InsertGrantAsync(grant, cancellationToken);
            await MarkInvitationUsedAsync(invitation.Id, account.Id, now, cancellationToken);
            return await CreateSessionAsync(company, account, grant, now, cancellationToken);
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
        await EnsureSeededAsync(cancellationToken);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var account = await GetAccountByLoginAsync(request.Login.Trim(), cancellationToken);
            if (account is null || !account.IsEnabled || !VerifyPassword(request.Password, account.PasswordHash, account.PasswordSalt))
            {
                throw new AccessDeniedException("invalid_credentials", "Неверный логин или пароль.");
            }

            var grant = await GetActiveGrantByAccountAsync(account.Id, cancellationToken);
            if (grant is null || grant.ExpiresAtUtc.HasValue && grant.ExpiresAtUtc <= now)
            {
                throw new AccessDeniedException("account_access_expired", "Доступ пользователя отключён или срок доступа истёк.");
            }

            var company = await RequireActiveCompanyAsync(grant.CompanyId, now, cancellationToken);
            account = CopyAccount(account, lastLoginAtUtc: now, lastLoginIp: NormalizeIpAddress(ipAddress));
            await UpdateAccountLoginAsync(account, cancellationToken);
            return await CreateSessionAsync(company, account, grant, now, cancellationToken);
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
        await EnsureSeededAsync(cancellationToken);
        var admin = await EnsurePlatformAdminAsync(cancellationToken);
        var loginMatches = string.Equals(request.Login.Trim(), admin.Login, StringComparison.OrdinalIgnoreCase);
        var passwordMatches = VerifyPassword(request.Password, admin.PasswordHash, admin.PasswordSalt);
        if (!loginMatches || !passwordMatches || !admin.IsEnabled)
        {
            throw new AccessDeniedException("invalid_platform_credentials", "Неверный логин или пароль администратора платформы.");
        }

        var now = DateTime.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new PlatformAdminSession
        {
            Id = Guid.NewGuid(),
            Login = admin.Login,
            TokenHash = HashToken(token),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(Math.Max(1, _options.PlatformSessionLifetimeHours)),
            LastUsedAtUtc = now,
        };

        const string sql = """
            INSERT INTO platform.platform_admin_sessions (id, platform_admin_id, token_hash, created_at_utc, expires_at_utc, last_used_at_utc)
            VALUES (@id, @platform_admin_id, @token_hash, @created_at_utc, @expires_at_utc, @last_used_at_utc);

            UPDATE platform.platform_admins
            SET last_login_at_utc = @created_at_utc,
                updated_at_utc = @created_at_utc
            WHERE id = @platform_admin_id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", session.Id);
        command.Parameters.AddWithValue("platform_admin_id", admin.Id);
        command.Parameters.AddWithValue("token_hash", session.TokenHash);
        command.Parameters.AddWithValue("created_at_utc", session.CreatedAtUtc);
        command.Parameters.AddWithValue("expires_at_utc", session.ExpiresAtUtc);
        command.Parameters.AddWithValue("last_used_at_utc", session.LastUsedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (token, session);
    }

    public async Task<PlatformAdminSession?> ResolvePlatformAdminSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT session.id, admin.login, session.token_hash, session.created_at_utc, session.expires_at_utc, session.revoked_at_utc, session.last_used_at_utc
            FROM platform.platform_admin_sessions session
            JOIN platform.platform_admins admin ON admin.id = session.platform_admin_id
            WHERE session.token_hash = @token_hash
                AND session.revoked_at_utc IS NULL
                AND session.expires_at_utc > @now
                AND admin.is_enabled;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("token_hash", HashToken(token));
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PlatformAdminSession
            {
                Id = reader.GetGuid(0),
                Login = reader.GetString(1),
                TokenHash = reader.GetString(2),
                CreatedAtUtc = reader.GetDateTime(3),
                ExpiresAtUtc = reader.GetDateTime(4),
                RevokedAtUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                LastUsedAtUtc = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            }
            : null;
    }

    public async Task<AuthenticatedCompanyContext?> ResolveSessionAsync(string token, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var session = await GetSessionByTokenHashAsync(HashToken(token), now, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var company = await RequireActiveCompanyAsync(session.CompanyId, now, cancellationToken);
        var account = await GetAccountByIdAsync(session.AccountId, cancellationToken);
        var grant = await GetGrantByIdAsync(session.GrantId, cancellationToken);
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
        await EnsureSeededAsync(cancellationToken);
        const string sql = "UPDATE access.access_sessions SET revoked_at_utc = @now WHERE id = @id;";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeCompanySessionsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = "UPDATE access.access_sessions SET revoked_at_utc = @now WHERE company_id = @company_id AND revoked_at_utc IS NULL;";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<CompanyAccountView>> GetCompanyAccountsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT
                account.id,
                access_grant.id,
                account.login,
                account.display_name,
                role.key,
                access_grant.status,
                access_grant.expires_at_utc,
                account.is_enabled,
                account.created_at_utc,
                account.last_login_at_utc,
                account.last_login_ip::text
            FROM access.company_access_grants access_grant
            JOIN access.accounts account ON account.id = access_grant.account_id
            JOIN access.roles role ON role.id = access_grant.role_id
            WHERE access_grant.company_id = @company_id
            ORDER BY account.login;
            """;

        var result = new List<CompanyAccountView>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var roleKey = reader.GetString(4);
            var status = NormalizeAccountStatus(reader.GetString(5));
            result.Add(new CompanyAccountView
            {
                AccountId = reader.GetGuid(0),
                GrantId = reader.GetGuid(1),
                Login = reader.GetString(2),
                DisplayName = reader.GetString(3),
                RoleKey = roleKey,
                Status = status,
                Permissions = GetDefaultPermissions(roleKey),
                AccessExpiresAtUtc = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                IsEnabled = reader.GetBoolean(7) && IsAccessStatusActive(status),
                CreatedAtUtc = reader.GetDateTime(8),
                LastLoginAtUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                LastLoginIp = reader.IsDBNull(10) ? null : reader.GetString(10),
            });
        }

        return result;
    }

    public async Task<CompanyAccountView?> GetCompanyAccountAsync(Guid companyId, Guid accountId, CancellationToken cancellationToken) =>
        (await GetCompanyAccountsAsync(companyId, cancellationToken)).FirstOrDefault(item => item.AccountId == accountId);

    public async Task<CompanyAccountView?> UpdateCompanyAccountAccessAsync(
        Guid companyId,
        Guid accountId,
        string status,
        CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        var normalizedStatus = NormalizeAccountStatus(status);
        const string sql = """
            UPDATE access.company_access_grants
            SET status = @status,
                updated_at_utc = @now
            WHERE company_id = @company_id AND account_id = @account_id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("status", normalizedStatus);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            return null;
        }
        if (!IsAccessStatusActive(normalizedStatus))
        {
            await RevokeAccountSessionsAsync(companyId, accountId, cancellationToken);
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
        await EnsureSeededAsync(cancellationToken);
        if (!await GrantExistsAsync(companyId, accountId, cancellationToken))
        {
            return null;
        }

        var (passwordHash, passwordSalt) = HashPassword(password);
        const string sql = """
            UPDATE access.accounts
            SET password_hash = @password_hash,
                password_salt = @password_salt,
                updated_at_utc = @now
            WHERE id = @account_id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("password_hash", passwordHash);
        command.Parameters.AddWithValue("password_salt", passwordSalt);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            return null;
        }

        await RevokeAccountSessionsAsync(companyId, accountId, cancellationToken);
        return await GetCompanyAccountAsync(companyId, accountId, cancellationToken);
    }

    public async Task<List<CompanyInvitationView>> GetCompanyInvitationsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT invitation.id, invitation.name, role.key, invitation.expires_at_utc, invitation.used_at_utc,
                invitation.used_by_account_id, invitation.revoked_at_utc, invitation.created_at_utc
            FROM access.company_invitations invitation
            JOIN access.roles role ON role.id = invitation.role_id
            WHERE invitation.company_id = @company_id
            ORDER BY invitation.created_at_utc DESC;
            """;

        var now = DateTime.UtcNow;
        var result = new List<CompanyInvitationView>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var roleKey = reader.GetString(2);
            var expiresAtUtc = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
            var usedAtUtc = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
            var revokedAtUtc = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
            result.Add(new CompanyInvitationView
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                RoleKey = roleKey,
                Permissions = GetDefaultPermissions(roleKey),
                ExpiresAtUtc = expiresAtUtc,
                UsedAtUtc = usedAtUtc,
                UsedByAccountId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                RevokedAtUtc = revokedAtUtc,
                CreatedAtUtc = reader.GetDateTime(7),
                IsActive = usedAtUtc is null && revokedAtUtc is null && (!expiresAtUtc.HasValue || expiresAtUtc > now),
            });
        }

        return result;
    }

    public async Task RevokeCompanyInvitationsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            UPDATE access.company_invitations
            SET revoked_at_utc = @now
            WHERE company_id = @company_id AND revoked_at_utc IS NULL AND used_at_utc IS NULL;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await GetCompanyAsync(companyId, cancellationToken);
        return company is not null
            && company.Status == CompanyStatus.Active
            && (!company.AccessExpiresAtUtc.HasValue || company.AccessExpiresAtUtc > DateTime.UtcNow);
    }

    public async Task UpsertSyncedCamerasAsync(
        string companyKey,
        string siteKey,
        string serverBaseUrl,
        IReadOnlyCollection<RemoteCameraState> cameras,
        CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            INSERT INTO catalog.cameras (company_id, site_id, server_node_id, source_camera_key, global_camera_key, name, is_enabled, last_seen_at_utc, updated_at_utc)
            SELECT company.id, site.id, server.id, @source_camera_key, @global_camera_key, @name, true, @now, @now
            FROM platform.companies company
            JOIN catalog.sites site ON site.company_id = company.id AND site.key = @site_key
            JOIN catalog.server_nodes server ON server.site_id = site.id AND server.base_url = @server_base_url
            WHERE company.key = @company_key
            ON CONFLICT (server_node_id, source_camera_key) DO UPDATE SET
                global_camera_key = EXCLUDED.global_camera_key,
                name = EXCLUDED.name,
                is_enabled = true,
                last_seen_at_utc = EXCLUDED.last_seen_at_utc,
                updated_at_utc = EXCLUDED.updated_at_utc;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var camera in cameras)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("company_key", companyKey);
            command.Parameters.AddWithValue("site_key", siteKey);
            command.Parameters.AddWithValue("server_base_url", serverBaseUrl.TrimEnd('/'));
            command.Parameters.AddWithValue("source_camera_key", camera.SourceCameraKey);
            command.Parameters.AddWithValue("global_camera_key", camera.CameraKey);
            command.Parameters.AddWithValue("name", camera.CameraName);
            command.Parameters.AddWithValue("now", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (_seeded)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (_seeded || await HasCompaniesAsync(cancellationToken) || !_postgreSqlOptions.SeedJsonConfigurationOnEmptyDatabase)
            {
                _seeded = true;
                return;
            }

            var companies = await ReadJsonAsync<CompanyAccessRecord>("companies.json", cancellationToken);
            foreach (var company in companies)
            {
                await UpsertCompanyCoreAsync(company, cancellationToken);
            }

            foreach (var store in _storeOptions.Stores)
            {
                var company = await GetCompanyByKeyCoreAsync(store.CompanyKey, cancellationToken)
                    ?? new CompanyAccessRecord
                    {
                        Id = Guid.NewGuid(),
                        Key = store.CompanyKey,
                        Name = store.CompanyKey.ToUpperInvariant(),
                        Status = CompanyStatus.Active,
                        UpdatedAtUtc = DateTime.UtcNow,
                    };
                if (await GetCompanyByKeyCoreAsync(company.Key, cancellationToken) is null)
                {
                    await UpsertCompanyCoreAsync(company, cancellationToken);
                }

                await UpsertCompanySiteCoreAsync(new CompanySiteBinding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    CompanyKey = company.Key,
                    SiteKey = store.SiteKey,
                    SiteName = store.SiteName,
                    ServerBaseUrl = store.ServerBaseUrl.TrimEnd('/'),
                    ConnectorAccessToken = store.ConnectorAccessToken,
                    CleaningDay = store.CleaningDay,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                }, cancellationToken);
            }

            _seeded = true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<CompanyAccessRecord> UpsertCompanyCoreAsync(CompanyAccessRecord company, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        const string sql = """
            INSERT INTO platform.companies (id, key, name, status, access_expires_at_utc, disabled_at_utc, disabled_reason, updated_at_utc)
            VALUES (@id, @key, @name, @status, @access_expires_at_utc, @disabled_at_utc, @disabled_reason, @updated_at_utc)
            ON CONFLICT (key) DO UPDATE SET
                name = EXCLUDED.name,
                status = EXCLUDED.status,
                access_expires_at_utc = EXCLUDED.access_expires_at_utc,
                disabled_at_utc = EXCLUDED.disabled_at_utc,
                disabled_reason = EXCLUDED.disabled_reason,
                updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", company.Id == Guid.Empty ? Guid.NewGuid() : company.Id);
        command.Parameters.AddWithValue("key", company.Key.Trim());
        command.Parameters.AddWithValue("name", company.Name.Trim());
        command.Parameters.AddWithValue("status", ToDbCompanyStatus(company.Status));
        command.Parameters.AddWithValue("access_expires_at_utc", (object?)company.AccessExpiresAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("disabled_at_utc", (object?)company.DisabledAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("disabled_reason", (object?)company.DisabledReason ?? DBNull.Value);
        command.Parameters.AddWithValue("updated_at_utc", company.UpdatedAtUtc == default ? now : company.UpdatedAtUtc);
        var id = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? company.Id);
        return new CompanyAccessRecord
        {
            Id = id,
            Key = company.Key,
            Name = company.Name,
            Status = company.Status,
            AccessExpiresAtUtc = company.AccessExpiresAtUtc,
            DisabledAtUtc = company.DisabledAtUtc,
            DisabledReason = company.DisabledReason,
            UpdatedAtUtc = company.UpdatedAtUtc,
        };
    }

    private async Task<CompanySiteBinding> UpsertCompanySiteCoreAsync(CompanySiteBinding site, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var siteId = site.Id == Guid.Empty ? Guid.NewGuid() : site.Id;

        const string siteSql = """
            INSERT INTO catalog.sites (id, company_id, key, name, cleaning_day, status, created_at_utc, updated_at_utc)
            VALUES (@id, @company_id, @key, @name, @cleaning_day, 'active', @created_at_utc, @updated_at_utc)
            ON CONFLICT (company_id, key) DO UPDATE SET
                name = EXCLUDED.name,
                cleaning_day = EXCLUDED.cleaning_day,
                status = 'active',
                updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING id;
            """;
        await using (var command = new NpgsqlCommand(siteSql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", siteId);
            command.Parameters.AddWithValue("company_id", site.CompanyId);
            command.Parameters.AddWithValue("key", site.SiteKey.Trim());
            command.Parameters.AddWithValue("name", site.SiteName.Trim());
            command.Parameters.AddWithValue("cleaning_day", site.CleaningDay);
            command.Parameters.AddWithValue("created_at_utc", site.CreatedAtUtc == default ? now : site.CreatedAtUtc);
            command.Parameters.AddWithValue("updated_at_utc", now);
            siteId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? siteId);
        }

        const string serverSql = """
            INSERT INTO catalog.server_nodes (company_id, site_id, connector_id, base_url, connector_access_token, connector_token_hash, is_enabled, created_at_utc, updated_at_utc)
            VALUES (@company_id, @site_id, @connector_id, @base_url, @connector_access_token, @connector_token_hash, true, @created_at_utc, @updated_at_utc)
            ON CONFLICT (site_id, base_url) DO UPDATE SET
                connector_access_token = EXCLUDED.connector_access_token,
                connector_token_hash = EXCLUDED.connector_token_hash,
                is_enabled = true,
                updated_at_utc = EXCLUDED.updated_at_utc;
            """;
        await using (var command = new NpgsqlCommand(serverSql, connection, transaction))
        {
            command.Parameters.AddWithValue("company_id", site.CompanyId);
            command.Parameters.AddWithValue("site_id", siteId);
            command.Parameters.AddWithValue("connector_id", site.SiteKey.Trim());
            command.Parameters.AddWithValue("base_url", site.ServerBaseUrl.TrimEnd('/'));
            command.Parameters.AddWithValue("connector_access_token", (object?)site.ConnectorAccessToken ?? DBNull.Value);
            command.Parameters.AddWithValue("connector_token_hash", string.IsNullOrWhiteSpace(site.ConnectorAccessToken) ? DBNull.Value : (object)HashToken(site.ConnectorAccessToken));
            command.Parameters.AddWithValue("created_at_utc", site.CreatedAtUtc == default ? now : site.CreatedAtUtc);
            command.Parameters.AddWithValue("updated_at_utc", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new CompanySiteBinding
        {
            Id = siteId,
            CompanyId = site.CompanyId,
            CompanyKey = site.CompanyKey,
            SiteKey = site.SiteKey,
            SiteName = site.SiteName,
            ServerBaseUrl = site.ServerBaseUrl,
            ConnectorAccessToken = site.ConnectorAccessToken ?? string.Empty,
            CleaningDay = site.CleaningDay,
            CreatedAtUtc = site.CreatedAtUtc == default ? now : site.CreatedAtUtc,
            UpdatedAtUtc = now,
            DisabledAtUtc = site.DisabledAtUtc,
        };
    }

    private async Task<bool> HasCompaniesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM platform.companies);", connection);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private async Task<CompanyAccessRecord?> GetCompanyByKeyCoreAsync(string companyKey, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, key, name, status, access_expires_at_utc, disabled_at_utc, disabled_reason, updated_at_utc
            FROM platform.companies
            WHERE lower(key) = lower(@key);
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("key", companyKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCompany(reader) : null;
    }

    private async Task<List<T>> ReadJsonAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        var path = EnsurePath(fileName);
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task<PlatformAdminRecord> EnsurePlatformAdminAsync(CancellationToken cancellationToken)
    {
        var admin = await GetPlatformAdminByLoginAsync(_options.PlatformAdminLogin, cancellationToken);
        if (admin is not null)
        {
            return admin;
        }

        var now = DateTime.UtcNow;
        var (passwordHash, passwordSalt) = HashPassword(_options.PlatformAdminPassword);
        admin = new PlatformAdminRecord
        {
            Id = Guid.NewGuid(),
            Login = _options.PlatformAdminLogin,
            DisplayName = "Администратор платформы",
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            IsEnabled = true,
        };
        const string sql = """
            INSERT INTO platform.platform_admins (id, login, display_name, password_hash, password_salt, is_enabled, created_at_utc, updated_at_utc)
            VALUES (@id, @login, @display_name, @password_hash, @password_salt, true, @now, @now)
            ON CONFLICT (login) DO NOTHING;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", admin.Id);
        command.Parameters.AddWithValue("login", admin.Login);
        command.Parameters.AddWithValue("display_name", admin.DisplayName);
        command.Parameters.AddWithValue("password_hash", admin.PasswordHash);
        command.Parameters.AddWithValue("password_salt", admin.PasswordSalt);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetPlatformAdminByLoginAsync(admin.Login, cancellationToken) ?? admin;
    }

    private async Task<PlatformAdminRecord?> GetPlatformAdminByLoginAsync(string login, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, login, display_name, password_hash, password_salt, is_enabled
            FROM platform.platform_admins
            WHERE lower(login) = lower(@login);
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("login", login.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PlatformAdminRecord
            {
                Id = reader.GetGuid(0),
                Login = reader.GetString(1),
                DisplayName = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                PasswordSalt = reader.GetString(4),
                IsEnabled = reader.GetBoolean(5),
            }
            : null;
    }

    private async Task<AccountRecord?> GetAccountByLoginAsync(string login, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, login, display_name, password_hash, password_salt, is_enabled, created_at_utc, last_login_at_utc, last_login_ip::text
            FROM access.accounts
            WHERE lower(login) = lower(@login);
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("login", login);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    private async Task<AccountRecord?> GetAccountByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, login, display_name, password_hash, password_salt, is_enabled, created_at_utc, last_login_at_utc, last_login_ip::text
            FROM access.accounts
            WHERE id = @id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    private async Task<bool> AccountExistsAsync(string login, CancellationToken cancellationToken) =>
        await GetAccountByLoginAsync(login, cancellationToken) is not null;

    private async Task InsertAccountAsync(AccountRecord account, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO access.accounts (id, login, display_name, password_hash, password_salt, is_enabled, created_at_utc, last_login_at_utc, last_login_ip)
            VALUES (@id, @login, @display_name, @password_hash, @password_salt, @is_enabled, @created_at_utc, @last_login_at_utc, CAST(@last_login_ip AS inet));
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", account.Id);
        command.Parameters.AddWithValue("login", account.Login);
        command.Parameters.AddWithValue("display_name", account.DisplayName);
        command.Parameters.AddWithValue("password_hash", account.PasswordHash);
        command.Parameters.AddWithValue("password_salt", account.PasswordSalt);
        command.Parameters.AddWithValue("is_enabled", account.IsEnabled);
        command.Parameters.AddWithValue("created_at_utc", account.CreatedAtUtc);
        command.Parameters.AddWithValue("last_login_at_utc", (object?)account.LastLoginAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("last_login_ip", (object?)account.LastLoginIp ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateAccountLoginAsync(AccountRecord account, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE access.accounts
            SET last_login_at_utc = @last_login_at_utc,
                last_login_ip = CAST(@last_login_ip AS inet),
                updated_at_utc = @updated_at_utc
            WHERE id = @id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", account.Id);
        command.Parameters.AddWithValue("last_login_at_utc", (object?)account.LastLoginAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("last_login_ip", (object?)account.LastLoginIp ?? DBNull.Value);
        command.Parameters.AddWithValue("updated_at_utc", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertGrantAsync(CompanyAccessGrant grant, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO access.company_access_grants (id, company_id, account_id, role_id, status, expires_at_utc, created_at_utc, updated_at_utc)
            SELECT @id, @company_id, @account_id, role.id, @status, @expires_at_utc, @created_at_utc, @created_at_utc
            FROM access.roles role
            WHERE role.key = @role_key;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", grant.Id);
        command.Parameters.AddWithValue("company_id", grant.CompanyId);
        command.Parameters.AddWithValue("account_id", grant.AccountId);
        command.Parameters.AddWithValue("role_key", grant.RoleKey);
        command.Parameters.AddWithValue("status", grant.Status);
        command.Parameters.AddWithValue("expires_at_utc", (object?)grant.ExpiresAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at_utc", grant.CreatedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<CompanyAccessGrant?> GetActiveGrantByAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT access_grant.id, access_grant.company_id, access_grant.account_id, role.key, access_grant.status, access_grant.expires_at_utc, access_grant.created_at_utc
            FROM access.company_access_grants access_grant
            JOIN access.roles role ON role.id = access_grant.role_id
            WHERE access_grant.account_id = @account_id AND access_grant.status = 'active'
            ORDER BY access_grant.created_at_utc DESC
            LIMIT 1;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("account_id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadGrant(reader) : null;
    }

    private async Task<CompanyAccessGrant?> GetGrantByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT access_grant.id, access_grant.company_id, access_grant.account_id, role.key, access_grant.status, access_grant.expires_at_utc, access_grant.created_at_utc
            FROM access.company_access_grants access_grant
            JOIN access.roles role ON role.id = access_grant.role_id
            WHERE access_grant.id = @id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadGrant(reader) : null;
    }

    private async Task<bool> GrantExistsAsync(Guid companyId, Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM access.company_access_grants WHERE company_id = @company_id AND account_id = @account_id);";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("account_id", accountId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private async Task<CompanyInvitation?> GetInvitationByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT invitation.id, invitation.company_id, invitation.name, invitation.token_hash, role.key,
                invitation.expires_at_utc, invitation.used_at_utc, invitation.used_by_account_id,
                invitation.revoked_at_utc, invitation.created_at_utc
            FROM access.company_invitations invitation
            JOIN access.roles role ON role.id = invitation.role_id
            WHERE invitation.token_hash = @token_hash;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CompanyInvitation
            {
                Id = reader.GetGuid(0),
                CompanyId = reader.GetGuid(1),
                Name = reader.GetString(2),
                TokenHash = reader.GetString(3),
                RoleKey = reader.GetString(4),
                Permissions = GetDefaultPermissions(reader.GetString(4)),
                ExpiresAtUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                UsedAtUtc = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                UsedByAccountId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                RevokedAtUtc = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                CreatedAtUtc = reader.GetDateTime(9),
            }
            : null;
    }

    private async Task MarkInvitationUsedAsync(Guid invitationId, Guid accountId, DateTime usedAtUtc, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE access.company_invitations SET used_at_utc = @used_at_utc, used_by_account_id = @account_id WHERE id = @id;";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", invitationId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("used_at_utc", usedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<(string SessionToken, AuthenticatedCompanyContext Context)> CreateSessionAsync(
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
        const string sql = """
            INSERT INTO access.access_sessions (id, company_id, account_id, grant_id, token_hash, created_at_utc, expires_at_utc, last_used_at_utc)
            VALUES (@id, @company_id, @account_id, @grant_id, @token_hash, @created_at_utc, @expires_at_utc, @last_used_at_utc);
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", session.Id);
        command.Parameters.AddWithValue("company_id", session.CompanyId);
        command.Parameters.AddWithValue("account_id", session.AccountId);
        command.Parameters.AddWithValue("grant_id", session.GrantId);
        command.Parameters.AddWithValue("token_hash", session.TokenHash);
        command.Parameters.AddWithValue("created_at_utc", session.CreatedAtUtc);
        command.Parameters.AddWithValue("expires_at_utc", session.ExpiresAtUtc);
        command.Parameters.AddWithValue("last_used_at_utc", session.LastUsedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (token, BuildContext(company, account, grant, session.Id));
    }

    private async Task<AccessSession?> GetSessionByTokenHashAsync(string tokenHash, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, company_id, account_id, grant_id, token_hash, created_at_utc, expires_at_utc, revoked_at_utc, last_used_at_utc
            FROM access.access_sessions
            WHERE token_hash = @token_hash AND revoked_at_utc IS NULL AND expires_at_utc > @now;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AccessSession
            {
                Id = reader.GetGuid(0),
                CompanyId = reader.GetGuid(1),
                AccountId = reader.GetGuid(2),
                GrantId = reader.GetGuid(3),
                TokenHash = reader.GetString(4),
                CreatedAtUtc = reader.GetDateTime(5),
                ExpiresAtUtc = reader.GetDateTime(6),
                RevokedAtUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                LastUsedAtUtc = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
            }
            : null;
    }

    private async Task RevokeAccountSessionsAsync(Guid companyId, Guid accountId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE access.access_sessions
            SET revoked_at_utc = @now
            WHERE company_id = @company_id AND account_id = @account_id AND revoked_at_utc IS NULL;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static CompanyAccessRecord ReadCompany(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Key = reader.GetString(1),
        Name = reader.GetString(2),
        Status = FromDbCompanyStatus(reader.GetString(3)),
        AccessExpiresAtUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
        DisabledAtUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
        DisabledReason = reader.IsDBNull(6) ? null : reader.GetString(6),
        UpdatedAtUtc = reader.GetDateTime(7),
    };

    private static CompanySiteBinding ReadCompanySiteBinding(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        CompanyId = reader.GetGuid(1),
        CompanyKey = reader.GetString(2),
        SiteKey = reader.GetString(3),
        SiteName = reader.GetString(4),
        ServerBaseUrl = reader.GetString(5),
        ConnectorAccessToken = reader.GetString(6),
        CleaningDay = reader.GetInt32(7),
        CreatedAtUtc = reader.GetDateTime(8),
        UpdatedAtUtc = reader.GetDateTime(9),
        DisabledAtUtc = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
    };

    private static AccountRecord ReadAccount(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Login = reader.GetString(1),
        DisplayName = reader.GetString(2),
        PasswordHash = reader.GetString(3),
        PasswordSalt = reader.GetString(4),
        IsEnabled = reader.GetBoolean(5),
        CreatedAtUtc = reader.GetDateTime(6),
        LastLoginAtUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
        LastLoginIp = reader.IsDBNull(8) ? null : reader.GetString(8),
    };

    private static CompanyAccessGrant ReadGrant(NpgsqlDataReader reader)
    {
        var roleKey = reader.GetString(3);
        return new CompanyAccessGrant
        {
            Id = reader.GetGuid(0),
            CompanyId = reader.GetGuid(1),
            AccountId = reader.GetGuid(2),
            RoleKey = roleKey,
            Status = NormalizeAccountStatus(reader.GetString(4)),
            Permissions = GetDefaultPermissions(roleKey),
            ExpiresAtUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            IsEnabled = true,
            CreatedAtUtc = reader.GetDateTime(6),
        };
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

    private static string ToDbCompanyStatus(CompanyStatus status) =>
        status.ToString().ToLowerInvariant();

    private static CompanyStatus FromDbCompanyStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "active" => CompanyStatus.Active,
            "suspended" => CompanyStatus.Suspended,
            "disabled" => CompanyStatus.Disabled,
            "archived" => CompanyStatus.Archived,
            _ => CompanyStatus.Disabled,
        };

    private string EnsurePath(string fileName)
    {
        var root = Path.IsPathRooted(_options.ConfigurationDirectory)
            ? _options.ConfigurationDirectory
            : Path.Combine(_environment.ContentRootPath, _options.ConfigurationDirectory);
        Directory.CreateDirectory(root);
        return Path.Combine(root, fileName);
    }

    private sealed class PlatformAdminRecord
    {
        public Guid Id { get; init; }
        public string Login { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
        public string PasswordSalt { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
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

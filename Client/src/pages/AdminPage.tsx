import AddBusinessRoundedIcon from '@mui/icons-material/AddBusinessRounded';
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded';
import CameraAltRoundedIcon from '@mui/icons-material/CameraAltRounded';
import CircleRoundedIcon from '@mui/icons-material/CircleRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import ContentCopyRoundedIcon from '@mui/icons-material/ContentCopyRounded';
import KeyRoundedIcon from '@mui/icons-material/KeyRounded';
import LockResetRoundedIcon from '@mui/icons-material/LockResetRounded';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import MapRoundedIcon from '@mui/icons-material/MapRounded';
import PauseCircleOutlineRoundedIcon from '@mui/icons-material/PauseCircleOutlineRounded';
import PlayCircleOutlineRoundedIcon from '@mui/icons-material/PlayCircleOutlineRounded';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import SecurityRoundedIcon from '@mui/icons-material/SecurityRounded';
import StorefrontRoundedIcon from '@mui/icons-material/StorefrontRounded';
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import CentralZoneConfigurator from '../components/stores/CentralZoneConfigurator';
import { useAppStore } from '../store';
import { PAGE_CONTENT_MAX_WIDTH, UI_RADIUS_PX } from '../theme/designTokens';
import type {
  CameraDto,
  CompanyAccessDto,
  CompanyAccountDto,
  CompanyInvitationDto,
  CompanySiteDto,
  CreateInvitationResponseDto,
} from '../types/central';

const STATUS_LABELS: Record<CompanyAccessDto['status'], string> = {
  active: 'Активна',
  suspended: 'Приостановлена',
  disabled: 'Отключена',
  archived: 'Архив',
};

const ROLE_LABELS: Record<string, string> = {
  'company-admin': 'Администратор',
  'company-operator': 'Оператор',
};

const ACCOUNT_STATUS_LABELS: Record<CompanyAccountDto['status'], string> = {
  active: 'Активен',
  suspended: 'Приостановлен',
  disabled: 'Заблокирован',
};

const toInputDateTime = (value: string | null) => (value ? value.slice(0, 16) : '');
const toUtcIsoOrNull = (value: string) => (value ? new Date(value).toISOString() : null);
const formatDate = (value: string | null) => (value ? new Date(value).toLocaleString('ru-RU') : 'Без срока');

const AdminPage = () => {
  const navigate = useNavigate();
  const { baseUrl, platformLogin, platformAuthorizedFetch, platformLogout, showAlert, showSnackbar } = useAppStore();
  const [companies, setCompanies] = useState<CompanyAccessDto[]>([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState<string | null>(null);
  const [tab, setTab] = useState<'sites' | 'users'>('sites');
  const [sites, setSites] = useState<CompanySiteDto[]>([]);
  const [accounts, setAccounts] = useState<CompanyAccountDto[]>([]);
  const [invitations, setInvitations] = useState<CompanyInvitationDto[]>([]);
  const [selectedSiteKey, setSelectedSiteKey] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [createKey, setCreateKey] = useState('');
  const [createName, setCreateName] = useState('');
  const [createExpiresAt, setCreateExpiresAt] = useState('');
  const [serverAddress, setServerAddress] = useState('');
  const [siteKey, setSiteKey] = useState('');
  const [siteName, setSiteName] = useState('');
  const [invitationName, setInvitationName] = useState('Оператор точки');
  const [invitationRoleKey, setInvitationRoleKey] = useState<'company-admin' | 'company-operator'>('company-operator');
  const [invitationExpiresAt, setInvitationExpiresAt] = useState('');
  const [lastInvitationToken, setLastInvitationToken] = useState<string | null>(null);
  const [accessExpirationByCompany, setAccessExpirationByCompany] = useState<Record<string, string>>({});
  const [selectedAccount, setSelectedAccount] = useState<CompanyAccountDto | null>(null);
  const [newAccountPassword, setNewAccountPassword] = useState('');
  const [zoneSettingsOpen, setZoneSettingsOpen] = useState(false);

  const selectedCompany = useMemo(
    () => companies.find((company) => company.id === selectedCompanyId) ?? null,
    [companies, selectedCompanyId],
  );
  const selectedSite = useMemo(
    () => sites.find((site) => site.siteKey === selectedSiteKey) ?? null,
    [selectedSiteKey, sites],
  );
  const activeCompaniesCount = useMemo(
    () => companies.filter((company) => company.status === 'active').length,
    [companies],
  );
  const activeInvitationsCount = useMemo(
    () => invitations.filter((invitation) => invitation.isActive).length,
    [invitations],
  );
  const selectedSiteCameras = useMemo<CameraDto[]>(() => {
    if (!selectedSite) {
      return [];
    }

    return selectedSite.cameras.map((camera) => ({
      key: camera.cameraKey,
      name: camera.cameraName,
      siteKey: selectedSite.siteKey,
      siteName: selectedSite.siteName,
      cameraId: camera.cameraId,
      sourceCameraKey: camera.sourceCameraKey,
      serverBaseUrl: selectedSite.serverBaseUrl,
      lastSyncUtc: selectedSite.lastSyncUtc,
      isAvailable: camera.isAvailable,
    }));
  }, [selectedSite]);

  const loadCompanies = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies`);
      if (!response.ok) {
        throw new Error(`Центральный сервер вернул ошибку ${response.status}.`);
      }
      const data = (await response.json()) as CompanyAccessDto[];
      setCompanies(data);
      setAccessExpirationByCompany(
        data.reduce<Record<string, string>>((accumulator, company) => {
          accumulator[company.id] = toInputDateTime(company.accessExpiresAtUtc);
          return accumulator;
        }, {}),
      );
    } catch (error: any) {
      showAlert('Ошибка', error?.message ?? 'Не удалось загрузить компании.');
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl, platformAuthorizedFetch, showAlert]);

  const loadCompanyDetails = useCallback(async (companyId: string) => {
    const [sitesResponse, accountsResponse, invitationsResponse] = await Promise.all([
      platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${companyId}/sites`),
      platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${companyId}/accounts`),
      platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${companyId}/invitations`),
    ]);
    if (!sitesResponse.ok || !accountsResponse.ok || !invitationsResponse.ok) {
      throw new Error('Не удалось загрузить данные компании.');
    }
    const nextSites = (await sitesResponse.json()) as CompanySiteDto[];
    setSites(nextSites);
    setAccounts((await accountsResponse.json()) as CompanyAccountDto[]);
    setInvitations((await invitationsResponse.json()) as CompanyInvitationDto[]);
    setSelectedSiteKey((current) => current ?? nextSites[0]?.siteKey ?? null);
  }, [baseUrl, platformAuthorizedFetch]);

  useEffect(() => {
    void loadCompanies();
  }, [loadCompanies]);

  useEffect(() => {
    if (!selectedCompanyId) {
      return;
    }
    void loadCompanyDetails(selectedCompanyId).catch((error: any) => {
      showAlert('Ошибка', error?.message ?? 'Не удалось загрузить данные компании.');
    });
  }, [loadCompanyDetails, selectedCompanyId, showAlert]);

  const createCompany = async () => {
    if (!createKey.trim() || !createName.trim()) {
      showAlert('Проверь данные', 'Нужно указать ключ и название компании.');
      return;
    }
    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        key: createKey.trim(),
        name: createName.trim(),
        accessExpiresAtUtc: toUtcIsoOrNull(createExpiresAt),
      }),
    });
    if (!response.ok) {
      showAlert('Ошибка', `Центральный сервер вернул ошибку ${response.status}.`);
      return;
    }
    setCreateKey('');
    setCreateName('');
    setCreateExpiresAt('');
    showSnackbar('Компания создана.');
    await loadCompanies();
  };

  const updateCompanyAccess = async (company: CompanyAccessDto, status: CompanyAccessDto['status']) => {
    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${company.id}/access`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        status,
        accessExpiresAtUtc: toUtcIsoOrNull(accessExpirationByCompany[company.id] ?? ''),
        reason: status === 'active' ? null : 'Изменено из панели администратора',
      }),
    });
    if (!response.ok) {
      showAlert('Ошибка', `Центральный сервер вернул ошибку ${response.status}.`);
      return;
    }
    showSnackbar(status === 'active' ? 'Компания включена.' : 'Доступ компании изменён.');
    await loadCompanies();
  };

  const bindServer = async () => {
    if (!selectedCompany || !serverAddress.trim() || !siteName.trim()) {
      showAlert('Проверь данные', 'Выбери компанию, укажи адрес Server и корректное название точки.');
      return;
    }
    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${selectedCompany.id}/sites`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        serverAddress: serverAddress.trim(),
        siteKey: siteKey.trim(),
        siteName: siteName.trim(),
        cleaningDay: 0,
      }),
    });
    if (!response.ok) {
      showAlert('Ошибка', `Не удалось привязать сервер точки. Код ошибки: ${response.status}.`);
      return;
    }
    setServerAddress('');
    setSiteKey('');
    setSiteName('');
    showSnackbar('Server привязан к компании.');
    await loadCompanyDetails(selectedCompany.id);
  };

  const createInvitation = async () => {
    if (!selectedCompany) {
      return;
    }
    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${selectedCompany.id}/invitations`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: invitationName.trim() || 'Оператор точки',
        roleKey: invitationRoleKey,
        expiresAtUtc: toUtcIsoOrNull(invitationExpiresAt),
      }),
    });
    const data = await response.json() as CreateInvitationResponseDto & { message?: string };
    if (!response.ok || !data.token) {
      showAlert('Ошибка', data.message ?? `Центральный сервер вернул ошибку ${response.status}.`);
      return;
    }
      setLastInvitationToken(data.token);
    showSnackbar('Одноразовый токен создан. Он показан только сейчас.');
    await loadCompanyDetails(selectedCompany.id);
  };

  const revokeActiveInvitations = async () => {
    if (!selectedCompany) {
      return;
    }
    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${selectedCompany.id}/invitations/revoke-active`, {
      method: 'POST',
    });
    if (!response.ok) {
      showAlert('Ошибка', `Центральный сервер вернул ошибку ${response.status}.`);
      return;
    }
    showSnackbar('Активные приглашения закрыты.');
    await loadCompanyDetails(selectedCompany.id);
  };

  const copyInvitationToken = async () => {
    if (!lastInvitationToken) {
      return;
    }
    await navigator.clipboard.writeText(lastInvitationToken);
    showSnackbar('Токен скопирован.');
  };

  const refreshSelectedAccount = async (accountId: string) => {
    if (!selectedCompany) {
      return null;
    }

    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${selectedCompany.id}/accounts/${accountId}`);
    if (!response.ok) {
      showAlert('Ошибка', `Не удалось загрузить пользователя. Код ошибки: ${response.status}.`);
      return null;
    }

    const account = (await response.json()) as CompanyAccountDto;
    setSelectedAccount(account);
    return account;
  };

  const openAccount = async (account: CompanyAccountDto) => {
    setSelectedAccount(account);
    setNewAccountPassword('');
    await refreshSelectedAccount(account.accountId);
  };

  const updateAccountAccess = async (status: CompanyAccountDto['status']) => {
    if (!selectedCompany || !selectedAccount) {
      return;
    }

    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${selectedCompany.id}/accounts/${selectedAccount.accountId}/access`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    });
    if (!response.ok) {
      showAlert('Ошибка', `Не удалось изменить доступ пользователя. Код ошибки: ${response.status}.`);
      return;
    }

    const account = (await response.json()) as CompanyAccountDto;
    setSelectedAccount(account);
    showSnackbar(status === 'active' ? 'Доступ пользователя включён.' : 'Доступ пользователя изменён.');
    await loadCompanyDetails(selectedCompany.id);
  };

  const changeAccountPassword = async () => {
    if (!selectedCompany || !selectedAccount) {
      return;
    }
    if (newAccountPassword.length < 8) {
      showAlert('Проверь данные', 'Пароль должен содержать минимум восемь символов.');
      return;
    }

    const response = await platformAuthorizedFetch(`${baseUrl}/api/platform/companies/${selectedCompany.id}/accounts/${selectedAccount.accountId}/password`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ password: newAccountPassword }),
    });
    if (!response.ok) {
      showAlert('Ошибка', `Не удалось задать новый пароль. Код ошибки: ${response.status}.`);
      return;
    }

    const account = (await response.json()) as CompanyAccountDto;
    setSelectedAccount(account);
    setNewAccountPassword('');
    showSnackbar('Новый пароль задан. Активные сессии пользователя закрыты.');
    await loadCompanyDetails(selectedCompany.id);
  };

  const logout = () => {
    platformLogout();
    navigate('/login');
  };

  const openCompany = (company: CompanyAccessDto) => {
    setSelectedCompanyId(company.id);
    setSelectedSiteKey(null);
    setLastInvitationToken(null);
    setSelectedAccount(null);
    setZoneSettingsOpen(false);
    setTab('sites');
  };

  return (
    <Box sx={{ minHeight: '100vh', background: '#020617', px: { xs: 2, md: 3 }, py: { xs: 2, md: 3 } }}>
      <Box sx={{ maxWidth: PAGE_CONTENT_MAX_WIDTH, mx: 'auto', display: 'grid', gap: 2.5 }}>
        <Paper sx={{ p: { xs: 2, md: 3 }, borderRadius: UI_RADIUS_PX, background: 'linear-gradient(135deg, rgba(15,23,42,0.98), rgba(30,41,59,0.82))' }}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ xs: 'flex-start', md: 'center' }} justifyContent="space-between">
            <Box>
              <Typography variant="overline" sx={{ color: 'rgba(148,163,184,0.78)', letterSpacing: '0.14em' }}>
                Администрирование платформы
              </Typography>
              <Typography variant="h4" sx={{ fontWeight: 900, mt: 0.4 }}>
                {selectedCompany ? selectedCompany.name : 'Компании'}
              </Typography>
              <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 820 }}>
                {selectedCompany
                  ? 'Здесь управляем точками компании, доступностью site-side Server, пользователями и одноразовыми приглашениями.'
                  : 'Выбери компанию, чтобы открыть её точки, пользователей, токены и административные настройки.'}
              </Typography>
            </Box>
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              <Chip icon={<SecurityRoundedIcon />} label={platformLogin ?? 'admin'} color="primary" variant="outlined" />
              <Button startIcon={<RefreshRoundedIcon />} variant="outlined" onClick={() => selectedCompanyId ? void loadCompanyDetails(selectedCompanyId) : void loadCompanies()} disabled={isLoading}>
                Обновить
              </Button>
              <Button startIcon={<LogoutRoundedIcon />} color="warning" variant="outlined" onClick={logout}>
                Выйти
              </Button>
            </Stack>
          </Stack>
        </Paper>

        {!selectedCompany ? (
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '0.78fr 1.22fr' }, gap: 2.5, alignItems: 'start' }}>
            <Paper sx={{ p: 2.5 }}>
              <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>
                Создать компанию
              </Typography>
              <Stack spacing={1.5}>
                <TextField label="Технический ключ компании" value={createKey} onChange={(event) => setCreateKey(event.target.value)} placeholder="sve" />
                <TextField label="Название компании" value={createName} onChange={(event) => setCreateName(event.target.value)} placeholder="SVE" />
                <TextField label="Доступ до" type="datetime-local" value={createExpiresAt} onChange={(event) => setCreateExpiresAt(event.target.value)} InputLabelProps={{ shrink: true }} />
                <Button startIcon={<AddBusinessRoundedIcon />} variant="contained" onClick={() => void createCompany()}>
                  Добавить компанию
                </Button>
              </Stack>
            </Paper>

            <Paper sx={{ p: { xs: 2, md: 2.5 } }}>
              <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" spacing={1.5} sx={{ mb: 2 }}>
                <Box>
                  <Typography variant="h6" sx={{ fontWeight: 800 }}>
                    Список компаний
                  </Typography>
                  <Typography color="text.secondary" variant="body2">
                    Активно: {activeCompaniesCount} из {companies.length}
                  </Typography>
                </Box>
                {isLoading ? <Chip label="Загрузка..." color="info" variant="outlined" /> : null}
              </Stack>
              <Stack spacing={1.5}>
                {companies.map((company) => (
                  <Box key={company.id} onClick={() => openCompany(company)} sx={{ p: 1.8, borderRadius: UI_RADIUS_PX, border: '1px solid rgba(148,163,184,0.16)', background: 'rgba(15,23,42,0.58)', cursor: 'pointer', '&:hover': { borderColor: 'rgba(96,165,250,0.46)', transform: 'translateY(-1px)' } }}>
                    <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} justifyContent="space-between">
                      <Box>
                        <Typography sx={{ fontWeight: 850, fontSize: '1.05rem' }}>{company.name}</Typography>
                        <Typography variant="body2" color="text.secondary">{company.key}</Typography>
                      </Box>
                      <Chip label={STATUS_LABELS[company.status] ?? company.status} color={company.status === 'active' ? 'success' : 'warning'} variant="outlined" />
                    </Stack>
                    <Divider sx={{ my: 1.5 }} />
                    <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.25} alignItems={{ xs: 'stretch', md: 'center' }} onClick={(event) => event.stopPropagation()}>
                      <TextField
                        label="Доступ компании до"
                        type="datetime-local"
                        size="small"
                        value={accessExpirationByCompany[company.id] ?? ''}
                        onChange={(event) => setAccessExpirationByCompany((current) => ({ ...current, [company.id]: event.target.value }))}
                        InputLabelProps={{ shrink: true }}
                        sx={{ minWidth: { md: 250 } }}
                      />
                      <Button variant="outlined" onClick={() => void updateCompanyAccess(company, 'active')}>Включить</Button>
                      <Button color="warning" variant="outlined" onClick={() => void updateCompanyAccess(company, 'suspended')}>Приостановить</Button>
                      <Button color="error" variant="outlined" onClick={() => void updateCompanyAccess(company, 'disabled')}>Отключить</Button>
                    </Stack>
                  </Box>
                ))}
                {!isLoading && companies.length === 0 ? <Alert severity="info">Компаний пока нет. Создай первую компанию слева.</Alert> : null}
              </Stack>
            </Paper>
          </Box>
        ) : (
          <Box sx={{ display: 'grid', gap: 2.5 }}>
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.2} justifyContent="space-between">
              <Button startIcon={<ArrowBackRoundedIcon />} variant="outlined" onClick={() => setSelectedCompanyId(null)}>
                Назад к компаниям
              </Button>
              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                <Chip label={STATUS_LABELS[selectedCompany.status]} color={selectedCompany.status === 'active' ? 'success' : 'warning'} variant="outlined" />
                <Chip label={`Доступ: ${formatDate(selectedCompany.accessExpiresAtUtc)}`} variant="outlined" />
              </Stack>
            </Stack>

            <Paper sx={{ p: { xs: 2, md: 2.5 } }}>
              <Tabs value={tab} onChange={(_, value) => setTab(value)} sx={{ mb: 2 }}>
                <Tab value="sites" label="Точки" />
                <Tab value="users" label="Пользователи и токены" />
              </Tabs>

              {tab === 'sites' ? (
                <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', xl: '0.72fr 1.28fr' }, gap: 2.5 }}>
                  <Box sx={{ display: 'grid', gap: 2 }}>
                    <Paper sx={{ p: 2, background: 'rgba(15,23,42,0.52)' }}>
                      <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>Добавить Server</Typography>
                      <Stack spacing={1.4}>
                        <TextField label="Адрес Server" value={serverAddress} onChange={(event) => setServerAddress(event.target.value)} placeholder="192.168.2.12 или http://192.168.2.12:5120" />
                <TextField label="Технический ключ точки" value={siteKey} onChange={(event) => setSiteKey(event.target.value)} placeholder="sve-svoboda" />
                <TextField label="Корректное название точки" value={siteName} onChange={(event) => setSiteName(event.target.value)} placeholder="SVE Свобода" required />
                        <Button startIcon={<StorefrontRoundedIcon />} variant="contained" onClick={() => void bindServer()}>
                          Привязать Server
                        </Button>
                      </Stack>
                    </Paper>

                    <Stack spacing={1.2}>
                      {sites.map((site) => (
                        <Box key={site.siteKey} onClick={() => setSelectedSiteKey(site.siteKey)} sx={{ p: 1.6, borderRadius: UI_RADIUS_PX, border: selectedSiteKey === site.siteKey ? '1px solid rgba(96,165,250,0.56)' : '1px solid rgba(148,163,184,0.16)', background: selectedSiteKey === site.siteKey ? 'rgba(37,99,235,0.18)' : 'rgba(15,23,42,0.56)', cursor: 'pointer' }}>
                          <Stack direction="row" spacing={1.2} justifyContent="space-between" alignItems="center">
                            <Box>
                              <Typography sx={{ fontWeight: 800 }}>{site.siteName}</Typography>
                              <Typography variant="body2" color="text.secondary">{site.serverBaseUrl}</Typography>
                            </Box>
                            <CircleRoundedIcon sx={{ fontSize: 16, color: site.isAvailable ? '#22c55e' : '#ef4444', filter: site.isAvailable ? 'drop-shadow(0 0 8px rgba(34,197,94,0.7))' : 'drop-shadow(0 0 8px rgba(239,68,68,0.55))' }} />
                          </Stack>
                          <Stack direction="row" spacing={0.75} sx={{ mt: 1 }} flexWrap="wrap" useFlexGap>
                            <Chip size="small" label={site.isAvailable ? 'Сервер доступен' : 'Сервер недоступен'} color={site.isAvailable ? 'success' : 'error'} variant="outlined" />
                            <Chip size="small" label={`Камер: ${site.cameras.length}`} variant="outlined" />
                          </Stack>
                        </Box>
                      ))}
                      {sites.length === 0 ? <Alert severity="info">У компании пока нет привязанных Server.</Alert> : null}
                    </Stack>
                  </Box>

                  <Paper sx={{ p: 2.5, background: 'rgba(15,23,42,0.52)' }}>
                    {selectedSite ? (
                      <Stack spacing={2}>
                        <Box>
                          <Typography variant="overline" color="text.secondary">Настройка точки</Typography>
                          <Typography variant="h5" sx={{ fontWeight: 850 }}>{selectedSite.siteName}</Typography>
                          <Typography color="text.secondary" sx={{ mt: 0.6 }}>{selectedSite.serverBaseUrl}</Typography>
                        </Box>
                        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                          <Chip icon={<CircleRoundedIcon sx={{ color: selectedSite.isAvailable ? '#22c55e' : '#ef4444' }} />} label={selectedSite.isAvailable ? 'Доступен' : 'Недоступен'} variant="outlined" color={selectedSite.isAvailable ? 'success' : 'error'} />
                          <Chip label={`Коннектор: ${selectedSite.connectorId || '—'}`} variant="outlined" />
                          <Chip label={`Последняя проверка: ${formatDate(selectedSite.lastSyncUtc)}`} variant="outlined" />
                        </Stack>
                        <Divider />
                        <Button
                          variant="contained"
                          startIcon={<MapRoundedIcon />}
                          onClick={() => setZoneSettingsOpen(true)}
                          disabled={!selectedSite.isAvailable || selectedSite.cameras.length === 0}
                        >
                          Разметить зоны точки
                        </Button>
                        <Box>
                          <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.2 }}>Камеры</Typography>
                          <Stack spacing={1}>
                            {selectedSite.cameras.map((camera) => (
                              <Box key={camera.cameraKey} sx={{ p: 1.4, borderRadius: UI_RADIUS_PX, border: '1px solid rgba(148,163,184,0.16)' }}>
                                <Stack direction="row" justifyContent="space-between" spacing={1.5}>
                                  <Stack direction="row" spacing={1.1} alignItems="center">
                                    <CameraAltRoundedIcon color="primary" />
                                    <Box>
                                      <Typography sx={{ fontWeight: 750 }}>{camera.cameraName}</Typography>
                                      <Typography variant="caption" color="text.secondary">{camera.cameraKey}</Typography>
                                    </Box>
                                  </Stack>
                                  <Chip size="small" label={camera.isAvailable ? 'Доступна' : 'Нет кадра'} color={camera.isAvailable ? 'success' : 'warning'} variant="outlined" />
                                </Stack>
                              </Box>
                            ))}
                          </Stack>
                        </Box>
                      </Stack>
                    ) : (
                      <Alert severity="info">Выбери точку слева, чтобы увидеть камеры и настройки.</Alert>
                    )}
                  </Paper>
                </Box>
              ) : (
                <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', xl: '0.82fr 1.18fr' }, gap: 2.5 }}>
                  <Stack spacing={2}>
                    <Paper sx={{ p: 2, background: 'rgba(15,23,42,0.52)' }}>
                      <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>Выпустить приглашение</Typography>
                      <Stack spacing={1.4}>
                        <TextField label="Название приглашения" value={invitationName} onChange={(event) => setInvitationName(event.target.value)} />
                        <TextField select label="Роль пользователя" value={invitationRoleKey} onChange={(event) => setInvitationRoleKey(event.target.value as 'company-admin' | 'company-operator')}>
                          <MenuItem value="company-admin">Администратор</MenuItem>
                          <MenuItem value="company-operator">Оператор</MenuItem>
                        </TextField>
                        <TextField label="Срок действия" type="datetime-local" value={invitationExpiresAt} onChange={(event) => setInvitationExpiresAt(event.target.value)} InputLabelProps={{ shrink: true }} />
                        <Button startIcon={<KeyRoundedIcon />} variant="contained" onClick={() => void createInvitation()}>Выпустить токен</Button>
                        <Button color="warning" variant="outlined" onClick={() => void revokeActiveInvitations()}>Закрыть активные токены</Button>
                        {lastInvitationToken ? (
                          <Alert severity="success" action={<IconButton size="small" onClick={() => void copyInvitationToken()}><ContentCopyRoundedIcon fontSize="small" /></IconButton>}>
                            <Typography sx={{ fontWeight: 700 }}>Токен показан один раз</Typography>
                            <Typography component="code" sx={{ display: 'block', wordBreak: 'break-all', fontSize: '0.82rem', mt: 0.5 }}>{lastInvitationToken}</Typography>
                          </Alert>
                        ) : null}
                      </Stack>
                    </Paper>
                    <Paper sx={{ p: 2, background: 'rgba(15,23,42,0.52)' }}>
                      <Typography sx={{ fontWeight: 800 }}>Активные приглашения: {activeInvitationsCount}</Typography>
                      <Typography variant="body2" color="text.secondary">Исходный токен хранится только у пользователя после выпуска и не показывается повторно.</Typography>
                    </Paper>
                  </Stack>

                  <Stack spacing={2}>
                    <Paper sx={{ p: 2.2, background: 'rgba(15,23,42,0.52)' }}>
                      <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>Пользователи компании</Typography>
                      <Stack spacing={1}>
                        {accounts.map((account) => (
                          <Box
                            key={account.grantId}
                            onClick={() => void openAccount(account)}
                            sx={{
                              p: 1.4,
                              borderRadius: UI_RADIUS_PX,
                              border: selectedAccount?.accountId === account.accountId ? '1px solid rgba(96,165,250,0.56)' : '1px solid rgba(148,163,184,0.16)',
                              cursor: 'pointer',
                              '&:hover': { borderColor: 'rgba(96,165,250,0.42)' },
                            }}
                          >
                            <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" spacing={1}>
                              <Box>
                                <Typography sx={{ fontWeight: 780 }}>{account.displayName || account.login}</Typography>
                                <Typography variant="body2" color="text.secondary">{account.login} · {ROLE_LABELS[account.roleKey] ?? account.roleKey}</Typography>
                              </Box>
                              <Chip label={ACCOUNT_STATUS_LABELS[account.status] ?? account.status} color={account.status === 'active' ? 'success' : 'warning'} variant="outlined" />
                            </Stack>
                            <Typography variant="caption" color="text.secondary">Доступ до: {formatDate(account.accessExpiresAtUtc)}</Typography>
                          </Box>
                        ))}
                        {accounts.length === 0 ? <Alert severity="info">Пользователей пока нет. Выпусти invitation token и активируй его на странице входа.</Alert> : null}
                      </Stack>
                    </Paper>

                    <Paper sx={{ p: 2.2, background: 'rgba(15,23,42,0.52)' }}>
                      <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>Приглашения</Typography>
                      <Stack spacing={1}>
                        {invitations.map((invitation) => (
                          <Box key={invitation.id} sx={{ p: 1.4, borderRadius: UI_RADIUS_PX, border: '1px solid rgba(148,163,184,0.16)' }}>
                            <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" spacing={1}>
                              <Box>
                                <Typography sx={{ fontWeight: 780 }}>{invitation.name}</Typography>
                                <Typography variant="body2" color="text.secondary">{ROLE_LABELS[invitation.roleKey] ?? invitation.roleKey} · срок: {formatDate(invitation.expiresAtUtc)}</Typography>
                              </Box>
                              <Chip label={invitation.isActive ? 'Активен' : invitation.usedAtUtc ? 'Использован' : 'Закрыт'} color={invitation.isActive ? 'success' : 'default'} variant="outlined" />
                            </Stack>
                          </Box>
                        ))}
                        {invitations.length === 0 ? <Alert severity="info">Приглашений пока нет.</Alert> : null}
                      </Stack>
                    </Paper>
                  </Stack>
                </Box>
              )}
            </Paper>
          </Box>
        )}
      </Box>

      <Dialog
        open={Boolean(selectedSite && zoneSettingsOpen)}
        onClose={() => setZoneSettingsOpen(false)}
        fullWidth
        maxWidth="xl"
        sx={{
          '& .MuiDialog-paper': {
            minHeight: { md: '84vh' },
            background: 'linear-gradient(145deg, rgba(2,6,23,0.98), rgba(15,23,42,0.96))',
            border: '1px solid rgba(148,163,184,0.18)',
          },
        }}
      >
        <DialogTitle sx={{ px: { xs: 2, md: 3 }, py: 2, display: 'flex', justifyContent: 'space-between', gap: 2 }}>
          <Box>
            <Typography variant="h5" sx={{ fontWeight: 800 }}>Разметка зон точки</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75 }}>
              {selectedSite ? `${selectedSite.siteName}: настройка зон открыта из админской панели.` : 'Настройка зон точки.'}
            </Typography>
          </Box>
          <IconButton onClick={() => setZoneSettingsOpen(false)} aria-label="Закрыть окно настройки зон">
            <CloseRoundedIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent sx={{ px: { xs: 2, md: 3 }, pb: { xs: 2, md: 3 }, pt: 0 }}>
          {selectedSite ? <CentralZoneConfigurator baseUrl={baseUrl} cameras={selectedSiteCameras} accessMode="platform" /> : null}
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(selectedAccount)} onClose={() => setSelectedAccount(null)} fullWidth maxWidth="md">
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', gap: 2, alignItems: 'flex-start' }}>
          <Box>
            <Typography variant="h5" sx={{ fontWeight: 850 }}>
              {selectedAccount?.displayName || selectedAccount?.login || 'Пользователь'}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {selectedAccount ? `${selectedAccount.login} · ${ROLE_LABELS[selectedAccount.roleKey] ?? selectedAccount.roleKey}` : ''}
            </Typography>
          </Box>
          <IconButton onClick={() => setSelectedAccount(null)} aria-label="Закрыть карточку пользователя">
            <CloseRoundedIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent sx={{ display: 'grid', gap: 2.2, pb: 3 }}>
          {selectedAccount ? (
            <>
              <Paper variant="outlined" sx={{ p: 2, display: 'grid', gap: 1.2 }}>
                <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                  <Chip label={ACCOUNT_STATUS_LABELS[selectedAccount.status] ?? selectedAccount.status} color={selectedAccount.status === 'active' ? 'success' : 'warning'} variant="outlined" />
                  <Chip label={`Роль: ${ROLE_LABELS[selectedAccount.roleKey] ?? selectedAccount.roleKey}`} variant="outlined" />
                  <Chip label={`Доступ: ${formatDate(selectedAccount.accessExpiresAtUtc)}`} variant="outlined" />
                </Stack>
                <Divider />
                <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 1.2 }}>
                  <Typography variant="body2" color="text.secondary">Создан: {formatDate(selectedAccount.createdAtUtc)}</Typography>
                  <Typography variant="body2" color="text.secondary">Последний вход: {formatDate(selectedAccount.lastLoginAtUtc)}</Typography>
                  <Typography variant="body2" color="text.secondary">IP входа: {selectedAccount.lastLoginIp || 'Нет данных'}</Typography>
                  <Typography variant="body2" color="text.secondary">Права: {selectedAccount.permissions.join(', ') || 'Нет прав'}</Typography>
                </Box>
              </Paper>

              <Paper variant="outlined" sx={{ p: 2, display: 'grid', gap: 1.5 }}>
                <Typography variant="h6" sx={{ fontWeight: 800 }}>Управление доступом</Typography>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={1}>
                  <Button startIcon={<PlayCircleOutlineRoundedIcon />} variant="outlined" onClick={() => void updateAccountAccess('active')}>Запустить доступ</Button>
                  <Button startIcon={<PauseCircleOutlineRoundedIcon />} color="warning" variant="outlined" onClick={() => void updateAccountAccess('suspended')}>Приостановить</Button>
                  <Button color="error" variant="outlined" onClick={() => void updateAccountAccess('disabled')}>Заблокировать</Button>
                </Stack>
              </Paper>

              <Paper variant="outlined" sx={{ p: 2, display: 'grid', gap: 1.5 }}>
                <Typography variant="h6" sx={{ fontWeight: 800 }}>Пароль</Typography>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.2}>
                  <TextField
                    label="Новый пароль"
                    type="password"
                    value={newAccountPassword}
                    onChange={(event) => setNewAccountPassword(event.target.value)}
                    fullWidth
                  />
                  <Button startIcon={<LockResetRoundedIcon />} variant="contained" onClick={() => void changeAccountPassword()} sx={{ minWidth: { md: 230 } }}>
                    Задать новый пароль
                  </Button>
                </Stack>
              </Paper>
            </>
          ) : null}
        </DialogContent>
      </Dialog>
    </Box>
  );
};

export default AdminPage;

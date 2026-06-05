import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Container, Tab, Tabs, TextField, Typography } from '@mui/material';
import type { FormEvent } from 'react';
import kvlogo from '../icons/kvlogo.svg';
import { UI_RADIUS_PX } from '../theme/designTokens';
import { useAppStore } from '../store';
import type { AuthResponseDto, PlatformAuthResponseDto } from '../types/central';

const LoginPage = () => {
  const [mode, setMode] = useState<'login' | 'invitation' | 'platform'>('login');
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [invitationToken, setInvitationToken] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [serverAddress, setServerAddress] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const navigate = useNavigate();
  const { setAuthenticated, setPlatformAuthenticated, setBaseUrl, baseUrl } = useAppStore();

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const resolvedAddress = (serverAddress.trim() || baseUrl).replace(/\/+$/, '');
    setError(null);
    setIsSubmitting(true);
    try {
      if (mode === 'platform') {
        const response = await fetch(`${resolvedAddress}/api/platform/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ login: login.trim(), password }),
        });
        const data = await response.json() as PlatformAuthResponseDto & { message?: string };
        if (!response.ok || !data.platformSessionToken) {
          throw new Error(data.message || 'Не удалось войти в админскую панель.');
        }
        setBaseUrl(resolvedAddress);
        setPlatformAuthenticated(data.admin.login, data.platformSessionToken);
        navigate('/admin');
        return;
      }

      const endpoint = mode === 'invitation' ? 'activate-invitation' : 'login';
      const payload = mode === 'invitation'
        ? { invitationToken: invitationToken.trim(), login: login.trim(), password, displayName: displayName.trim() }
        : { login: login.trim(), password };
      const response = await fetch(`${resolvedAddress}/api/auth/${endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const data = await response.json() as AuthResponseDto & { message?: string };
      if (!response.ok || !data.sessionToken) {
        throw new Error(data.message || 'Не удалось получить доступ к сервису.');
      }
      setBaseUrl(resolvedAddress);
      setAuthenticated(data.account.login, data.sessionToken);
      navigate('/stores');
    } catch (submitError: any) {
      setError(submitError?.message ?? 'Не удалось выполнить вход.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', background: '#020617', px: 2, py: 4 }}>
      <Container maxWidth="sm">
        <Box component="form" onSubmit={handleSubmit} sx={{ width: '100%', px: { xs: 2, md: 4 }, py: 3.5, display: 'grid', gap: 2 }}>
          <Box sx={{ display: 'grid', placeItems: 'center', gap: 1 }}>
            <Box sx={{ width: 82, height: 82, borderRadius: UI_RADIUS_PX, display: 'grid', placeItems: 'center', background: '#1e293b', border: '1px solid rgba(148,163,184,0.18)', p: 1.5 }}>
              <Box component="img" src={kvlogo} alt="KV Logo" sx={{ width: '100%', height: '100%' }} />
            </Box>
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
              {mode === 'platform' ? 'Администрирование платформы' : 'Доступ к компании'}
            </Typography>
          </Box>

          <Tabs value={mode} onChange={(_, value) => setMode(value)} variant="fullWidth">
            <Tab value="login" label="Вход" />
            <Tab value="invitation" label="Активация приглашения" />
            <Tab value="platform" label="Администратор" />
          </Tabs>

          <TextField label="Адрес CentralServer" value={serverAddress} onChange={(event) => setServerAddress(event.target.value)} placeholder={baseUrl} />
          {mode === 'invitation' ? (
            <>
              <TextField label="Одноразовый токен приглашения" value={invitationToken} onChange={(event) => setInvitationToken(event.target.value)} required />
              <TextField label="Отображаемое имя" value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
            </>
          ) : null}
          <TextField label="Логин" value={login} onChange={(event) => setLogin(event.target.value)} autoComplete="username" required />
          <TextField label="Пароль" type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete={mode === 'invitation' ? 'new-password' : 'current-password'} required />

          {error ? <Alert severity="error">{error}</Alert> : null}
          <Button type="submit" variant="contained" disabled={isSubmitting} sx={{ minHeight: 50, fontWeight: 700 }}>
            {isSubmitting
              ? 'Проверяем доступ...'
              : mode === 'invitation'
                ? 'Активировать приглашение'
                : mode === 'platform'
                  ? 'Войти как администратор'
                  : 'Войти'}
          </Button>
        </Box>
      </Container>
    </Box>
  );
};

export default LoginPage;

import CssBaseline from '@mui/material/CssBaseline';
import { createTheme, ThemeProvider } from '@mui/material/styles';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import AppLayout from './components/AppLayout';
import GlobalFeedback from './components/GlobalFeedback';
import AdminPage from './pages/AdminPage';
import LoginPage from './pages/LoginPage';
import MenuPage from './pages/MenuPage';
import PlaceholderPage from './pages/PlaceholderPage';
import StoresPage from './pages/StoresPage';
import StreamingPage from './pages/StreamingPage';
import { AppStoreProvider, useAppStore } from './store';
import { ELEVATED_BACKGROUND, SURFACE_BACKGROUND, SURFACE_BORDER, SURFACE_SHADOW, UI_RADIUS, UI_RADIUS_PX } from './theme/designTokens';

const DEFAULT_CENTRAL_SERVER = 'http://10.10.69.56:5120';

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#6366F1' },
    secondary: { main: '#F97316' },
    background: { default: '#020617', paper: '#020617' },
    text: { primary: '#E5E7EB', secondary: '#9CA3AF' },
  },
  shape: { borderRadius: UI_RADIUS },
  components: {
    MuiPaper: {
      styleOverrides: {
        root: {
          borderRadius: UI_RADIUS_PX,
          background: SURFACE_BACKGROUND,
          border: SURFACE_BORDER,
          boxShadow: SURFACE_SHADOW,
        },
      },
    },
    MuiCard: { styleOverrides: { root: { borderRadius: UI_RADIUS_PX } } },
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          borderRadius: UI_RADIUS_PX,
          paddingInline: 18,
          minHeight: 42,
          transition: 'all 0.18s ease-out',
          '&:hover': { transform: 'translateY(-1px)', boxShadow: '0 10px 22px rgba(99,102,241,0.22)' },
        },
      },
    },
    MuiOutlinedInput: { styleOverrides: { root: { borderRadius: UI_RADIUS_PX, background: 'rgba(15,23,42,0.52)' } } },
    MuiAlert: { styleOverrides: { root: { borderRadius: UI_RADIUS_PX, alignItems: 'center' } } },
    MuiMenu: { styleOverrides: { paper: { borderRadius: UI_RADIUS_PX, background: ELEVATED_BACKGROUND, border: SURFACE_BORDER } } },
  },
});

const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const { isAuthenticated } = useAppStore();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
};

const PlatformProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const { isPlatformAdminAuthenticated } = useAppStore();
  return isPlatformAdminAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
};

const PublicRoute = ({ children }: { children: React.ReactNode }) => {
  const { isAuthenticated, isPlatformAdminAuthenticated } = useAppStore();
  if (isPlatformAdminAuthenticated) {
    return <Navigate to="/admin" replace />;
  }
  return isAuthenticated ? <Navigate to="/stores" replace /> : <>{children}</>;
};

const RoutedApp = () => {
  return (
    <BrowserRouter>
      <GlobalFeedback />
      <Routes>
        <Route path="/login" element={<PublicRoute><LoginPage /></PublicRoute>} />
        <Route path="/admin" element={<PlatformProtectedRoute><AdminPage /></PlatformProtectedRoute>} />
        <Route path="/stores" element={<ProtectedRoute><AppLayout showSurface={false}><StoresPage /></AppLayout></ProtectedRoute>} />
        <Route path="/menu" element={<ProtectedRoute><AppLayout maxWidth="980px" showSurface={false}><MenuPage /></AppLayout></ProtectedRoute>} />
        <Route path="/streaming" element={<ProtectedRoute><AppLayout showSurface={false}><StreamingPage /></AppLayout></ProtectedRoute>} />
        <Route path="/download-archive" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="Загрузка и просмотр записей" subtitle="Раздел уже присутствует в новом shell, но серверная логика архива будет подключаться следующим этапом." /></AppLayout></ProtectedRoute>} />
        <Route path="/live-archive" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="Онлайн просмотр" subtitle="Визуальный раздел перенесён из первого приложения, а новая central-логика для live archive будет подключаться отдельно." /></AppLayout></ProtectedRoute>} />
        <Route path="/defects" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="Таблица нарушений" subtitle="Структура страницы и навигация сохранены, но central incident pipeline ещё не подключён." /></AppLayout></ProtectedRoute>} />
        <Route path="/statistics" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="Статистика" subtitle="Раздел подготовлен визуально и встроен в общий shell, дальнейшая аналитика будет строиться на новых central incidents." /></AppLayout></ProtectedRoute>} />
        <Route path="/audit" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="Аудит" subtitle="Экран оставлен в новой навигации как визуальная заглушка до подключения central audit trail." /></AppLayout></ProtectedRoute>} />
        <Route path="/players" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="Плееры" subtitle="Раздел сохранён в shell первого приложения, но пока не привязан к новой central-модели медиапросмотра." /></AppLayout></ProtectedRoute>} />
        <Route path="/users" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="Пользователи" subtitle="Страница оставлена как визуальная заглушка до появления central user and role management." /></AppLayout></ProtectedRoute>} />
        <Route path="/schedule" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="График" subtitle="Этот раздел будет подключён позже к новой модели сотрудников и расписаний." /></AppLayout></ProtectedRoute>} />
        <Route path="/faq" element={<ProtectedRoute><AppLayout showSurface={false}><PlaceholderPage title="FAQ" subtitle="Раздел сохранён в интерфейсе как в исходном клиенте, но пока работает как заглушка." /></AppLayout></ProtectedRoute>} />
        <Route path="*" element={<Navigate to="/stores" replace />} />
      </Routes>
    </BrowserRouter>
  );
};

const App = () => (
  <ThemeProvider theme={darkTheme}>
    <CssBaseline />
    <AppStoreProvider defaultBaseUrl={DEFAULT_CENTRAL_SERVER}>
      <RoutedApp />
    </AppStoreProvider>
  </ThemeProvider>
);

export default App;

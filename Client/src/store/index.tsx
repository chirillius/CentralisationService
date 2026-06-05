import React, { createContext, startTransition, useCallback, useContext, useMemo, useRef, useState } from 'react';
import type { StoreDto } from '../types/central';

const STORAGE_KEYS = {
  auth: 'centralClientAuth',
  login: 'centralClientLogin',
  platformAuth: 'centralClientPlatformAuth',
  platformLogin: 'centralClientPlatformLogin',
  baseUrl: 'centralClientBaseUrl',
  selectedStore: 'centralClientSelectedStore',
} as const;

type AlertState = {
  open: boolean;
  title: string;
  message: string;
};

type SnackbarState = {
  open: boolean;
  message: string;
};

type AppStoreValue = {
  isAuthenticated: boolean;
  isPlatformAdminAuthenticated: boolean;
  login: string | null;
  platformLogin: string | null;
  baseUrl: string;
  selectedStore: StoreDto | null;
  sessionToken: string | null;
  platformSessionToken: string | null;
  setAuthenticated: (login: string, sessionToken: string) => void;
  setPlatformAuthenticated: (login: string, sessionToken: string) => void;
  logout: () => void;
  platformLogout: () => void;
  authorizedFetch: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
  platformAuthorizedFetch: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
  setBaseUrl: (value: string) => void;
  setSelectedStore: (store: StoreDto | null) => void;
  alertState: AlertState;
  snackbarState: SnackbarState;
  showAlert: (title: string, message: string) => void;
  closeAlert: () => void;
  showSnackbar: (message: string) => void;
  closeSnackbar: () => void;
};

const AppStoreContext = createContext<AppStoreValue | null>(null);

const HTTP_STATUS_MESSAGES: Record<number, string> = {
  400: 'Запрос заполнен некорректно. Проверьте введённые данные.',
  401: 'Нужно войти в систему заново.',
  403: 'Доступ запрещён. Возможно, компания заблокирована или срок доступа истёк.',
  404: 'Запрошенные данные не найдены.',
  405: 'Это действие сейчас недоступно.',
};

const API_CODE_MESSAGES: Record<string, string> = {
  account_access_expired: 'Доступ пользователя отключён или срок доступа истёк.',
  authentication_required: 'Нужно войти в систему заново.',
  company_access_expired: 'Срок доступа компании истёк.',
  company_disabled: 'Компания заблокирована. Обратитесь к администратору сервиса.',
  company_suspended: 'Доступ компании приостановлен. Обратитесь к администратору сервиса.',
  company_unavailable: 'Компания сейчас недоступна.',
  connector_registration_failed: 'Не удалось привязать Server к компании.',
  invalid_credentials: 'Неверный логин или пароль.',
  invalid_invitation: 'Токен приглашения неверный.',
  invalid_session: 'Сессия недействительна или истекла.',
  invitation_expired: 'Срок действия приглашения истёк.',
  invitation_unavailable: 'Приглашение уже использовано или закрыто.',
  login_exists: 'Пользователь с таким логином уже существует.',
  platform_admin_required: 'Нужен вход администратора платформы.',
  server_address_required: 'Нужно указать адрес сервера точки.',
  site_name_required: 'Нужно указать корректное название точки.',
};

const readApiErrorMessage = async (response: Response) => {
  try {
    const data = await response.clone().json() as { code?: string; message?: string; title?: string };
    if (data.code && API_CODE_MESSAGES[data.code]) {
      return API_CODE_MESSAGES[data.code];
    }
    if (data.message && !/^[A-Za-z0-9_ .,'":/-]+$/.test(data.message)) {
      return data.message;
    }
  } catch {
    // Ignore non-JSON errors and fall back to a stable Russian status message.
  }

  return HTTP_STATUS_MESSAGES[response.status] ?? `Сервер вернул ошибку ${response.status}.`;
};

const readJson = <T,>(key: string): T | null => {
  const raw = localStorage.getItem(key);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as T;
  } catch {
    localStorage.removeItem(key);
    return null;
  }
};

export const AppStoreProvider = ({
  children,
  defaultBaseUrl,
}: {
  children: React.ReactNode;
  defaultBaseUrl: string;
}) => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() =>
    Boolean(localStorage.getItem(STORAGE_KEYS.auth)),
  );
  const [isPlatformAdminAuthenticated, setIsPlatformAdminAuthenticated] = useState<boolean>(() =>
    Boolean(localStorage.getItem(STORAGE_KEYS.platformAuth)),
  );
  const [login, setLogin] = useState<string | null>(() => localStorage.getItem(STORAGE_KEYS.login));
  const [platformLogin, setPlatformLogin] = useState<string | null>(() => localStorage.getItem(STORAGE_KEYS.platformLogin));
  const [sessionToken, setSessionToken] = useState<string | null>(() => localStorage.getItem(STORAGE_KEYS.auth));
  const [platformSessionToken, setPlatformSessionToken] = useState<string | null>(() =>
    localStorage.getItem(STORAGE_KEYS.platformAuth),
  );
  const [baseUrlState, setBaseUrlState] = useState<string>(
    () => localStorage.getItem(STORAGE_KEYS.baseUrl) ?? defaultBaseUrl,
  );
  const [selectedStore, setSelectedStoreState] = useState<StoreDto | null>(() =>
    readJson<StoreDto>(STORAGE_KEYS.selectedStore),
  );
  const [alertState, setAlertState] = useState<AlertState>({
    open: false,
    title: '',
    message: '',
  });
  const [snackbarState, setSnackbarState] = useState<SnackbarState>({
    open: false,
    message: '',
  });
  const lastAlertRef = useRef<{ key: string; shownAt: number } | null>(null);

  const showAlert = useCallback((title: string, message: string) => {
    const key = `${title}:${message}`;
    const now = Date.now();
    const lastAlert = lastAlertRef.current;
    if (lastAlert?.key === key && now - lastAlert.shownAt < 3500) {
      return;
    }

    lastAlertRef.current = { key, shownAt: now };
    setAlertState({ open: true, title, message });
  }, []);

  const closeAlert = useCallback(() => {
    setAlertState({ open: false, title: '', message: '' });
  }, []);

  const showSnackbar = useCallback((message: string) => {
    setSnackbarState({ open: true, message });
  }, []);

  const closeSnackbar = useCallback(() => {
    setSnackbarState({ open: false, message: '' });
  }, []);

  const authorizedFetch = useCallback(async (input: RequestInfo | URL, init?: RequestInit) => {
    const headers = new Headers(init?.headers);
    if (sessionToken) {
      headers.set('Authorization', `Bearer ${sessionToken}`);
    }
    const response = await fetch(input, { ...init, headers });
    if (response.status >= 400 && response.status <= 405) {
      showAlert('Доступ к сервису', await readApiErrorMessage(response));
    }
    return response;
  }, [sessionToken, showAlert]);

  const platformAuthorizedFetch = useCallback(async (input: RequestInfo | URL, init?: RequestInit) => {
    const headers = new Headers(init?.headers);
    if (platformSessionToken) {
      headers.set('Authorization', `Bearer ${platformSessionToken}`);
    }
    const response = await fetch(input, { ...init, headers });
    if (response.status >= 400 && response.status <= 405) {
      showAlert('Доступ к сервису', await readApiErrorMessage(response));
    }
    return response;
  }, [platformSessionToken, showAlert]);

  const value = useMemo<AppStoreValue>(
    () => ({
      isAuthenticated,
      isPlatformAdminAuthenticated,
      login,
      platformLogin,
      sessionToken,
      platformSessionToken,
      baseUrl: baseUrlState,
      selectedStore,
      setAuthenticated: (nextLogin, nextSessionToken) => {
        localStorage.setItem(STORAGE_KEYS.auth, nextSessionToken);
        localStorage.setItem(STORAGE_KEYS.login, nextLogin);
        startTransition(() => {
          setLogin(nextLogin);
          setSessionToken(nextSessionToken);
          setIsAuthenticated(true);
        });
      },
      setPlatformAuthenticated: (nextLogin, nextSessionToken) => {
        localStorage.setItem(STORAGE_KEYS.platformAuth, nextSessionToken);
        localStorage.setItem(STORAGE_KEYS.platformLogin, nextLogin);
        startTransition(() => {
          setPlatformLogin(nextLogin);
          setPlatformSessionToken(nextSessionToken);
          setIsPlatformAdminAuthenticated(true);
        });
      },
      logout: () => {
        localStorage.removeItem(STORAGE_KEYS.auth);
        localStorage.removeItem(STORAGE_KEYS.login);
        startTransition(() => {
          setLogin(null);
          setSessionToken(null);
          setIsAuthenticated(false);
        });
      },
      platformLogout: () => {
        localStorage.removeItem(STORAGE_KEYS.platformAuth);
        localStorage.removeItem(STORAGE_KEYS.platformLogin);
        startTransition(() => {
          setPlatformLogin(null);
          setPlatformSessionToken(null);
          setIsPlatformAdminAuthenticated(false);
        });
      },
      authorizedFetch,
      platformAuthorizedFetch,
      setBaseUrl: (value) => {
        localStorage.setItem(STORAGE_KEYS.baseUrl, value);
        setBaseUrlState(value);
      },
      setSelectedStore: (store) => {
        if (store) {
          localStorage.setItem(STORAGE_KEYS.selectedStore, JSON.stringify(store));
        } else {
          localStorage.removeItem(STORAGE_KEYS.selectedStore);
        }
        setSelectedStoreState(store);
      },
      alertState,
      snackbarState,
      showAlert,
      closeAlert,
      showSnackbar,
      closeSnackbar,
    }),
    [
      alertState,
      authorizedFetch,
      baseUrlState,
      closeAlert,
      closeSnackbar,
      isAuthenticated,
      isPlatformAdminAuthenticated,
      login,
      platformAuthorizedFetch,
      platformLogin,
      platformSessionToken,
      selectedStore,
      sessionToken,
      showAlert,
      showSnackbar,
      snackbarState,
    ],
  );

  return <AppStoreContext.Provider value={value}>{children}</AppStoreContext.Provider>;
};

export const useAppStore = () => {
  const context = useContext(AppStoreContext);
  if (!context) {
    throw new Error('useAppStore must be used within AppStoreProvider');
  }

  return context;
};

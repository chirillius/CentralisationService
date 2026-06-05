import AddBusinessRoundedIcon from '@mui/icons-material/AddBusinessRounded';
import AdminPanelSettingsRoundedIcon from '@mui/icons-material/AdminPanelSettingsRounded';
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded';
import CalendarMonthRoundedIcon from '@mui/icons-material/CalendarMonthRounded';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import DeleteIcon from '@mui/icons-material/Delete';
import GroupRoundedIcon from '@mui/icons-material/GroupRounded';
import RadioButtonCheckedRoundedIcon from '@mui/icons-material/RadioButtonCheckedRounded';
import StorefrontIcon from '@mui/icons-material/Storefront';
import { Alert, Box, Button, Chip, CircularProgress, Dialog, DialogContent, DialogTitle, IconButton, Paper, Typography } from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import CentralZoneConfigurator from '../components/stores/CentralZoneConfigurator';
import { useAppStore } from '../store';
import { PAGE_CONTENT_MAX_WIDTH, UI_RADIUS_PX } from '../theme/designTokens';
import type { CameraDto, StoreDto } from '../types/central';

const getStoreKey = (store: StoreDto) => `${store.siteKey}::${store.serverBaseUrl}`;

const StoresPage = () => {
  const navigate = useNavigate();
  const { baseUrl, selectedStore, setSelectedStore, showAlert, showSnackbar, authorizedFetch } = useAppStore();
  const [stores, setStores] = useState<StoreDto[]>([]);
  const [cameras, setCameras] = useState<Record<string, CameraDto[]>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeStoreKey, setActiveStoreKey] = useState<string | null>(null);
  const [isActivating, setIsActivating] = useState<string | null>(null);
  const [zoneSettingsOpen, setZoneSettingsOpen] = useState(false);

  const loadStores = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await authorizedFetch(`${baseUrl}/api/stores`);
      if (!response.ok) {
        throw new Error(`Центральный сервер вернул ошибку ${response.status}.`);
      }
      const data = (await response.json()) as StoreDto[];
      setStores(data);
    } catch (loadError: any) {
      const message = loadError?.message ?? 'Не удалось загрузить список магазинов';
      setError(message);
      showAlert('Ошибка', message);
    } finally {
      setIsLoading(false);
    }
  }, [authorizedFetch, baseUrl, showAlert]);

  const loadCameras = useCallback(async (siteKey: string) => {
    const response = await authorizedFetch(`${baseUrl}/api/cameras?siteKey=${encodeURIComponent(siteKey)}`);
    if (!response.ok) {
      throw new Error(`Центральный сервер вернул ошибку ${response.status}.`);
    }
    const data = (await response.json()) as CameraDto[];
    setCameras((current) => ({ ...current, [siteKey]: data }));
  }, [authorizedFetch, baseUrl]);

  useEffect(() => {
    void loadStores();
  }, [loadStores]);

  useEffect(() => {
    if (selectedStore) {
      setActiveStoreKey(getStoreKey(selectedStore));
    }
  }, [selectedStore]);

  useEffect(() => {
    const currentStore = stores.find((store) => getStoreKey(store) === activeStoreKey);
    if (currentStore && !cameras[currentStore.siteKey]) {
      void loadCameras(currentStore.siteKey).catch(() => undefined);
    }
  }, [activeStoreKey, cameras, loadCameras, stores]);

  const currentStore = useMemo(
    () => stores.find((store) => getStoreKey(store) === activeStoreKey) ?? null,
    [activeStoreKey, stores],
  );

  const currentStoreCameras = currentStore ? cameras[currentStore.siteKey] ?? [] : [];

  const activateStore = async (store: StoreDto) => {
    const storeKey = getStoreKey(store);
    setIsActivating(storeKey);
    try {
      if (!store.isAvailable) {
        showAlert('Ошибка', 'Server магазина сейчас недоступен.');
        return;
      }

      setSelectedStore(store);
      setActiveStoreKey(storeKey);
      if (!cameras[store.siteKey]) {
        await loadCameras(store.siteKey);
      }
      showSnackbar(`Магазин ${store.siteName} выбран в качестве текущего.`);
      navigate('/menu');
    } catch {
      showAlert('Ошибка', 'Не удалось активировать магазин.');
    } finally {
      setIsActivating(null);
    }
  };

  return (
    <Box sx={{ px: { xs: 0, md: 0.5 }, py: 0 }}>
      <Box sx={{ maxWidth: PAGE_CONTENT_MAX_WIDTH, mx: 'auto' }}>
        {error ? <Alert severity="error" sx={{ mb: 2.5 }}>{error}</Alert> : null}

        {isLoading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 10 }}>
            <CircularProgress />
          </Box>
        ) : stores.length > 0 ? (
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1.35fr 0.85fr' }, gap: 2.5, alignItems: 'start' }}>
            <Box sx={{ display: 'grid', gap: 1.5 }}>
              {stores.map((store) => {
                const storeKey = getStoreKey(store);
                const isSelected = activeStoreKey === storeKey;
                const isCurrent = selectedStore?.siteKey === store.siteKey;
                const isUnavailable = !store.isAvailable;
                return (
                  <Box
                    key={storeKey}
                    onClick={() => {
                      if (!isUnavailable) {
                        setActiveStoreKey(storeKey);
                        setZoneSettingsOpen(false);
                      }
                    }}
                    onDoubleClick={() => {
                      if (!isUnavailable) {
                        void activateStore(store);
                      }
                    }}
                    sx={{
                      userSelect: 'none',
                      cursor: isUnavailable ? 'not-allowed' : 'pointer',
                      opacity: isUnavailable ? 0.46 : 1,
                      filter: isUnavailable ? 'saturate(0.45)' : 'none',
                      borderRadius: '14px',
                      border: isSelected ? '1px solid rgba(96,165,250,0.46)' : '1px solid rgba(148,163,184,0.16)',
                      background: isSelected
                        ? 'linear-gradient(135deg, rgba(30,41,59,0.96), rgba(37,99,235,0.18))'
                        : 'linear-gradient(135deg, rgba(15,23,42,0.82), rgba(30,41,59,0.58))',
                      boxShadow: isSelected ? '0 16px 42px rgba(15,23,42,0.5)' : '0 10px 30px rgba(15,23,42,0.22)',
                      transition: 'transform 0.18s ease-out, box-shadow 0.18s ease-out, border-color 0.18s ease-out, background 0.18s ease-out, opacity 0.18s ease-out, filter 0.18s ease-out',
                      '&:hover': {
                        transform: isUnavailable ? 'none' : 'translateY(-2px)',
                        borderColor: isUnavailable ? (isSelected ? 'rgba(96,165,250,0.46)' : 'rgba(148,163,184,0.16)') : 'rgba(96,165,250,0.38)',
                        boxShadow: isUnavailable ? (isSelected ? '0 16px 42px rgba(15,23,42,0.5)' : '0 10px 30px rgba(15,23,42,0.22)') : '0 18px 44px rgba(15,23,42,0.44)',
                      },
                    }}
                  >
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.6, px: { xs: 2, md: 2.4 }, py: { xs: 1.7, md: 1.9 } }}>
                      <Box sx={{ width: 44, height: 44, borderRadius: '14px', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, color: isSelected ? 'primary.light' : 'rgba(226,232,240,0.92)', background: isSelected ? 'rgba(59,130,246,0.16)' : 'rgba(148,163,184,0.1)', position: 'relative' }}>
                        <StorefrontIcon sx={{ fontSize: 24 }} />
                      </Box>

                      <Box sx={{ flex: 1, minWidth: 0 }}>
                        <Typography sx={{ fontWeight: 800, fontSize: { xs: '1rem', md: '1.04rem' }, lineHeight: 1.2, color: '#f8fafc', mb: 0.55 }}>
                          {store.siteName}
                        </Typography>
                        <Typography sx={{ color: 'rgba(203,213,225,0.74)', fontSize: '0.84rem', lineHeight: 1.45, wordBreak: 'break-word' }}>
                          {store.serverBaseUrl}
                        </Typography>
                        <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap', mt: 1.2 }}>
                          <Chip size="small" label={`Коннектор: ${store.connectorId || '—'}`} sx={{ borderRadius: UI_RADIUS_PX, bgcolor: 'rgba(148,163,184,0.08)', color: 'rgba(226,232,240,0.88)' }} />
                          <Chip size="small" label={store.isAvailable ? 'Доступен' : 'Недоступен'} color={store.isAvailable ? 'success' : 'warning'} variant="outlined" />
                          <Chip size="small" label={`Камер: ${store.cameraCount}`} sx={{ borderRadius: UI_RADIUS_PX, bgcolor: 'rgba(99,102,241,0.1)', color: '#c4b5fd' }} />
                        </Box>
                      </Box>

                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.8, flexShrink: 0 }}>
                        {isCurrent ? <CheckCircleIcon color="success" fontSize="small" /> : <RadioButtonCheckedRoundedIcon sx={{ color: isSelected ? '#93c5fd' : 'rgba(148,163,184,0.52)' }} />}
                        <ArrowForwardRoundedIcon sx={{ color: 'rgba(148,163,184,0.72)' }} />
                      </Box>
                    </Box>
                  </Box>
                );
              })}
            </Box>

            <Box sx={{ display: 'grid', gap: 2 }}>
              <Paper sx={{ p: { xs: 2.2, md: 2.5 }, borderRadius: UI_RADIUS_PX, border: '1px solid rgba(148,163,184,0.16)', background: 'linear-gradient(145deg, rgba(15,23,42,0.94), rgba(30,41,59,0.75))', boxShadow: '0 18px 42px rgba(15,23,42,0.32)' }}>
                {currentStore ? (
                  <Box sx={{ display: 'grid', gap: 1.5 }}>
                    <Box>
                      <Typography variant="overline" sx={{ color: 'rgba(148,163,184,0.78)', letterSpacing: '0.12em' }}>
                        Выбранный магазин
                      </Typography>
                      <Typography variant="h5" sx={{ fontWeight: 800, mt: 0.2 }}>
                        {currentStore.siteName}
                      </Typography>
                      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.9, lineHeight: 1.6 }}>
                        Central client получает магазины и камеры только через `CentralServer`, поэтому именно здесь будет сосредоточена настройка разметки зон и дальнейшего detection pipeline.
                      </Typography>
                    </Box>

                    <Box sx={{ display: 'grid', gap: 1 }}>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1.25, alignItems: 'center' }}>
                        <Typography variant="subtitle2" color="text.secondary">ServerBaseUrl</Typography>
                        <Typography sx={{ fontWeight: 700, textAlign: 'right', wordBreak: 'break-word' }}>{currentStore.serverBaseUrl}</Typography>
                      </Box>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1.25, alignItems: 'center' }}>
                        <Typography variant="subtitle2" color="text.secondary">Cleaning day</Typography>
                        <Chip icon={<CalendarMonthRoundedIcon />} label={String(currentStore.cleaningDay)} size="small" color="primary" variant="outlined" />
                      </Box>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1.25, alignItems: 'center' }}>
                        <Typography variant="subtitle2" color="text.secondary">Количество камер</Typography>
                        <Chip icon={<GroupRoundedIcon />} label={String(currentStore.cameraCount)} size="small" color="info" variant="outlined" />
                      </Box>
                    </Box>

                    <Box sx={{ display: 'grid', gridTemplateColumns: '1fr', gap: 1 }}>
                      <Button variant="contained" onClick={() => void activateStore(currentStore)} disabled={isActivating === getStoreKey(currentStore)}>
                        {isActivating === getStoreKey(currentStore) ? 'Подключаем...' : 'Сделать активным'}
                      </Button>
                      <Button variant="outlined" startIcon={<AdminPanelSettingsRoundedIcon />} onClick={() => setZoneSettingsOpen(true)} disabled={!currentStore.isAvailable}>
                        Открыть настройку зон
                      </Button>
                      <Button variant="outlined" startIcon={<AddBusinessRoundedIcon />} onClick={() => showAlert('Информация', 'UI добавления магазина будет подключаться отдельным этапом, когда central catalog перейдёт с appsettings на API+persistence.')}>
                        Добавить магазин
                      </Button>
                      <Button variant="outlined" color="error" startIcon={<DeleteIcon />} onClick={() => showAlert('Информация', 'Удаление магазина пока не подключено к CentralServer API, раздел оставлен визуально.')}>
                        Удалить магазин
                      </Button>
                    </Box>
                  </Box>
                ) : (
                  <Alert severity="info">Выбери магазин слева, чтобы открыть его central settings.</Alert>
                )}
              </Paper>
            </Box>
          </Box>
        ) : (
          <Alert severity="warning">На CentralServer пока нет доступных магазинов.</Alert>
        )}
      </Box>

      <Dialog
        open={Boolean(currentStore && zoneSettingsOpen)}
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
        <DialogTitle
          sx={{
            px: { xs: 2, md: 3 },
            py: 2,
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'space-between',
            gap: 2,
          }}
        >
          <Box>
            <Typography variant="h5" sx={{ fontWeight: 800 }}>
              Настройка зон
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75, maxWidth: 820 }}>
              {currentStore
                ? `Разметка для магазина ${currentStore.siteName} открыта в отдельном окне поверх списка магазинов. Используется один последний кадр камеры с ручным обновлением.`
                : 'Разметка зон открывается поверх текущей страницы, как в исходном клиенте.'}
            </Typography>
          </Box>
          <IconButton onClick={() => setZoneSettingsOpen(false)} aria-label="Закрыть окно настройки зон">
            <CloseRoundedIcon />
          </IconButton>
        </DialogTitle>

        <DialogContent sx={{ px: { xs: 2, md: 3 }, pb: { xs: 2, md: 3 }, pt: 0 }}>
          {currentStore ? (
            <CentralZoneConfigurator baseUrl={baseUrl} cameras={currentStoreCameras} />
          ) : null}
        </DialogContent>
      </Dialog>
    </Box>
  );
};

export default StoresPage;

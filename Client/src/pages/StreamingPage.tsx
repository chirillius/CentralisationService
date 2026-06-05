import { Alert, Box, Button, Card, CardContent, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import PageHero from '../components/PageHero';
import { useAppStore } from '../store';
import type { CameraDto, StoreDto } from '../types/central';

const StreamingPage = () => {
  const { baseUrl, selectedStore, showAlert, authorizedFetch } = useAppStore();
  const [stores, setStores] = useState<StoreDto[]>([]);
  const [cameras, setCameras] = useState<CameraDto[]>([]);
  const [selectedSiteKey, setSelectedSiteKey] = useState(selectedStore?.siteKey ?? '');
  const [selectedCameraKey, setSelectedCameraKey] = useState('');
  const [previewNonce, setPreviewNonce] = useState(() => Date.now());
  const [previewUrl, setPreviewUrl] = useState('');

  const selectedCamera = useMemo(
    () => cameras.find((camera) => camera.key === selectedCameraKey) ?? null,
    [cameras, selectedCameraKey],
  );

  useEffect(() => {
    let cancelled = false;
    const loadStores = async () => {
      try {
        const response = await authorizedFetch(`${baseUrl}/api/stores`);
        if (!response.ok) {
          throw new Error(`Центральный сервер вернул ошибку ${response.status}.`);
        }
        const data = (await response.json()) as StoreDto[];
        if (!cancelled) {
          setStores(data);
          if (!selectedSiteKey) {
            setSelectedSiteKey(selectedStore?.siteKey ?? data[0]?.siteKey ?? '');
          }
        }
      } catch (error: any) {
        if (!cancelled) {
          showAlert('Ошибка', error?.message ?? 'Не удалось загрузить магазины для стриминга.');
        }
      }
    };

    void loadStores();
    return () => {
      cancelled = true;
    };
  }, [authorizedFetch, baseUrl, selectedSiteKey, selectedStore?.siteKey, showAlert]);

  useEffect(() => {
    if (!selectedSiteKey) {
      setCameras([]);
      setSelectedCameraKey('');
      return;
    }
    let cancelled = false;
    const loadCameras = async () => {
      try {
        const response = await authorizedFetch(`${baseUrl}/api/cameras?siteKey=${encodeURIComponent(selectedSiteKey)}`);
        if (!response.ok) {
          throw new Error(`Центральный сервер вернул ошибку ${response.status}.`);
        }
        const data = (await response.json()) as CameraDto[];
        if (!cancelled) {
          setCameras(data);
          setSelectedCameraKey((current) => current && data.some((camera) => camera.key === current) ? current : data[0]?.key ?? '');
        }
      } catch (error: any) {
        if (!cancelled) {
          showAlert('Ошибка', error?.message ?? 'Не удалось загрузить камеры для стриминга.');
        }
      }
    };

    void loadCameras();
    return () => {
      cancelled = true;
    };
  }, [authorizedFetch, baseUrl, selectedSiteKey, showAlert]);

  useEffect(() => {
    if (!selectedCameraKey) {
      return;
    }
    const timer = window.setInterval(() => setPreviewNonce(Date.now()), 3000);
    return () => window.clearInterval(timer);
  }, [selectedCameraKey]);

  useEffect(() => {
    if (!selectedCameraKey) {
      setPreviewUrl('');
      return;
    }
    let cancelled = false;
    let objectUrl = '';
    const loadFrame = async () => {
      try {
        const response = await authorizedFetch(`${baseUrl}/api/cameras/${encodeURIComponent(selectedCameraKey)}/frame?ts=${previewNonce}`);
        if (!response.ok) {
          throw new Error(`Не удалось получить кадр. Код ошибки: ${response.status}.`);
        }
        objectUrl = URL.createObjectURL(await response.blob());
        if (!cancelled) {
          setPreviewUrl((current) => {
            if (current) {
              URL.revokeObjectURL(current);
            }
            return objectUrl;
          });
        }
      } catch (error: any) {
        if (!cancelled) {
          showAlert('Ошибка', error?.message ?? 'Не удалось получить кадр.');
        }
      }
    };
    void loadFrame();
    return () => {
      cancelled = true;
      if (objectUrl && objectUrl !== previewUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [authorizedFetch, baseUrl, previewNonce, selectedCameraKey]);

  return (
    <Box sx={{ px: { xs: 2, md: 3 }, pb: 4 }}>
      <PageHero
        title="Потоковое видео"
        subtitle="В этом разделе уже работает новая central-логика: выбор магазина, выбор камеры и получение текущего кадра через CentralServer."
        storeName={selectedStore?.siteName ?? null}
      />

      <Stack spacing={2.5}>
        <Card>
          <CardContent>
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
              <TextField select label="Магазин" value={selectedSiteKey} onChange={(event) => setSelectedSiteKey(event.target.value)} fullWidth>
                {stores.map((store) => (
                  <MenuItem key={store.siteKey} value={store.siteKey}>
                    {store.siteName}
                  </MenuItem>
                ))}
              </TextField>
              <TextField select label="Камера" value={selectedCameraKey} onChange={(event) => setSelectedCameraKey(event.target.value)} fullWidth>
                {cameras.map((camera) => (
                  <MenuItem key={camera.key} value={camera.key}>
                    {camera.name}
                  </MenuItem>
                ))}
              </TextField>
              <Button variant="outlined" onClick={() => setPreviewNonce(Date.now())}>Обновить кадр</Button>
            </Stack>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            {selectedCamera ? (
              <Stack spacing={2}>
                <Box
                  component="img"
                  src={previewUrl}
                  alt={selectedCamera.name}
                  sx={{ width: '100%', aspectRatio: '16 / 9', objectFit: 'cover', borderRadius: '18px', border: '1px solid rgba(148,163,184,0.14)', background: '#020617' }}
                />
                <Box>
                  <Typography variant="h6" sx={{ fontWeight: 800 }}>{selectedCamera.name}</Typography>
                  <Typography variant="body2" color="text.secondary">Магазин: {selectedCamera.siteName}</Typography>
                  <Typography variant="body2" color="text.secondary">Сервер: {selectedCamera.serverBaseUrl}</Typography>
                </Box>
              </Stack>
            ) : (
              <Alert severity="warning">Выберите камеру, чтобы получить кадр через CentralServer.</Alert>
            )}
          </CardContent>
        </Card>
      </Stack>
    </Box>
  );
};

export default StreamingPage;

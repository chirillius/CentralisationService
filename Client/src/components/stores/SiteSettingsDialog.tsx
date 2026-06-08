import AddRoundedIcon from '@mui/icons-material/AddRounded';
import CloseRoundedIcon from '@mui/icons-material/CloseRounded';
import DeleteRoundedIcon from '@mui/icons-material/DeleteRounded';
import EditRoundedIcon from '@mui/icons-material/EditRounded';
import VideocamRoundedIcon from '@mui/icons-material/VideocamRounded';
import { Alert, Box, Button, Chip, Dialog, DialogContent, DialogTitle, IconButton, Paper, Stack, Tab, Tabs, TextField, Typography } from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import { UI_RADIUS_PX } from '../../theme/designTokens';
import type { CameraDto } from '../../types/central';
import CentralZoneConfigurator from './CentralZoneConfigurator';

type CameraFormState = {
  key: string;
  name: string;
  host: string;
  highQualityPath: string;
  lowQualityPath: string;
};

type SiteSettingsDialogProps = {
  open: boolean;
  title: string;
  subtitle: string;
  baseUrl: string;
  cameras: CameraDto[];
  cameraEndpoint: string;
  canManage: boolean;
  request: typeof fetch;
  onClose: () => void;
  onChanged: () => Promise<void> | void;
};

const emptyForm: CameraFormState = {
  key: '',
  name: '',
  host: '',
  highQualityPath: '/Streaming/Channels/101',
  lowQualityPath: '/Streaming/Channels/102',
};

const SiteSettingsDialog = ({
  open,
  title,
  subtitle,
  baseUrl,
  cameras,
  cameraEndpoint,
  canManage,
  request,
  onClose,
  onChanged,
}: SiteSettingsDialogProps) => {
  const [tab, setTab] = useState<'cameras' | 'zones'>('cameras');
  const [editingCameraKey, setEditingCameraKey] = useState<string | null>(null);
  const [form, setForm] = useState<CameraFormState>(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const editingCamera = useMemo(
    () => cameras.find((camera) => camera.sourceCameraKey === editingCameraKey) ?? null,
    [cameras, editingCameraKey],
  );

  useEffect(() => {
    if (!open) {
      setTab('cameras');
      setEditingCameraKey(null);
      setForm(emptyForm);
      setError(null);
    }
  }, [open]);

  useEffect(() => {
    if (!editingCamera) {
      return;
    }

    setForm({
      key: editingCamera.sourceCameraKey,
      name: editingCamera.name,
      host: editingCamera.host,
      highQualityPath: editingCamera.highQualityPath || '/Streaming/Channels/101',
      lowQualityPath: editingCamera.lowQualityPath || '/Streaming/Channels/102',
    });
  }, [editingCamera]);

  const resetForm = () => {
    setEditingCameraKey(null);
    setForm(emptyForm);
    setError(null);
  };

  const saveCamera = async () => {
    if (!canManage) {
      return;
    }

    if (!form.name.trim() || !form.host.trim()) {
      setError('Укажи название камеры и IP/host без логина и пароля.');
      return;
    }

    if (form.host.includes('@') || form.host.toLowerCase().startsWith('rtsp://')) {
      setError('В поле адреса камеры должен быть только IP или host, без RTSP, логина и пароля.');
      return;
    }

    setIsSaving(true);
    setError(null);
    try {
      const url = editingCameraKey
        ? `${baseUrl}${cameraEndpoint}/${encodeURIComponent(editingCameraKey)}`
        : `${baseUrl}${cameraEndpoint}`;
      const response = await request(url, {
        method: editingCameraKey ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          key: form.key.trim(),
          name: form.name.trim(),
          host: form.host.trim(),
          highQualityPath: form.highQualityPath.trim() || '/Streaming/Channels/101',
          lowQualityPath: form.lowQualityPath.trim() || '/Streaming/Channels/102',
        }),
      });

      if (!response.ok) {
        setError(`Не удалось сохранить камеру. Код ошибки: ${response.status}.`);
        return;
      }

      resetForm();
      await onChanged();
    } finally {
      setIsSaving(false);
    }
  };

  const deleteCamera = async (camera: CameraDto) => {
    if (!canManage || !window.confirm(`Удалить камеру "${camera.name}"?`)) {
      return;
    }

    const response = await request(`${baseUrl}${cameraEndpoint}/${encodeURIComponent(camera.sourceCameraKey)}`, {
      method: 'DELETE',
    });
    if (!response.ok) {
      setError(`Не удалось удалить камеру. Код ошибки: ${response.status}.`);
      return;
    }

    if (editingCameraKey === camera.sourceCameraKey) {
      resetForm();
    }
    await onChanged();
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="xl" sx={{ '& .MuiDialog-paper': { minHeight: { md: '84vh' }, background: 'linear-gradient(145deg, rgba(2,6,23,0.98), rgba(15,23,42,0.96))', border: '1px solid rgba(148,163,184,0.18)' } }}>
      <DialogTitle sx={{ px: { xs: 2, md: 3 }, py: 2, display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 2 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800 }}>{title}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75, maxWidth: 860 }}>{subtitle}</Typography>
        </Box>
        <IconButton onClick={onClose} aria-label="Закрыть окно настройки точки">
          <CloseRoundedIcon />
        </IconButton>
      </DialogTitle>

      <DialogContent sx={{ px: { xs: 2, md: 3 }, pb: { xs: 2, md: 3 }, pt: 0 }}>
        <Tabs value={tab} onChange={(_, value) => setTab(value)} sx={{ mb: 2 }}>
          <Tab value="cameras" label="Камеры" />
          <Tab value="zones" label="Разметка зон" />
        </Tabs>

        {tab === 'cameras' ? (
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: canManage ? '1fr 0.92fr' : '1fr' }, gap: 2 }}>
            <Stack spacing={1.2}>
              {cameras.map((camera) => (
                <Paper key={camera.sourceCameraKey} sx={{ p: 1.6, borderRadius: UI_RADIUS_PX, background: 'rgba(15,23,42,0.62)' }}>
                  <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.2} alignItems={{ xs: 'flex-start', md: 'center' }} justifyContent="space-between">
                    <Stack direction="row" spacing={1.2} alignItems="center">
                      <Box sx={{ width: 38, height: 38, borderRadius: '12px', display: 'grid', placeItems: 'center', color: 'primary.light', background: 'rgba(99,102,241,0.16)' }}>
                        <VideocamRoundedIcon />
                      </Box>
                      <Box>
                        <Typography sx={{ fontWeight: 800 }}>{camera.name}</Typography>
                        <Typography variant="body2" color="text.secondary">{camera.host || 'host не указан'} · {camera.sourceCameraKey}</Typography>
                        <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap sx={{ mt: 0.8 }}>
                          <Chip size="small" label={`HQ ${camera.highQualityPath || '/Streaming/Channels/101'}`} variant="outlined" />
                          <Chip size="small" label={`LQ ${camera.lowQualityPath || '/Streaming/Channels/102'}`} variant="outlined" />
                          <Chip size="small" label={camera.isAvailable ? 'Доступна' : 'Нет кадра'} color={camera.isAvailable ? 'success' : 'warning'} variant="outlined" />
                        </Stack>
                      </Box>
                    </Stack>
                    {canManage ? (
                      <Stack direction="row" spacing={1}>
                        <Button size="small" startIcon={<EditRoundedIcon />} variant="outlined" onClick={() => setEditingCameraKey(camera.sourceCameraKey)}>Изменить</Button>
                        <Button size="small" startIcon={<DeleteRoundedIcon />} color="error" variant="outlined" onClick={() => void deleteCamera(camera)}>Удалить</Button>
                      </Stack>
                    ) : null}
                  </Stack>
                </Paper>
              ))}
              {cameras.length === 0 ? <Alert severity="info">Камеры пока не настроены.</Alert> : null}
            </Stack>

            {canManage ? (
              <Paper sx={{ p: 2, borderRadius: UI_RADIUS_PX, background: 'rgba(15,23,42,0.62)' }}>
                <Typography variant="h6" sx={{ fontWeight: 800, mb: 1.5 }}>
                  {editingCameraKey ? 'Изменить камеру' : 'Добавить камеру'}
                </Typography>
                {error ? <Alert severity="error" sx={{ mb: 1.5 }}>{error}</Alert> : null}
                <Stack spacing={1.4}>
                  <TextField label="Технический ключ" value={form.key} onChange={(event) => setForm((current) => ({ ...current, key: event.target.value }))} placeholder="front" />
                  <TextField label="Название" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} placeholder="Front" />
                  <TextField label="Адрес камеры" value={form.host} onChange={(event) => setForm((current) => ({ ...current, host: event.target.value }))} placeholder="192.168.2.32" helperText="Только IP или host. Логин и пароль хранятся локально на Server." />
                  <TextField label="Поток высокого качества" value={form.highQualityPath} onChange={(event) => setForm((current) => ({ ...current, highQualityPath: event.target.value }))} />
                  <TextField label="Поток низкого качества" value={form.lowQualityPath} onChange={(event) => setForm((current) => ({ ...current, lowQualityPath: event.target.value }))} />
                  <Stack direction="row" spacing={1}>
                    <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={() => void saveCamera()} disabled={isSaving}>
                      {isSaving ? 'Сохраняем...' : 'Сохранить камеру'}
                    </Button>
                    <Button variant="outlined" onClick={resetForm}>Очистить</Button>
                  </Stack>
                </Stack>
              </Paper>
            ) : (
              <Alert severity="info">У оператора доступен только просмотр камер. Изменение камер и зон доступно администратору компании.</Alert>
            )}
          </Box>
        ) : (
          canManage ? <CentralZoneConfigurator baseUrl={baseUrl} cameras={cameras} /> : <Alert severity="info">Разметка зон доступна только администратору компании.</Alert>
        )}
      </DialogContent>
    </Dialog>
  );
};

export default SiteSettingsDialog;

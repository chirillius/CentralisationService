import AddRoundedIcon from '@mui/icons-material/AddRounded';
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded';
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded';
import RemoveRoundedIcon from '@mui/icons-material/RemoveRounded';
import RestartAltRoundedIcon from '@mui/icons-material/RestartAltRounded';
import SaveRoundedIcon from '@mui/icons-material/SaveRounded';
import { Alert, Box, Button, Chip, CircularProgress, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useAppStore } from '../../store';
import type { CameraDto } from '../../types/central';
import type { UpsertZoneRequestDto, ZoneNameCatalogDto, ZonePointDto, ZoneRecordDto } from '../../types/zones';

const CUSTOM_ZONE_LABEL = 'Свой вариант';
const KNOWN_ZONE_TYPE_KEYS: Record<string, string> = {
  Клиентская: 'client-zone',
  Прилавок: 'stall-zone',
  Касса: 'cash-register-zone',
  Дым: 'smoke-zone',
  Телефон: 'phone-zone',
  Бутылки: 'bottles-zone',
  Бейдж: 'badge-zone',
  Стол: 'table-zone',
  Свет: 'light-zone',
  'Мойка полов': 'mopping-zone',
};

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

const CentralZoneConfigurator = ({
  baseUrl,
  cameras,
  accessMode = 'company',
}: {
  baseUrl: string;
  cameras: CameraDto[];
  accessMode?: 'company' | 'platform';
}) => {
  const { showAlert, showSnackbar, authorizedFetch, platformAuthorizedFetch } = useAppStore();
  const request = accessMode === 'platform' ? platformAuthorizedFetch : authorizedFetch;
  const zonesPath = accessMode === 'platform' ? '/api/platform/zones' : '/api/zones';
  const cameraFramePath = accessMode === 'platform' ? '/api/platform/cameras' : '/api/cameras';
  const [zoneCatalog, setZoneCatalog] = useState<ZoneNameCatalogDto | null>(null);
  const [zones, setZones] = useState<ZoneRecordDto[]>([]);
  const [selectedCameraKey, setSelectedCameraKey] = useState('');
  const [selectedZoneId, setSelectedZoneId] = useState<string | null>(null);
  const [selectedZoneName, setSelectedZoneName] = useState('');
  const [customZoneName, setCustomZoneName] = useState('');
  const [draftPoints, setDraftPoints] = useState<ZonePointDto[]>([]);
  const [draggedPointIndex, setDraggedPointIndex] = useState<number | null>(null);
  const [frameUrl, setFrameUrl] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const overlayRef = useRef<SVGSVGElement | null>(null);

  const selectedCamera = useMemo(
    () => cameras.find((camera) => camera.key === selectedCameraKey) ?? null,
    [cameras, selectedCameraKey],
  );
  const customOptionLabel = zoneCatalog?.customOptionLabel ?? CUSTOM_ZONE_LABEL;
  const zoneNameOptions = useMemo(() => {
    const names = zoneCatalog?.names ?? [];
    return zoneCatalog?.allowCustom ? [...names, customOptionLabel] : names;
  }, [customOptionLabel, zoneCatalog]);
  const previewPolygonPoints = useMemo(
    () => draftPoints.map((point) => `${point.x * 100},${point.y * 100}`).join(' '),
    [draftPoints],
  );

  useEffect(() => {
    if (!cameras.length) {
      setSelectedCameraKey('');
      return;
    }
    if (!cameras.some((camera) => camera.key === selectedCameraKey)) {
      setSelectedCameraKey(cameras[0].key);
    }
  }, [cameras, selectedCameraKey]);

  useEffect(() => {
    let cancelled = false;
    const loadCatalog = async () => {
      try {
        const response = await request(`${baseUrl}${zonesPath}/names`);
        if (!response.ok) {
          throw new Error(`Не удалось загрузить каталог зон. Код ошибки: ${response.status}.`);
        }
        const data = (await response.json()) as ZoneNameCatalogDto;
        if (cancelled) {
          return;
        }
        setZoneCatalog(data);
        setSelectedZoneName((current) => current || data.names[0] || data.customOptionLabel);
      } catch (loadError: any) {
        if (!cancelled) {
          setError(loadError?.message ?? 'Не удалось загрузить типы зон');
        }
      }
    };

    void loadCatalog();
    return () => {
      cancelled = true;
    };
  }, [baseUrl, request, zonesPath]);

  const revokeFrame = () => {
    if (frameUrl) {
      URL.revokeObjectURL(frameUrl);
    }
  };

  const loadZones = async (cameraKey: string) => {
    const response = await request(`${baseUrl}${zonesPath}?cameraKey=${encodeURIComponent(cameraKey)}`);
    if (!response.ok) {
      throw new Error(`Не удалось загрузить зоны. Код ошибки: ${response.status}.`);
    }
    return (await response.json()) as ZoneRecordDto[];
  };

  const loadFrame = async (cameraKey: string) => {
    const response = await request(`${baseUrl}${cameraFramePath}/${encodeURIComponent(cameraKey)}/frame?ts=${Date.now()}`);
    if (!response.ok) {
      throw new Error(`Не удалось получить кадр. Код ошибки: ${response.status}.`);
    }
    const blob = await response.blob();
    revokeFrame();
    const nextUrl = URL.createObjectURL(blob);
    setFrameUrl(nextUrl);
  };

  useEffect(() => {
    if (!selectedCameraKey) {
      setZones([]);
      return;
    }
    let cancelled = false;

    const load = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const [loadedZones] = await Promise.all([loadZones(selectedCameraKey), loadFrame(selectedCameraKey)]);
        if (cancelled) {
          return;
        }
        setZones(loadedZones);
        setSelectedZoneId(null);
        setDraftPoints([]);
      } catch (loadError: any) {
        if (!cancelled) {
          setError(loadError?.message ?? 'Не удалось загрузить настройки камеры');
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, [baseUrl, selectedCameraKey]);

  useEffect(
    () => () => {
      revokeFrame();
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );

  const beginNewZone = () => {
    setSelectedZoneId(null);
    setDraftPoints([]);
    setCustomZoneName('');
    setSelectedZoneName(zoneCatalog?.names[0] || customOptionLabel);
  };

  const selectZone = (zone: ZoneRecordDto) => {
    setSelectedZoneId(zone.id);
    setDraftPoints(zone.points);
    const isKnownName = zoneCatalog?.names.includes(zone.zoneName) ?? false;
    setSelectedZoneName(isKnownName ? zone.zoneName : customOptionLabel);
    setCustomZoneName(isKnownName ? zone.customName ?? '' : zone.displayName);
  };

  const getOverlayPoint = (clientX: number, clientY: number): ZonePointDto | null => {
    const overlay = overlayRef.current;
    if (!overlay) {
      return null;
    }

    const bounds = overlay.getBoundingClientRect();
    if (!bounds.width || !bounds.height) {
      return null;
    }

    return {
      x: clamp((clientX - bounds.left) / bounds.width, 0, 1),
      y: clamp((clientY - bounds.top) / bounds.height, 0, 1),
    };
  };

  const handleOverlayClick = (event: React.MouseEvent<SVGSVGElement>) => {
    if (!frameUrl || draggedPointIndex !== null) {
      return;
    }

    const point = getOverlayPoint(event.clientX, event.clientY);
    if (!point) {
      return;
    }

    setDraftPoints((current) => [...current, point]);
  };

  const handlePointPointerDown = (index: number, event: React.PointerEvent<SVGCircleElement>) => {
    event.preventDefault();
    event.stopPropagation();
    overlayRef.current?.setPointerCapture(event.pointerId);
    setDraggedPointIndex(index);
  };

  const handleOverlayPointerMove = (event: React.PointerEvent<SVGSVGElement>) => {
    if (draggedPointIndex === null) {
      return;
    }

    const point = getOverlayPoint(event.clientX, event.clientY);
    if (!point) {
      return;
    }

    setDraftPoints((current) => current.map((item, index) => (index === draggedPointIndex ? point : item)));
  };

  const handleOverlayPointerUp = (event: React.PointerEvent<SVGSVGElement>) => {
    if (overlayRef.current?.hasPointerCapture(event.pointerId)) {
      overlayRef.current.releasePointerCapture(event.pointerId);
    }
    setDraggedPointIndex(null);
  };

  const handleSaveZone = async () => {
    if (!selectedCamera || draftPoints.length < 3) {
      showAlert('Ошибка', 'Для сохранения зоны нужно выбрать камеру и отметить минимум три точки.');
      return;
    }

    const resolvedName = selectedZoneName === customOptionLabel ? customZoneName.trim() : selectedZoneName.trim();
    if (!resolvedName) {
      showAlert('Ошибка', 'Укажите название зоны.');
      return;
    }

    const payload: UpsertZoneRequestDto = {
      siteKey: selectedCamera.siteKey,
      cameraKey: selectedCamera.key,
      zoneTypeKey: KNOWN_ZONE_TYPE_KEYS[selectedZoneName] ?? 'custom-zone',
      zoneName: selectedZoneName,
      customName: selectedZoneName === customOptionLabel ? resolvedName : null,
      points: draftPoints,
    };

    const method = selectedZoneId ? 'PUT' : 'POST';
    const endpoint = selectedZoneId
      ? `${baseUrl}${zonesPath}/${selectedZoneId}`
      : `${baseUrl}${zonesPath}`;

    setIsSaving(true);
    try {
      const response = await request(endpoint, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!response.ok) {
        throw new Error(`Не удалось сохранить зону. Код ошибки: ${response.status}.`);
      }
      const saved = (await response.json()) as ZoneRecordDto;
      const refreshed = await loadZones(selectedCamera.key);
      setZones(refreshed);
      selectZone(saved);
      showSnackbar('Зона сохранена на CentralServer.');
    } catch (saveError: any) {
      showAlert('Ошибка', saveError?.message ?? 'Не удалось сохранить зону.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleDeleteZone = async () => {
    if (!selectedZoneId || !selectedCamera) {
      return;
    }
    setIsSaving(true);
    try {
      const response = await request(`${baseUrl}${zonesPath}/${selectedZoneId}`, { method: 'DELETE' });
      if (!response.ok) {
        throw new Error(`Не удалось удалить зону. Код ошибки: ${response.status}.`);
      }
      const refreshed = await loadZones(selectedCamera.key);
      setZones(refreshed);
      beginNewZone();
      showSnackbar('Зона удалена.');
    } catch (deleteError: any) {
      showAlert('Ошибка', deleteError?.message ?? 'Не удалось удалить зону.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: { xs: 1.5, md: 1.75 }, borderRadius: '14px', display: 'grid', gap: 1.5 }}>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} justifyContent="space-between" alignItems={{ xs: 'stretch', md: 'center' }}>
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 800 }}>Настройка зон</Typography>
          <Typography variant="body2" color="text.secondary">
            Разметка идёт по одному последнему кадру. Обновляй кадр вручную, когда нужно получить свежий стоп-кадр с камеры.
          </Typography>
        </Box>
        <Button
          variant="outlined"
          startIcon={<RefreshRoundedIcon />}
          onClick={() => {
            if (selectedCameraKey) {
              void loadFrame(selectedCameraKey);
            }
          }}
          disabled={!selectedCameraKey || isLoading}
        >
          Обновить кадр
        </Button>
      </Stack>

      {error ? <Alert severity="warning">{error}</Alert> : null}

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: selectedZoneName === customOptionLabel ? '1fr 1fr 1fr' : '1fr 1fr' }, gap: 1.5 }}>
        <TextField select label="Камера" value={selectedCameraKey} onChange={(event) => setSelectedCameraKey(event.target.value)} fullWidth>
          {cameras.map((camera) => (
            <MenuItem key={camera.key} value={camera.key}>
              {camera.name} ({camera.sourceCameraKey})
            </MenuItem>
          ))}
        </TextField>
        <TextField select label="Тип зоны" value={selectedZoneName} onChange={(event) => setSelectedZoneName(event.target.value)} fullWidth>
          {zoneNameOptions.map((zoneName) => (
            <MenuItem key={zoneName} value={zoneName}>
              {zoneName}
            </MenuItem>
          ))}
        </TextField>
        {selectedZoneName === customOptionLabel ? (
          <TextField fullWidth label="Своё название зоны" value={customZoneName} onChange={(event) => setCustomZoneName(event.target.value)} />
        ) : null}
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', xl: 'minmax(0, 1.45fr) minmax(320px, 0.9fr)' }, gap: 2, alignItems: 'start' }}>
        <Paper variant="outlined" sx={{ p: { xs: 1.25, md: 1.6 }, borderRadius: '14px', display: 'grid', gap: 1.25 }}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} alignItems={{ xs: 'stretch', md: 'center' }} justifyContent="space-between">
            <Typography variant="body2" color="text.secondary">
              Клик по кадру добавляет вершину полигона. Точки можно двигать мышью. Сохранённые зоны отображаются поверх этого же фиксированного кадра.
            </Typography>
            <Stack direction="row" spacing={1} flexWrap="wrap">
              <Button variant="outlined" startIcon={<AddRoundedIcon />} onClick={beginNewZone}>Новая зона</Button>
              <Button variant="outlined" startIcon={<RemoveRoundedIcon />} onClick={() => setDraftPoints((current) => current.slice(0, -1))} disabled={draftPoints.length === 0}>Убрать точку</Button>
              <Button variant="outlined" startIcon={<RestartAltRoundedIcon />} onClick={() => setDraftPoints([])} disabled={draftPoints.length === 0}>Сбросить</Button>
            </Stack>
          </Stack>

          <Box sx={{ position: 'relative', width: '100%', aspectRatio: '16 / 9', borderRadius: '14px', overflow: 'hidden', border: '1px solid rgba(148,163,184,0.18)', background: 'linear-gradient(145deg, rgba(2,6,23,0.92), rgba(15,23,42,0.82))' }}>
            {isLoading ? (
              <Box sx={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <CircularProgress />
              </Box>
            ) : frameUrl ? (
              <>
                <Box component="img" src={frameUrl} alt="Последний кадр камеры" sx={{ position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'contain', display: 'block', background: '#020617' }} />
                <svg ref={overlayRef} className="zone-overlay" viewBox="0 0 100 100" preserveAspectRatio="none" onClick={handleOverlayClick} onPointerMove={handleOverlayPointerMove} onPointerUp={handleOverlayPointerUp}>
                  {zones.map((zone) => (
                    <polygon
                      key={zone.id}
                      className={zone.id === selectedZoneId ? 'saved-zone is-selected' : 'saved-zone'}
                      points={zone.points.map((point) => `${point.x * 100},${point.y * 100}`).join(' ')}
                      onClick={(event) => {
                        event.stopPropagation();
                        selectZone(zone);
                      }}
                    />
                  ))}
                  {draftPoints.length > 0 ? (
                    <>
                      <polyline className="draft-zone" points={previewPolygonPoints} />
                      {draftPoints.length >= 3 ? <polygon className="draft-zone-fill" points={previewPolygonPoints} /> : null}
                      {draftPoints.map((point, index) => (
                        <circle key={`${point.x}-${point.y}-${index}`} className="draft-point" cx={point.x * 100} cy={point.y * 100} r={1.3} onPointerDown={(event) => handlePointPointerDown(index, event)} />
                      ))}
                    </>
                  ) : null}
                </svg>
              </>
            ) : (
              <Box sx={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', px: 3 }}>
                <Alert severity="info">Выберите камеру и обновите кадр, чтобы начать разметку.</Alert>
              </Box>
            )}
          </Box>
        </Paper>

        <Paper variant="outlined" sx={{ p: { xs: 1.25, md: 1.6 }, borderRadius: '14px', display: 'grid', gap: 1.25 }}>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip label={`Точек: ${draftPoints.length}`} color={draftPoints.length >= 3 ? 'success' : 'default'} variant="outlined" />
            {selectedZoneId ? (
              <Chip label="Редактирование сохранённой зоны" color="info" variant="outlined" />
            ) : (
              <Chip label="Создание новой зоны" color="warning" variant="outlined" />
            )}
          </Stack>

          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.2}>
            <Button variant="contained" startIcon={<SaveRoundedIcon />} onClick={handleSaveZone} disabled={isSaving}>
              {selectedZoneId ? 'Сохранить изменения' : 'Сохранить зону'}
            </Button>
            <Button variant="outlined" color="error" startIcon={<DeleteOutlineRoundedIcon />} onClick={handleDeleteZone} disabled={!selectedZoneId || isSaving}>
              Удалить зону
            </Button>
          </Stack>

          <Box className="zone-list">
            <Typography variant="subtitle2" sx={{ fontWeight: 800, mb: 1 }}>Сохранённые зоны камеры</Typography>
            <Stack spacing={1}>
              {zones.length > 0 ? (
                zones.map((zone) => (
                  <Button key={zone.id} variant={zone.id === selectedZoneId ? 'contained' : 'outlined'} onClick={() => selectZone(zone)} sx={{ justifyContent: 'space-between' }}>
                    <span>{zone.displayName}</span>
                    <span>{zone.points.length} pts</span>
                  </Button>
                ))
              ) : (
                <Alert severity="info">Для этой камеры пока нет сохранённых зон.</Alert>
              )}
            </Stack>
          </Box>
        </Paper>
      </Box>
    </Paper>
  );
};

export default CentralZoneConfigurator;

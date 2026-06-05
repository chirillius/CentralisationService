import { Box, Button, Chip, Paper, Typography } from '@mui/material';
import PageHero from '../components/PageHero';
import { useAppStore } from '../store';
import {
  ELEVATED_BACKGROUND,
  PAGE_CONTENT_MAX_WIDTH,
  SURFACE_BORDER,
  SURFACE_SHADOW,
  UI_RADIUS_PX,
} from '../theme/designTokens';

const PlaceholderPage = ({
  title,
  subtitle,
  actionLabel = 'Открыть раздел',
}: {
  title: string;
  subtitle: string;
  actionLabel?: string;
}) => {
  const { selectedStore, showAlert, showSnackbar } = useAppStore();

  return (
    <Box sx={{ width: '100%', maxWidth: PAGE_CONTENT_MAX_WIDTH, mx: 'auto', px: { xs: 2, md: 3 }, pb: 5 }}>
      <PageHero title={title} subtitle={subtitle} storeName={selectedStore?.siteName ?? null} />

      <Paper sx={{ p: { xs: 2.5, md: 3 }, borderRadius: UI_RADIUS_PX, border: SURFACE_BORDER, background: ELEVATED_BACKGROUND, boxShadow: SURFACE_SHADOW }}>
        <Box sx={{ display: 'grid', gap: 2 }}>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            <Chip label="Central client" color="primary" variant="outlined" />
            <Chip
              label="Визуальный раздел подготовлен"
              sx={{ borderRadius: UI_RADIUS_PX, bgcolor: 'rgba(56,189,248,0.12)', color: '#7dd3fc' }}
            />
          </Box>

          <Typography variant="body1" color="text.secondary" sx={{ lineHeight: 1.7 }}>
            Этот раздел уже присутствует в новом интерфейсе и визуально встроен в shell первого приложения, но его серверная логика пока не подключена к CentralServer.
          </Typography>

          <Box sx={{ display: 'flex', gap: 1.25, flexWrap: 'wrap' }}>
            <Button variant="contained" onClick={() => showSnackbar('Раздел открыт как визуальная заглушка.')}>
              {actionLabel}
            </Button>
            <Button
              variant="outlined"
              onClick={() => showAlert('Внимание', 'Для этого раздела центральная логика будет подключаться отдельным этапом.')}
            >
              Показать пояснение
            </Button>
          </Box>
        </Box>
      </Paper>
    </Box>
  );
};

export default PlaceholderPage;

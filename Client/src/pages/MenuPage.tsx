import DownloadForOfflineRoundedIcon from '@mui/icons-material/DownloadForOfflineRounded';
import FactCheckRoundedIcon from '@mui/icons-material/FactCheckRounded';
import HelpOutlineRoundedIcon from '@mui/icons-material/HelpOutlineRounded';
import InsightsRoundedIcon from '@mui/icons-material/InsightsRounded';
import PlayCircleRoundedIcon from '@mui/icons-material/PlayCircleRounded';
import SensorsRoundedIcon from '@mui/icons-material/SensorsRounded';
import { Box, IconButton, Paper, Tooltip, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useAppStore } from '../store';
import { ELEVATED_BACKGROUND, SURFACE_BORDER, UI_RADIUS_PX } from '../theme/designTokens';

const menuCards = [
  {
    title: 'Потоковое видео',
    description: 'Быстрый доступ к видеопотокам магазина и просмотру подключённых камер.',
    path: '/streaming',
    icon: SensorsRoundedIcon,
  },
  {
    title: 'Загрузка и просмотр записей',
    description: 'Скачивание и просмотр сохранённых архивов по выбранным дате и времени.',
    path: '/download-archive',
    icon: DownloadForOfflineRoundedIcon,
  },
  {
    title: 'Онлайн просмотр',
    description: 'Работа с архивом за выбранные дату и время без предварительного скачивания записей.',
    path: '/live-archive',
    icon: PlayCircleRoundedIcon,
  },
  {
    title: 'Таблица нарушений',
    description: 'Поиск, подтверждение и просмотр зафиксированных нарушений по выбранному магазину.',
    path: '/defects',
    icon: FactCheckRoundedIcon,
  },
  {
    title: 'Статистика',
    description: 'Сводная аналитика по фиксациям за период и динамике нарушений.',
    path: '/statistics',
    icon: InsightsRoundedIcon,
  },
];

const MenuPage = () => {
  const navigate = useNavigate();
  const { selectedStore, showAlert } = useAppStore();

  const handleNavigate = (path: string) => {
    if (!selectedStore?.siteName && path !== '/stores') {
      showAlert('Внимание', 'Сначала выберите магазин.');
      navigate('/stores');
      return;
    }

    navigate(path);
  };

  return (
    <Box sx={{ width: '100%', display: 'flex', justifyContent: 'center', py: '110px', position: 'relative' }}>
      <Box sx={{ width: '100%', maxWidth: 720, display: 'grid', gridTemplateColumns: '1fr', gap: 1.4 }}>
        {menuCards.map((item) => {
          const IconComponent = item.icon;
          return (
            <Paper
              key={item.path}
              onClick={() => handleNavigate(item.path)}
              sx={{
                p: { xs: 1.5, md: 1.75 },
                borderRadius: UI_RADIUS_PX,
                border: SURFACE_BORDER,
                background: ELEVATED_BACKGROUND,
                boxShadow: '0 14px 34px rgba(2,6,23,0.2)',
                transition: 'transform 0.18s ease-out, box-shadow 0.18s ease-out, border-color 0.18s ease-out',
                cursor: 'pointer',
                '&:hover': {
                  transform: 'translateY(-2px)',
                  boxShadow: '0 18px 38px rgba(15,23,42,0.28)',
                  borderColor: 'rgba(96,165,250,0.28)',
                },
              }}
            >
              <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'auto minmax(0, 1fr)' }, alignItems: 'center', gap: { xs: 1.2, md: 1.5 } }}>
                <Box sx={{ width: 46, height: 46, borderRadius: UI_RADIUS_PX, display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'rgba(99,102,241,0.14)', color: '#c4b5fd', boxShadow: 'inset 0 0 0 1px rgba(129,140,248,0.16)', flexShrink: 0 }}>
                  <IconComponent sx={{ fontSize: 24 }} />
                </Box>
                <Box sx={{ minWidth: 0, pl: 0.25 }}>
                  <Typography variant="h6" sx={{ fontWeight: 800, mb: 0.35, fontSize: { xs: '1.02rem', md: '1.08rem' }, lineHeight: 1.2 }}>
                    {item.title}
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.5, fontSize: '0.9rem' }}>
                    {item.description}
                  </Typography>
                </Box>
              </Box>
            </Paper>
          );
        })}
      </Box>

      <Tooltip title="FAQ" placement="right">
        <IconButton
          aria-label="FAQ"
          onClick={() => navigate('/faq')}
          sx={{
            position: 'fixed',
            left: { xs: 84, md: 92 },
            bottom: { xs: 22, md: 28 },
            width: 46,
            height: 46,
            borderRadius: '50%',
            color: '#c4b5fd',
            backgroundColor: 'rgba(99,102,241,0.12)',
            boxShadow: 'inset 0 0 0 1px rgba(129,140,248,0.16)',
            transition: 'transform 0.18s ease-out, box-shadow 0.18s ease-out, background-color 0.18s ease-out',
            zIndex: 1200,
            '&:hover': {
              transform: 'translateY(-2px)',
              backgroundColor: 'rgba(99,102,241,0.18)',
              boxShadow: 'inset 0 0 0 1px rgba(129,140,248,0.22), 0 12px 24px rgba(15,23,42,0.28)',
            },
          }}
        >
          <HelpOutlineRoundedIcon sx={{ fontSize: 22 }} />
        </IconButton>
      </Tooltip>
    </Box>
  );
};

export default MenuPage;

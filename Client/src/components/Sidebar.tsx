import { Box, Divider, List, ListItem, ListItemButton, Tooltip } from '@mui/material';
import SensorsRoundedIcon from '@mui/icons-material/SensorsRounded';
import DownloadForOfflineRoundedIcon from '@mui/icons-material/DownloadForOfflineRounded';
import PlayCircleRoundedIcon from '@mui/icons-material/PlayCircleRounded';
import FactCheckRoundedIcon from '@mui/icons-material/FactCheckRounded';
import InsightsRoundedIcon from '@mui/icons-material/InsightsRounded';
import { useLocation, useNavigate } from 'react-router-dom';
import kvlogo from '../icons/kvlogo.svg';
import { ELEVATED_BACKGROUND, SURFACE_BORDER, SURFACE_SHADOW, UI_RADIUS_PX } from '../theme/designTokens';

const menuItems = [
  { text: 'Потоковое видео', path: '/streaming', icon: SensorsRoundedIcon },
  { text: 'Загрузка и просмотр записей', path: '/download-archive', icon: DownloadForOfflineRoundedIcon },
  { text: 'Онлайн просмотр', path: '/live-archive', icon: PlayCircleRoundedIcon },
  { text: 'Таблица нарушений', path: '/defects', icon: FactCheckRoundedIcon },
  { text: 'Статистика', path: '/statistics', icon: InsightsRoundedIcon },
];

const Sidebar = () => {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Box
      sx={{
        width: 68,
        height: '100vh',
        borderRight: SURFACE_BORDER,
        display: 'flex',
        flexDirection: 'column',
        position: 'fixed',
        left: 0,
        top: 0,
        zIndex: 1000,
        backdropFilter: 'blur(18px)',
        boxShadow: SURFACE_SHADOW,
        background: ELEVATED_BACKGROUND,
      }}
    >
      <Box
        sx={{
          p: 1.5,
          pb: 1,
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          minHeight: 62,
        }}
      >
        <Box
          onClick={() => navigate('/menu')}
          role="button"
          tabIndex={0}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              navigate('/menu');
            }
          }}
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: 44,
            height: 44,
            borderRadius: UI_RADIUS_PX,
            cursor: 'pointer',
            transition: 'transform 0.18s ease, background-color 0.18s ease, box-shadow 0.18s ease',
            '&:hover': {
              transform: 'translateY(-1px)',
              backgroundColor: 'rgba(99,102,241,0.12)',
              boxShadow: '0 10px 24px rgba(15,23,42,0.32)',
            },
          }}
        >
          <Box component="img" src={kvlogo} alt="KV Logo" sx={{ width: 40, height: 40, display: 'block' }} />
        </Box>
      </Box>

      <Divider />

      <Box sx={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', overflow: 'hidden', py: 2 }}>
        <List sx={{ width: '100%', flex: 0 }}>
          {menuItems.map((item) => {
            const IconComponent = item.icon;
            const isActive = location.pathname === item.path;
            return (
              <ListItem key={item.path} disablePadding>
                <Tooltip title={item.text} placement="right">
                  <ListItemButton
                    selected={isActive}
                    onClick={() => navigate(item.path)}
                    sx={{
                      justifyContent: 'center',
                      py: 1.45,
                      color: isActive ? '#fff' : 'rgba(226,232,240,0.84)',
                      transition: 'background-color 0.2s ease, color 0.2s ease, transform 0.2s ease',
                      '&:hover': {
                        bgcolor: 'rgba(99,102,241,0.14)',
                        color: '#c4b5fd',
                        transform: 'translateX(1px)',
                      },
                      '&.Mui-selected': {
                        bgcolor: 'primary.main',
                        color: 'primary.contrastText',
                        boxShadow: '0 12px 24px rgba(99,102,241,0.26)',
                        '&:hover': { bgcolor: 'primary.dark' },
                      },
                    }}
                  >
                    <IconComponent sx={{ fontSize: 28 }} />
                  </ListItemButton>
                </Tooltip>
              </ListItem>
            );
          })}
        </List>
      </Box>
    </Box>
  );
};

export default Sidebar;

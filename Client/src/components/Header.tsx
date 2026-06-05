import StorefrontRoundedIcon from '@mui/icons-material/StorefrontRounded';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import { Box, Button, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useAppStore } from '../store';
import { ELEVATED_BACKGROUND, SURFACE_BORDER, SURFACE_SHADOW, UI_RADIUS_PX } from '../theme/designTokens';

const Header = () => {
  const navigate = useNavigate();
  const { selectedStore, logout, showSnackbar } = useAppStore();

  return (
    <Box
      sx={{
        position: 'fixed',
        top: 0,
        left: 68,
        right: 0,
        height: 64,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        px: { xs: 1.5, md: 2.5 },
        gap: 2,
        zIndex: 1100,
        backdropFilter: 'blur(18px)',
        boxShadow: SURFACE_SHADOW,
        borderBottom: SURFACE_BORDER,
        background: ELEVATED_BACKGROUND,
      }}
    >
      <Box sx={{ minWidth: 0, display: 'flex', alignItems: 'center' }}>
        <Box
          onClick={() => navigate('/stores')}
          role="button"
          tabIndex={0}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              navigate('/stores');
            }
          }}
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1.15,
            minWidth: 0,
            px: { xs: 1.2, sm: 1.45, md: 1.7 },
            py: 0.85,
            borderRadius: UI_RADIUS_PX,
            border: SURFACE_BORDER,
            background: 'linear-gradient(135deg, rgba(15,23,42,0.72), rgba(30,41,59,0.52))',
            boxShadow: '0 10px 28px rgba(15,23,42,0.35)',
            cursor: 'pointer',
            transition: 'transform 0.18s ease-out, box-shadow 0.18s ease-out, border-color 0.18s ease-out, background 0.18s ease-out',
            '&:hover': {
              transform: 'translateY(-1px)',
              borderColor: 'rgba(96,165,250,0.45)',
              boxShadow: '0 14px 32px rgba(15,23,42,0.45)',
              background: 'linear-gradient(135deg, rgba(15,23,42,0.82), rgba(37,99,235,0.24))',
            },
          }}
        >
          <StorefrontRoundedIcon sx={{ color: 'rgba(226,232,240,0.9)' }} />
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="caption" sx={{ display: 'block', color: 'rgba(148,163,184,0.88)', letterSpacing: '0.08em', textTransform: 'uppercase', mb: 0.15 }}>
              Активный магазин
            </Typography>
            <Typography variant="subtitle1" sx={{ fontWeight: 800, maxWidth: { xs: 170, sm: 280, md: 360 }, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', lineHeight: 1.1 }}>
              {selectedStore?.siteName ?? 'Магазин не выбран'}
            </Typography>
          </Box>
        </Box>
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Button variant="outlined" onClick={() => navigate('/stores')}>
          Магазины
        </Button>
        <Button
          variant="outlined"
          color="inherit"
          startIcon={<LogoutRoundedIcon />}
          onClick={() => {
            logout();
            showSnackbar('Сеанс завершён.');
            navigate('/login', { replace: true });
          }}
        >
          Выход
        </Button>
      </Box>
    </Box>
  );
};

export default Header;

import { Box } from '@mui/material';
import Header from './Header';
import Sidebar from './Sidebar';
import {
  PAGE_CONTENT_MAX_WIDTH,
  SURFACE_BACKGROUND,
  SURFACE_BORDER,
  SURFACE_SHADOW,
  UI_RADIUS_PX,
} from '../theme/designTokens';

const AppLayout = ({
  children,
  maxWidth = PAGE_CONTENT_MAX_WIDTH,
  showSurface = true,
}: {
  children: React.ReactNode;
  maxWidth?: string | number;
  showSurface?: boolean;
}) => {
  return (
    <Box
      sx={{
        display: 'flex',
        minHeight: '100vh',
        bgcolor: 'background.default',
        backgroundImage:
          'radial-gradient(circle at top, rgba(99,102,241,0.18), transparent 55%), radial-gradient(circle at bottom, rgba(15,23,42,0.9), #020617)',
      }}
    >
      <Sidebar />
      <Header />
      <Box
        sx={{
          '--app-layout-top-offset': { xs: '92px', md: '98px' },
          '--app-layout-bottom-offset': { xs: '10px', sm: '14px', md: '18px' },
          '--app-surface-padding-y': showSurface ? { xs: '28px', sm: '36px', md: '44px' } : '0px',
          pl: '68px',
          pt: { xs: '92px', md: '98px' },
          pr: { xs: 1.25, sm: 1.75, md: 2.25 },
          pb: { xs: 1.25, sm: 1.75, md: 2.25 },
          flex: 1,
          display: 'flex',
          alignItems: 'stretch',
        }}
      >
        <Box
          sx={{
            width: '100%',
            maxWidth,
            mx: 'auto',
            ...(showSurface
              ? {
                  borderRadius: UI_RADIUS_PX,
                  overflow: 'hidden',
                  backdropFilter: 'blur(18px)',
                  background: SURFACE_BACKGROUND,
                  border: SURFACE_BORDER,
                  boxShadow: SURFACE_SHADOW,
                  p: { xs: 1.75, sm: 2.25, md: 2.75 },
                }
              : { p: 0 }),
          }}
        >
          {children}
        </Box>
      </Box>
    </Box>
  );
};

export default AppLayout;

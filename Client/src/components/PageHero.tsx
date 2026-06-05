import { Box, Chip, Typography } from '@mui/material';
import { HERO_TITLE_SX, PAGE_SECTION_SPACING, UI_RADIUS_PX } from '../theme/designTokens';

const PageHero = ({
  title,
  subtitle,
  storeName,
}: {
  title: string;
  subtitle?: string;
  storeName?: string | null;
}) => {
  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: { xs: 'flex-start', md: 'center' },
        flexWrap: 'wrap',
        gap: 2,
        mb: PAGE_SECTION_SPACING,
      }}
    >
      <Box sx={{ minWidth: 0, pl: 0.25 }}>
        <Typography variant="h4" component="h1" sx={{ ...HERO_TITLE_SX, mb: 0.75 }}>
          {title}
        </Typography>
        {subtitle ? (
          <Typography variant="body1" color="text.secondary" sx={{ maxWidth: 780, lineHeight: 1.55 }}>
            {subtitle}
          </Typography>
        ) : null}
      </Box>

      {storeName ? (
        <Chip
          label={storeName}
          color="primary"
          variant="outlined"
          sx={{
            px: 1,
            height: 36,
            fontWeight: 700,
            borderRadius: UI_RADIUS_PX,
            borderColor: 'rgba(96,165,250,0.45)',
            backgroundColor: 'rgba(15,23,42,0.42)',
            backdropFilter: 'blur(10px)',
          }}
        />
      ) : null}
    </Box>
  );
};

export default PageHero;

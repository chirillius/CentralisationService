export const UI_RADIUS = 14;
export const UI_RADIUS_PX = `${UI_RADIUS}px`;

export const SURFACE_BORDER = '1px solid rgba(148,163,184,0.16)';
export const SURFACE_BACKGROUND =
  'linear-gradient(145deg, rgba(15,23,42,0.94), rgba(15,23,42,0.98))';
export const ELEVATED_BACKGROUND =
  'linear-gradient(145deg, rgba(15,23,42,0.92), rgba(30,41,59,0.72))';
export const SURFACE_SHADOW = '0 20px 48px rgba(15,23,42,0.58)';

export const PAGE_CONTENT_MAX_WIDTH = '1440px';
export const PAGE_SECTION_SPACING = { xs: 2, md: 2.5 } as const;

export const HERO_TITLE_COLOR = '#eef4ff';
export const HERO_TITLE_TEXT_SHADOW = [
  '0 1px 0 rgba(2,6,23,0.92)',
  '1px 0 0 rgba(96,165,250,0.18)',
  '-1px 0 0 rgba(96,165,250,0.18)',
  '0 0 14px rgba(96,165,250,0.08)',
].join(', ');

export const HERO_TITLE_SX = {
  fontWeight: 800,
  lineHeight: 1.08,
  letterSpacing: '-0.03em',
  color: HERO_TITLE_COLOR,
  textShadow: HERO_TITLE_TEXT_SHADOW,
} as const;

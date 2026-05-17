import img01 from '../../assets/img_01.jpg';
import img02 from '../../assets/img_02.jpg';
import img03 from '../../assets/img_03.jpg';
import img04 from '../../assets/img_04.jpg';
import img05 from '../../assets/img_05.jpg';
import img06 from '../../assets/img_06.jpg';
import img07 from '../../assets/img_07.jpg';

export const IMG = {
  aerodrom: img01,
  regele: img02,
  turda: img03,
  decorat: img04,
  inchisoare: img05,
  fabrica: img06,
  cer: img07,
} as const;

export const FONT = 'Courier New';

export const FONT_SIZE = {
  date: 40,
  body: 36,
  small: 28,
  title: 84,
  tiny: 22,
} as const;

export const COLOR = {
  bg: '#0d0d0d',
  text: '#e6e6e6',
  dim: '#8a8a8a',
  gold: '#a89060',
  white: '#ffffff',
} as const;

export const CHAR_DELAY = {
  fast: 0.025,
  normal: 0.05,
  slow: 0.09,
  glacial: 0.14,
} as const;

export const BLINK_PERIOD = 0.5;

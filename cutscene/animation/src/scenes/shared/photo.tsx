import {
  Img,
  ImgProps,
  blur,
  brightness,
  contrast,
  grayscale,
  hue,
  saturate,
  sepia,
} from '@motion-canvas/2d';
import {Reference} from '@motion-canvas/core';

export interface AgedPhotoProps extends Omit<ImgProps, 'filters'> {
  imgRef?: Reference<Img>;
}

export function AgedPhoto({imgRef, ...imgProps}: AgedPhotoProps) {
  return (
    <Img
      ref={imgRef}
      {...imgProps}
      filters={[grayscale(0.75), contrast(1.18), sepia(0.18)]}
    />
  );
}

export function SepiaPhoto({imgRef, ...imgProps}: AgedPhotoProps) {
  return (
    <Img
      ref={imgRef}
      {...imgProps}
      filters={[
        grayscale(1),
        sepia(0.95),
        hue(-8),
        saturate(1.55),
        contrast(0.88),
        brightness(1.08),
        blur(1.2),
      ]}
    />
  );
}

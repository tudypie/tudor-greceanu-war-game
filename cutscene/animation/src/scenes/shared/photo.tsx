import {Img, ImgProps, grayscale, contrast, sepia} from '@motion-canvas/2d';
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

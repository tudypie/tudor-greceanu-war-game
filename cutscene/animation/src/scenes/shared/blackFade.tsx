import {Rect, View2D} from '@motion-canvas/2d';
import {createSignal} from '@motion-canvas/core';

export function setupBlackFade(view: View2D) {
  const op = createSignal(1);
  view.add(
    <Rect
      width={view.width}
      height={view.height}
      fill={'#000000'}
      opacity={op}
      zIndex={9999}
    />,
  );
  return {
    fadeIn: (duration: number = 1.2) => op(0, duration),
    fadeOut: (duration: number = 1.2) => op(1, duration),
  };
}

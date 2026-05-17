import {Rect} from '@motion-canvas/2d';
import {SimpleSignal, waitFor} from '@motion-canvas/core';
import {BLINK_PERIOD, CHAR_DELAY} from './constants';

export function* typeText(
  signal: SimpleSignal<string>,
  text: string,
  charDelay: number = CHAR_DELAY.normal,
) {
  for (let c = 1; c <= text.length; c++) {
    signal(text.substring(0, c));
    yield* waitFor(charDelay);
  }
}

export function* blink(cursor: Rect, cycles: number) {
  for (let i = 0; i < cycles; i++) {
    yield* cursor.opacity(0, BLINK_PERIOD / 2);
    yield* cursor.opacity(1, BLINK_PERIOD / 2);
  }
}

export function* retypeText(
  signal: SimpleSignal<string>,
  newText: string,
  charDelay: number = CHAR_DELAY.fast,
) {
  const current = signal();
  for (let c = current.length; c > 0; c--) {
    signal(current.substring(0, c - 1));
    yield* waitFor(charDelay * 0.5);
  }
  yield* typeText(signal, newText, charDelay);
}

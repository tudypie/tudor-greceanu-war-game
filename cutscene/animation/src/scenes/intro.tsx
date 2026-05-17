import {Layout, Rect, Txt, makeScene2D} from '@motion-canvas/2d';
import {createRef, createSignal, waitFor} from '@motion-canvas/core';
import {setupBlackFade} from './shared/blackFade';

const LINES = [
  'Data: mai 1943',
  'Locatie: aerodromul Makievska',
  'Obiectiv: Apara aerodromul alaturi de trupele germane',
];

const FONT = 'Courier New';
const FONT_SIZE = 44;
const FILL = '#e6e6e6';
const BG = '#000000';
const CHAR_DELAY = 0.06;
const BLINK_PERIOD = 0.5;

export default makeScene2D(function* (view) {
  view.fill(BG);

  const fade = setupBlackFade(view);

  const sigs = LINES.map(() => createSignal(''));
  const cursors = LINES.map(() => createRef<Rect>());

  view.add(
    <Layout
      layout
      direction={'column'}
      gap={18}
      x={-view.width() / 2 + 80}
      y={-view.height() / 2 + 80}
      offset={[-1, -1]}
    >
      {LINES.map((_, i) => (
        <Layout direction={'row'} alignItems={'center'} gap={6}>
          <Txt
            text={sigs[i]}
            fontFamily={FONT}
            fontSize={FONT_SIZE}
            fill={FILL}
          />
          <Rect
            ref={cursors[i]}
            width={FONT_SIZE * 0.55}
            height={FONT_SIZE}
            fill={FILL}
            opacity={0}
          />
        </Layout>
      ))}
    </Layout>,
  );

  yield* fade.fadeIn();

  for (let i = 0; i < LINES.length; i++) {
    cursors[i]().opacity(1);
    yield* blink(cursors[i](), 1);

    for (let c = 1; c <= LINES[i].length; c++) {
      sigs[i](LINES[i].substring(0, c));
      yield* waitFor(CHAR_DELAY);
    }

    yield* blink(cursors[i](), 2);

    if (i < LINES.length - 1) {
      cursors[i]().opacity(0);
    }
  }

  yield* blink(cursors[LINES.length - 1](), 8);

  yield* fade.fadeOut();
});

function* blink(cursor: Rect, cycles: number) {
  for (let i = 0; i < cycles; i++) {
    yield* cursor.opacity(0, BLINK_PERIOD / 2);
    yield* cursor.opacity(1, BLINK_PERIOD / 2);
  }
}

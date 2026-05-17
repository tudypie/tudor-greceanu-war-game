import {Img, Layout, Rect, Txt, makeScene2D} from '@motion-canvas/2d';
import {Reference, SimpleSignal, createRef, createSignal, waitFor} from '@motion-canvas/core';
import {CHAR_DELAY, COLOR, FONT, FONT_SIZE, IMG} from './shared/constants';
import {AgedPhoto} from './shared/photo';
import {blink, typeText} from './shared/typewriter';

interface Line {
  text: string;
  signal: SimpleSignal<string>;
  cursor: Reference<Rect>;
  size: number;
  fill: string;
  marginTop?: number;
  charDelay?: number;
}

export default makeScene2D(function* (view) {
  view.fill(COLOR.bg);

  const mk = (
    text: string,
    size: number,
    fill: string,
    marginTop: number = 0,
    charDelay: number = CHAR_DELAY.normal,
  ): Line => ({
    text,
    signal: createSignal<string>(''),
    cursor: createRef<Rect>(),
    size,
    fill,
    marginTop,
    charDelay,
  });

  const lines: Line[] = [
    mk('Data: Mai 1943', FONT_SIZE.date, COLOR.text),
    mk('Locatie: Aerodromul Makievska, Ucraina', FONT_SIZE.body, COLOR.text),
    mk('Obiectiv: Apara aerodromul alaturi de trupele germane', FONT_SIZE.body, COLOR.text),
    mk('Frontul de Est, 1943.', FONT_SIZE.small, COLOR.dim, 30),
    mk('Romania lupta alaturi de Germania nazista.', FONT_SIZE.small, COLOR.dim),
    mk('Pilotii romani aparau cerul impreuna.', FONT_SIZE.small, COLOR.dim),
    mk('Esti pilot. Nimic altceva nu conteaza acum.', FONT_SIZE.body, COLOR.text, 50, CHAR_DELAY.slow),
  ];

  const imgRef = createRef<Img>();
  const imgOpacity = createSignal(0);

  view.add(
    <Layout
      layout
      direction={'column'}
      gap={18}
      x={-view.width() / 2 + 80}
      y={-view.height() / 2 + 80}
      offset={[-1, -1]}
    >
      {lines.map((line) => (
        <Layout
          direction={'row'}
          alignItems={'center'}
          gap={6}
          marginTop={line.marginTop ?? 0}
        >
          <Txt
            text={line.signal}
            fontFamily={FONT}
            fontSize={line.size}
            fill={line.fill}
          />
          <Rect
            ref={line.cursor}
            width={line.size * 0.55}
            height={line.size}
            fill={line.fill}
            opacity={0}
          />
        </Layout>
      ))}
    </Layout>,
  );

  view.add(
    <AgedPhoto
      imgRef={imgRef}
      src={IMG.aerodrom}
      width={680}
      height={460}
      x={view.width() / 2 - 80 - 340}
      y={60}
      opacity={imgOpacity}
    />,
  );

  yield* waitFor(0.4);

  lines[0].cursor().opacity(1);
  yield* blink(lines[0].cursor(), 1);
  yield* typeText(lines[0].signal, lines[0].text, lines[0].charDelay);
  yield* blink(lines[0].cursor(), 3);
  lines[0].cursor().opacity(0);

  for (const idx of [1, 2]) {
    lines[idx].cursor().opacity(1);
    yield* typeText(lines[idx].signal, lines[idx].text, lines[idx].charDelay);
    yield* blink(lines[idx].cursor(), 2);
    lines[idx].cursor().opacity(0);
  }

  yield* waitFor(1.2);
  yield* imgOpacity(1, 0.8);

  for (const idx of [3, 4, 5]) {
    lines[idx].cursor().opacity(1);
    yield* typeText(lines[idx].signal, lines[idx].text, lines[idx].charDelay);
    lines[idx].cursor().opacity(0);
    yield* waitFor(0.3);
  }

  yield* waitFor(1.8);
  lines[6].cursor().opacity(1);
  yield* typeText(lines[6].signal, lines[6].text, lines[6].charDelay);
  yield* blink(lines[6].cursor(), 4);
  lines[6].cursor().opacity(0);

  yield* waitFor(1.0);
  yield* imgOpacity(0, 1.2);
  yield* waitFor(1.5);
});

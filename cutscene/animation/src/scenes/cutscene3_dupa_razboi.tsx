import {Layout, Txt, makeScene2D} from '@motion-canvas/2d';
import {createSignal, waitFor} from '@motion-canvas/core';
import {CHAR_DELAY, COLOR, FONT, FONT_SIZE, IMG} from './shared/constants';
import {AgedPhoto, SepiaPhoto} from './shared/photo';
import {retypeText, typeText} from './shared/typewriter';

export default makeScene2D(function* (view) {
  view.fill(COLOR.bg);

  const sDate = createSignal<string>('');

  const sGold = createSignal<string>('');
  const sPlain = createSignal<string>('');
  const img4Op = createSignal(0);
  const act1Op = createSignal(0);

  const sArrestTitle = createSignal<string>('');
  const sArrestCtx1 = createSignal<string>('');
  const sArrestCtx2 = createSignal<string>('');
  const img5Op = createSignal(0);
  const act2Op = createSignal(0);

  const sFactory1 = createSignal<string>('');
  const sFactory2 = createSignal<string>('');
  const sFactory3 = createSignal<string>('');
  const img6Op = createSignal(0);
  const act3Op = createSignal(0);

  const sTally = createSignal<string>('');
  const sEscape = createSignal<string>('');
  const sRelease1 = createSignal<string>('');
  const sRelease2 = createSignal<string>('');
  const act4Op = createSignal(0);

  const sEnd1 = createSignal<string>('');
  const sEnd2 = createSignal<string>('');
  const sEnd3 = createSignal<string>('');
  const end1Op = createSignal(1);
  const end2Op = createSignal(1);
  const end3Op = createSignal(1);
  const img7Op = createSignal(0);
  const act5Op = createSignal(0);

  const dateOp = createSignal(1);

  const sCredit = createSignal<string>('');
  const creditOp = createSignal(0);

  view.add(
    <Layout
      layout
      direction={'column'}
      gap={10}
      x={-view.width() / 2 + 80}
      y={-view.height() / 2 + 80}
      offset={[-1, -1]}
      opacity={dateOp}
    >
      <Txt
        text={sDate}
        fontFamily={FONT}
        fontSize={FONT_SIZE.date}
        fill={COLOR.text}
      />
    </Layout>,
  );

  view.add(
    <Layout
      layout
      direction={'row'}
      alignItems={'center'}
      gap={60}
      opacity={act1Op}
    >
      <Layout layout direction={'column'} gap={18}>
        <Txt
          text={sGold}
          fontFamily={FONT}
          fontSize={FONT_SIZE.body}
          fill={COLOR.gold}
        />
        <Txt
          text={sPlain}
          fontFamily={FONT}
          fontSize={FONT_SIZE.body}
          fill={COLOR.text}
        />
      </Layout>
      <Layout opacity={img4Op}>
        <AgedPhoto src={IMG.decorat} width={620} height={420} />
      </Layout>
    </Layout>,
  );

  view.add(
    <Layout
      layout
      direction={'column'}
      alignItems={'center'}
      gap={26}
      opacity={act2Op}
    >
      <Txt
        text={sArrestTitle}
        fontFamily={FONT}
        fontSize={FONT_SIZE.title}
        fontWeight={700}
        fill={COLOR.text}
      />
      <Layout opacity={img5Op}>
        <AgedPhoto src={IMG.inchisoare} width={1150} height={540} />
      </Layout>
      <Layout layout direction={'column'} alignItems={'center'} gap={10}>
        <Txt
          text={sArrestCtx1}
          fontFamily={FONT}
          fontSize={FONT_SIZE.small}
          fill={COLOR.dim}
        />
        <Txt
          text={sArrestCtx2}
          fontFamily={FONT}
          fontSize={FONT_SIZE.small}
          fill={COLOR.dim}
        />
      </Layout>
    </Layout>,
  );

  view.add(
    <Layout
      layout
      direction={'row'}
      alignItems={'center'}
      gap={70}
      opacity={act3Op}
    >
      <Layout opacity={img6Op}>
        <SepiaPhoto src={IMG.fabrica} width={720} height={480} />
      </Layout>
      <Layout layout direction={'column'} gap={16}>
        <Txt
          text={sFactory1}
          fontFamily={FONT}
          fontSize={FONT_SIZE.small}
          fill={COLOR.text}
        />
        <Txt
          text={sFactory2}
          fontFamily={FONT}
          fontSize={FONT_SIZE.small}
          fill={COLOR.dim}
        />
        <Txt
          text={sFactory3}
          fontFamily={FONT}
          fontSize={FONT_SIZE.small}
          fill={COLOR.dim}
        />
      </Layout>
    </Layout>,
  );

  view.add(
    <Layout
      layout
      direction={'column'}
      alignItems={'center'}
      gap={36}
      opacity={act4Op}
    >
      <Txt
        text={sTally}
        fontFamily={FONT}
        fontSize={FONT_SIZE.body}
        fill={COLOR.text}
      />
      <Txt
        text={sEscape}
        fontFamily={FONT}
        fontSize={FONT_SIZE.body}
        fill={COLOR.text}
      />
      <Txt
        text={sRelease1}
        fontFamily={FONT}
        fontSize={FONT_SIZE.body}
        fill={COLOR.text}
      />
      <Txt
        text={sRelease2}
        fontFamily={FONT}
        fontSize={FONT_SIZE.body}
        fill={COLOR.dim}
      />
    </Layout>,
  );

  view.add(
    <Layout
      layout
      direction={'column'}
      alignItems={'center'}
      gap={28}
      opacity={act5Op}
    >
      <Layout opacity={img7Op}>
        <AgedPhoto src={IMG.cer} width={860} height={520} />
      </Layout>
      <Txt
        text={sEnd1}
        fontFamily={FONT}
        fontSize={FONT_SIZE.body}
        fill={COLOR.text}
        opacity={end1Op}
      />
      <Txt
        text={sEnd2}
        fontFamily={FONT}
        fontSize={FONT_SIZE.body}
        fill={COLOR.text}
        opacity={end2Op}
      />
      <Txt
        text={sEnd3}
        fontFamily={FONT}
        fontSize={FONT_SIZE.body}
        fill={COLOR.text}
        opacity={end3Op}
      />
    </Layout>,
  );

  view.add(
    <Txt
      text={sCredit}
      fontFamily={FONT}
      fontSize={FONT_SIZE.tiny}
      fill={COLOR.dim}
      y={view.height() / 2 - 60}
      opacity={creditOp}
    />,
  );

  // ----- ACT 1: Celebrarea -----
  yield* waitFor(0.4);
  act1Op(1);

  for (const year of ['1945', '1946', '1947']) {
    yield* retypeText(sDate, `Data: ${year}`, CHAR_DELAY.fast);
    yield* waitFor(0.5);
  }

  yield* img4Op(1, 1.2);
  yield* typeText(sGold, 'Esti pilot decorat. Misiuni indeplinite.', CHAR_DELAY.slow);
  yield* waitFor(1.2);
  yield* typeText(sPlain, 'Si ce daca?', CHAR_DELAY.slow);
  yield* waitFor(1.5);

  // ----- ACT 2: Arestarea -----
  yield* img4Op(0, 0.5);
  act1Op(0);

  yield* retypeText(sDate, 'Data: Aprilie 1949', CHAR_DELAY.fast);
  yield* waitFor(1.2);

  yield* act2Op(1, 0.6);
  yield* typeText(sArrestTitle, 'ARESTAT', CHAR_DELAY.glacial);
  yield* waitFor(0.6);
  yield* img5Op(1, 1.4);

  const arrestDelay = CHAR_DELAY.slow * 2;
  yield* typeText(
    sArrestCtx1,
    'Regimul comunist te-a acuzat de crime inventate.',
    arrestDelay,
  );
  yield* waitFor(0.3);
  yield* typeText(
    sArrestCtx2,
    'Pilotul care aparase Romania. Tu. Acum dusman al poporului',
    arrestDelay,
  );

  yield* waitFor(2.0);

  // ----- ACT 3: Fabrica -----
  yield* img5Op(0, 1.0);
  act2Op(0);
  sArrestTitle('');

  act3Op(1);
  yield* img6Op(1, 0.8);

  yield* retypeText(sDate, 'Data: 1949-1952', CHAR_DELAY.fast);

  yield* typeText(sFactory1, 'Lucrezi ca strungar.', CHAR_DELAY.normal);
  yield* waitFor(0.3);
  yield* typeText(
    sFactory2,
    'Pilot decorat, aplecat peste o unealta.',
    CHAR_DELAY.normal,
  );
  yield* waitFor(0.3);
  yield* typeText(sFactory3, 'Cerul iti este doar o amintire.', CHAR_DELAY.slow);

  yield* waitFor(1.8);

  // ----- ACT 4: Numarul anilor -----
  yield* img6Op(0, 0.8);
  act3Op(0);

  act4Op(1);

  let tallyAcc = '';
  const phaseA = ['1949', '1950', '1951', '1952'];
  for (const year of phaseA) {
    yield* retypeText(sDate, `Data: ${year}`, CHAR_DELAY.fast);
    tallyAcc += (tallyAcc ? ' ' : '') + '|';
    sTally(tallyAcc);
    yield* waitFor(0.45);
  }

  yield* waitFor(0.6);

  const phaseB = [
    '1953', '1954', '1955', '1956', '1957', '1958',
    '1959', '1960', '1961', '1962', '1963', '1964',
  ];
  for (const year of phaseB) {
    yield* retypeText(sDate, `Data: ${year}`, CHAR_DELAY.fast);
    tallyAcc += ' |';
    sTally(tallyAcc);
    yield* waitFor(0.22);
  }

  yield* retypeText(sDate, 'Data: 1964 — Amnistie generala', CHAR_DELAY.fast);
  yield* typeText(sRelease1, 'Esti eliberat.', CHAR_DELAY.slow);
  yield* waitFor(0.4);
  yield* typeText(sRelease2, 'Ai 50 de ani.', CHAR_DELAY.slow);

  yield* waitFor(2.5);

  // ----- ACT 5: Sfarsitul -----
  yield* act4Op(0, 1.2);

  act5Op(1);
  yield* img7Op(1, 2.0);

  yield* typeText(sEnd1, 'Razboiul nu ti-a dat nimic.', CHAR_DELAY.glacial);
  yield* waitFor(0.6);
  yield* typeText(sEnd2, 'Victoria nu ti-a dat nimic.', CHAR_DELAY.glacial);
  yield* waitFor(0.6);
  yield* typeText(sEnd3, 'Propria ta tara ti-a luat tot.', CHAR_DELAY.glacial);

  yield* waitFor(3.0);

  // Fade rows bottom-up
  yield* end3Op(0, 0.7);
  yield* end2Op(0, 0.7);
  yield* end1Op(0, 0.7);
  yield* img7Op(0, 0.9);
  yield* dateOp(0, 1.0);

  yield* waitFor(1.5);

  // Credit
  sCredit('Bazat pe viata pilotului Tudor Greceanu');
  creditOp(1);
  yield* waitFor(4.0);
});

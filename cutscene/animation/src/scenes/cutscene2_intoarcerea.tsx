import {Layout, Txt, makeScene2D} from '@motion-canvas/2d';
import {all, createRef, createSignal, waitFor} from '@motion-canvas/core';
import {CHAR_DELAY, COLOR, FONT, FONT_SIZE, IMG} from './shared/constants';
import {glitch} from './shared/fx';
import {AgedPhoto} from './shared/photo';
import {retypeText, typeText} from './shared/typewriter';

export default makeScene2D(function* (view) {
  view.fill(COLOR.bg);

  const sDate = createSignal<string>('Data: Mai 1943');
  const sLoc = createSignal<string>('');

  const sTitle = createSignal<string>('');
  const sCtx1 = createSignal<string>('');
  const sCtx2 = createSignal<string>('');
  const sCtx3 = createSignal<string>('');
  const imgAOpacity = createSignal(0);
  const act1Opacity = createSignal(1);

  const sObj = createSignal<string>('');
  const imgBOpacity = createSignal(0);
  const act2Opacity = createSignal(0);

  const sFinal = createSignal<string>('');
  const act3Opacity = createSignal(0);

  const root = createRef<Layout>();
  const fadeOut = createSignal(1);

  view.add(
    <Layout ref={root} opacity={fadeOut}>
      <Layout
        layout
        direction={'column'}
        gap={12}
        x={-view.width() / 2 + 80}
        y={-view.height() / 2 + 80}
        offset={[-1, -1]}
      >
        <Txt
          text={sDate}
          fontFamily={FONT}
          fontSize={FONT_SIZE.date}
          fill={COLOR.text}
        />
        <Txt
          text={sLoc}
          fontFamily={FONT}
          fontSize={FONT_SIZE.body}
          fill={COLOR.text}
        />
      </Layout>

      <Layout
        layout
        direction={'column'}
        alignItems={'center'}
        gap={28}
        y={-40}
        opacity={act1Opacity}
      >
        <Txt
          text={sTitle}
          fontFamily={FONT}
          fontSize={FONT_SIZE.title}
          fontWeight={700}
          fill={COLOR.text}
        />
        <Layout opacity={imgAOpacity}>
          <AgedPhoto src={IMG.regele} width={960} height={540} />
        </Layout>
        <Layout layout direction={'column'} alignItems={'center'} gap={10}>
          <Txt
            text={sCtx1}
            fontFamily={FONT}
            fontSize={FONT_SIZE.small}
            fill={COLOR.dim}
          />
          <Txt
            text={sCtx2}
            fontFamily={FONT}
            fontSize={FONT_SIZE.small}
            fill={COLOR.dim}
          />
          <Txt
            text={sCtx3}
            fontFamily={FONT}
            fontSize={FONT_SIZE.small}
            fill={COLOR.dim}
          />
        </Layout>
      </Layout>

      <Layout
        layout
        direction={'row'}
        alignItems={'center'}
        gap={60}
        opacity={act2Opacity}
      >
        <Layout opacity={imgBOpacity}>
          <AgedPhoto src={IMG.turda} width={680} height={460} />
        </Layout>
        <Txt
          text={sObj}
          fontFamily={FONT}
          fontSize={FONT_SIZE.body}
          fill={COLOR.text}
        />
      </Layout>

      <Layout opacity={act3Opacity}>
        <Txt
          text={sFinal}
          fontFamily={FONT}
          fontSize={FONT_SIZE.body}
          fill={COLOR.text}
        />
      </Layout>
    </Layout>,
  );

  yield* waitFor(0.8);

  yield* retypeText(sDate, 'Data: 23 August 1944', CHAR_DELAY.fast);
  yield* glitch(root, 3);

  yield* imgAOpacity(1, 0.3);
  yield* typeText(sTitle, 'ROMANIA A INTORS ARMELE', CHAR_DELAY.slow);

  yield* typeText(sCtx1, 'Regele Mihai I a ordonat arestarea lui Ion Antonescu.', CHAR_DELAY.normal);
  yield* waitFor(0.3);
  yield* typeText(sCtx2, 'Romania a semnat armistitiul cu Aliatii.', CHAR_DELAY.normal);
  yield* waitFor(0.3);
  yield* typeText(sCtx3, 'Fostii tai aliati au devenit inamici peste noapte.', CHAR_DELAY.normal);

  yield* waitFor(1.8);

  yield* all(act1Opacity(0, 1.2), imgAOpacity(0, 1.2));

  yield* retypeText(sDate, 'Data: Septembrie 1944', CHAR_DELAY.fast);
  yield* typeText(sLoc, 'Locatie: Deasupra Turdei, Transilvania', CHAR_DELAY.normal);

  act2Opacity(1);
  yield* imgBOpacity(1, 0.8);
  yield* waitFor(0.3);
  yield* typeText(sObj, 'Obiectiv: Ataca fortele germane', CHAR_DELAY.normal);

  yield* waitFor(1.8);

  yield* all(act2Opacity(0, 1.2), imgBOpacity(0, 1.2));

  act3Opacity(1);
  yield* typeText(sFinal, 'Inamicul tau de ieri. Aliatul tau de ieri. Acelasi cer.', CHAR_DELAY.slow);

  yield* waitFor(2.0);
  yield* fadeOut(0, 1.5);
});

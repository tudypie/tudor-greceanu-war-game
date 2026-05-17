# IMAGE MANIFEST — Tudor Greceanu Cutscenes

All 7 slots currently use `la_karpovka.jpeg` as a placeholder. Replace each
`img_0X.jpg` with the real image at the **same path** and **same filename**.
No code changes are required when swapping — only the file contents.

**Recommended dimensions:** 1600x900 (16:9) or larger. Scenes downscale to
~35–60% of a 1920x1080 canvas, so anything ≥ 1280x720 looks clean.
**Format:** `.jpg` preferred. Photo-aged filter (grayscale + grain +
vignette) is applied at render time — supply raw originals.

---

## img_01.jpg — Cutscene 1 (Briefing)
- **Moment:** Mai 1943, aerodromul Makievska, briefing-ul de misiune.
- **Subiect ideal:** aerodrom de campanie de pe Frontul de Est, avion
  IAR-80 sau IAR-81 pe pista, pilot romanesc al EAR alaturi de aparat.
- **Plasare in scena:** dreapta ecranului, ~35% latime.
- **Apare la:** ~3–4s dupa start, dupa primele 3 randuri typewriter.

## img_02.jpg — Cutscene 2 (Intoarcerea, ACT 1)
- **Moment:** 23 August 1944, momentul intoarcerii armelor.
- **Subiect ideal:** Regele Mihai I in uniforma, sau o fotografie din
  ziua arestarii lui Ion Antonescu la Palatul Regal.
- **Plasare in scena:** centrat, ~50% latime, intra brusc dupa
  screen-shake + glitch.
- **Apare cu:** titlu mare "ROMANIA A INTORS ARMELE".

## img_03.jpg — Cutscene 2 (Intoarcerea, ACT 2)
- **Moment:** Septembrie 1944, batalia pentru Ardeal / Turda.
- **Subiect ideal:** infanterie romaneasca in Transilvania, tancuri
  romanesti pe front, sau coloane militare 1944.
- **Plasare in scena:** stanga ecranului, ~35% latime.

## img_04.jpg — Cutscene 3 (Dupa razboi, ACT 1 — Celebrarea)
- **Moment:** 1945–1947, perioada decoratiilor militare post-belice.
- **Subiect ideal:** pilot romanesc decorat, ceremonie militara,
  medalii WWII (Crucea de Fier, Ordinul Mihai Viteazul etc.).
- **Plasare in scena:** dreapta ecranului, fade-in lent (~1.2s).
- **Asociat:** text in culoare aurie #a89060.

## img_05.jpg — Cutscene 3 (Dupa razboi, ACT 2 — Arestarea)
- **Moment:** Aprilie 1949, arestarea de catre regimul comunist.
- **Subiect ideal:** inchisoare comunista romaneasca — Aiud, Sighet,
  Jilava — celula, ziduri, coridoare. Imagine grea, sobra.
- **Plasare in scena:** centrat, ~60% latime, intra BRUSC (fara fade).
- **Asociat:** flash alb inainte, titlu instant "ARESTAT" (fara typewriter).

## img_06.jpg — Cutscene 3 (Dupa razboi, ACT 3 — Fabrica)
- **Moment:** 1949–1952, munca silnica ca strungar.
- **Subiect ideal:** strung industrial romanesc, hala de fabrica
  comunista, muncitori la masini-unealta — anii '50.
- **Plasare in scena:** stanga ecranului, ~40% latime.

## img_07.jpg — Cutscene 3 (Dupa razboi, ACT 5 — Sfarsitul)
- **Moment:** finalul jocului, dupa eliberare 1964.
- **Subiect ideal:** cer deschis cu nori, sau un avion de epoca in
  zbor — ceva deschis, nedeterminat, melancolic. NU triumfal.
- **Plasare in scena:** centrat, ~45% latime, fade-in cel mai lent
  din tot jocul (2s).

---

## Swap procedure

1. Place the real image at `src/assets/img_0X.jpg` (overwrite the placeholder).
2. Keep the `.jpg` extension and the exact filename.
3. Re-run `npm run start` — Vite hot-reloads the new image automatically.
4. No code changes needed.

If you ever want a different extension (e.g. `.png`), update the import
path in `shared/constants.ts` (`IMAGE_PATHS`).

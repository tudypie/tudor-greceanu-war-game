# Plan Joc: „Pentru Cine?”

## Viziunea Anti-War

**Paradoxul central:** jocul te face să te simți erou — dogfight-uri epice, kills, adrenalină. Apoi îți arată că a fost fără sens. Ca *Full Metal Jacket*: prima jumătate te seduce, a doua te șochează.

## Răspunsuri la Întrebările Tale

### First person sau third person?
**Third person**, cu camera aproape de avion (ca *Ace Combat*).

**Motiv:** vezi avionul tău cu cocarda română, simți identitatea lui Tudor. First person ar fi mai imersiv, dar mult mai greu de implementat bine într-o săptămână.

### O misiune sau mai multe?
**O singură misiune lungă**, împărțită în 3 faze narative.

**Motiv:** mai puțin scope creep, mai mult impact emoțional concentrat.

### Game / feedback loop
Distrugi avioane inamice → primești confirmări radio entuziaste de la camarazi → contor de kills vizibil → muzică epică. Te simți erou. Apoi totul se oprește brusc.

## Structura Completă a Jocului

### ACT 1 — „Cerul e al Nostru”
*Gameplay principal: ~10 min*

**Setup:** scurtă cutscenă text/voiceover. Tudor primește ordinele. Decolează.

#### Gameplay
- Terrain procedural, cer plin de avioane.
- Waves de inamici, aliați în radio.
- HUD cu kills, altitude, speed.
- Muzică orchestrală eroică.
- Radio: camarazii te laudă după fiecare kill.

#### Feedback loop
1. Localizezi inamicul.
2. Îl urmărești.
3. Îl dobori.
4. Flash „KILL” + sunet triumfal.
5. Radio: „Bravo Tudor!”
6. Următorul inamic apare.
7. Repeat.

#### Momentul pivot
La un anumit număr de kills (de exemplu 10), jocul îți oferă un moment de pauză forțată: Tudor vede un avion inamic prăbușindu-se în flăcări, în detaliu.

Muzica se oprește 2 secunde, apoi continuă.

Jucătorul simte că ceva e în neregulă, dar merge mai departe.

### TRANZIȚIE 1 — „Victoria”
*Storytelling pur: ~2–3 min*

Secvență de ziare animate (2D, stil colaj):

- Titluri triumfale despre victorie.
- Fotografii de epocă stilizate.
- Apoi ziarul se schimbă treptat, cu titluri comuniste.
- Speech Ceaușescu (text + audio, dacă găsești arhivă).
- Ultima pagină de ziar: poza lui Tudor cu titlul **„DUȘMAN AL POPORULUI”**.

### ACT 2 — „Celula”
*Gameplay stealth/puzzle: ~5–7 min*

**Atmosferă:** opus total față de Act 1. Întunecat, claustrofobic, lent. Ca o altă specie de joc.

#### Gameplay
- Top-down sau first person în celulă.
- Tudor a planificat evadarea — tu execuți planul.
- Mecanică simplă: timing + observare gardieni.
- Tensiune crescândă.

#### Momentul cheie
Ești aproape afară. Apoi un sunet. Lumini. Gardieni.

Nu există buton de luptă. Tudor nu poate face nimic.

Fade to black.

**Text:** „A fost prins.”

#### De ce funcționează anti-war
Același om care doborâse zeci de avioane inamice este acum neputincios în fața propriului stat.

### TRANZIȚIE 2 — „Anii Pierduți”
*~5 secunde*

Simplu și brutal:

1951

.

.

.

1964

Sunet de ușă care se deschide.

### ACT 3 — „Interviul”
*Storytelling final: ~2 min*

Tudor bătrân, într-un fotoliu. Interviu filmat, stilizat în Unity, cu shader alb-negru și granulație.

Întrebarea intervievatorului apare ca text:

> „Pentru cine ați luptat, domnule Greceanu?”

Pauză lungă.

Tudor răspunde — tu, ca jucător, alegi dintre 3 variante. Nu există răspuns „corect”:

- „Pentru România.”
- „Pentru camarazii mei.”
- „Nu știu.”

Oricare ar fi răspunsul, urmează același final:

- fotografii reale de epocă;
- muzică lentă;
- credits.

**Ultima imagine:** Statuia Aviatorilor din București.

## Plan de Dezvoltare — 7 Zile

| Zi | Task |
|---|---|
| 1 | Setup Unity, terrain procedural de bază, avion controller (third person), sky system |
| 2 | AI avioane inamice (simplu — urmăresc + trag), AI aliați, spawn waves |
| 3 | HUD, kill counter, radio feedback, muzică + SFX, momentul pivot |
| 4 | Secvența ziare (UI animat 2D), speech Ceaușescu, tranziție la Act 2 |
| 5 | Celula — environment, gardieni simpli, mecanică de evadare, prinderea |
| 6 | Act 3 — interviul, choice system, credits, toate tranzițiile legate |
| 7 | Polish, bug fixing, build final, trailer de 1 minut |

## Sfaturi Tehnice Rapide

- **Avion controller:** folosește `Rigidbody` cu drag, nu physics complexe. Simplu e mai bine într-o săptămână.
- **Inamici AI:** SimpleBT sau state machine cu 3 stări: `patrol -> chase -> attack`.
- **Ziare:** UI Canvas animat cu Animator, imagini 2D în stil sepia.
- **Celula:** poți refolosi assets low-poly; lighting-ul face toată atmosfera.
- **Audio:** poți găsi speech Ceaușescu arhivat pe YouTube sau în arhive publice.
# Plan de Implementare — `PlaneController`

> Mecanica principală a jocului: zborul avionului lui Tudor Greceanu în Actul 1.
> Focus: **arcade**, nu simulator. Trebuie să se simtă bun în 10 minute, nu realist în 1000 de ore.
> Target: third person, camera la ~6m în spate și 2m deasupra (stil Ace Combat / Crimson Skies).

---

## 1. Obiective de Design

| Obiectiv | De ce |
|---|---|
| Controale intuitive în <30s | Jucătorul trebuie să simtă „pot face asta" imediat |
| Senzație de viteză și greutate | Avionul nu e un drone — are inerție, mișcarea trebuie să comunice masă |
| Manevre satisfăcătoare (loop, barrel roll, viraj strâns) | Dogfight-urile = momentul „erou" din Act 1 |
| Stall pe vertical, nu pe altitudine | Penalizăm zbor prost, dar fără bătut joc de jucător |
| Zero physics complexe (no lift coefficient real) | Trebuie scris în 1-2 zile, nu 1-2 săptămâni |

**Anti-obiective** (lucruri pe care nu le facem):
- Aerodinamică reală (lift/drag curves bazate pe AoA)
- Damage model pe componente (aripa stângă, motor, etc.)
- Combustibil, oxygen, G-LOC
- Trim, flaps, landing gear, takeoff/landing — Tudor decolează în cutscene

---

## 2. Modelul Fizic — „Arcade Fake-Physics"

Folosim `Rigidbody` cu `useGravity = false`. Toate forțele sunt calculate de noi în `FixedUpdate`. Gravitația o aplicăm manual ca să avem control complet (vrem ca planul să poată „pluti" la viteză mică, nu să cadă brusc).

### Forțe aplicate per frame fizic:

1. **Thrust** — împinge înainte pe `transform.forward`
   - Magnitudine = `currentThrottle * maxThrust`
   - `currentThrottle` se interpolează spre `targetThrottle` (input) cu `throttleResponseSpeed`

2. **Drag direcțional** — frânează mișcarea, mai mult lateral și vertical decât înainte
   - Descompunem velocitatea în `forward / right / up` (local space)
   - Aplicăm coeficienți diferiți pe fiecare componentă: forward=0.001, side=0.05, vertical=0.02
   - Acest „body drag" face avionul să meargă într-adevăr unde e îndreptat, nu să alunece

3. **Lift fake** — ține avionul în aer când are viteză suficientă
   - `lift = liftCoefficient * forwardSpeed^2 * cos(rollAngle)`
   - Aplicat pe `transform.up` (axa locală sus a avionului)
   - `cos(rollAngle)` => când avionul e pe muchie, pierde lift => coboară natural. Foarte important pentru feel.

4. **Gravitație manuală** — `Physics.gravity * gravityMultiplier` (multiplier ~1.0, dar tweakable)

5. **Auto-leveling slab** (opțional, behind a flag) — un mic torque care întoarce avionul spre orizontal când nu există input. Ajutor pentru jucători casuali; off pentru hardcore.

### Stall:
- Dacă `forwardSpeed < stallSpeed` și `pitchAngle > 30°` (nasul sus):
  - `lift` se taie la 0
  - Aplicăm un torque mic care împinge nasul în jos (recovery natural)
  - HUD afișează „STALL" clipind
  - Recuperare: scade nasul, prinde viteză.

---

## 3. Controale

Folosim **Unity Input System** (nu vechiul `Input.GetAxis`) — generăm un asset `PlaneInput.inputactions`.

| Acțiune | Tastatură/Mouse | Gamepad | Domeniu |
|---|---|---|---|
| Pitch (sus/jos) | W / S sau Mouse Y | Stick stâng Y | -1..+1 |
| Yaw (stânga/dreapta) | A / D | Stick stâng X | -1..+1 |
| Roll | Q / E sau Mouse X | Trigger L/R diferențial | -1..+1 |
| Throttle | Shift / Ctrl | Trigger drept / stâng | 0..1 |
| Fire | Spațiu / Click stânga | Trigger drept | bool |
| Boost / Afterburner (scurt) | Tab | A (south) | bool |

**Notă**: prima iterație ignoră Yaw — pitch+roll sunt suficiente pentru un dogfight arcade. Yaw îl adăugăm doar dacă e nevoie pentru aliniere precisă pe țintă.

Input → values curate (cu deadzone, smoothing exponential cu `inputResponseSpeed`) → folosite în `ApplyControlTorques()`.

---

## 4. Rotație — Torques, nu `transform.Rotate`

Aplicăm torques pe rigidbody, nu setăm rotație direct. Asta menține fizica consistentă și permite ciocniri / forțe externe să interacționeze natural.

```
pitchTorque = -pitchInput * pitchPower * transform.right
yawTorque   =  yawInput   * yawPower   * transform.up
rollTorque  = -rollInput  * rollPower  * transform.forward
rb.AddTorque(pitchTorque + yawTorque + rollTorque, ForceMode.Acceleration)
```

**Damping angular**: setăm `rb.angularDamping = 0.5` (sau aplicăm manual un counter-torque proporțional cu `angularVelocity`) ca să nu se rotească la nesfârșit după ce dai drumul la input.

**Scaling cu viteza**: torque-urile sunt slabe la viteză mică (control suprafețe ineficient) și mai puternice la viteză mare:
`effectivePower = power * Mathf.Clamp01(forwardSpeed / referenceSpeed)`

---

## 5. Structura Codului

Un singur script pentru iterația 1, apoi îl spargem dacă crește. Tot ce ține de input, fizică și state stays in `PlaneController` până când e prea mare.

### `Assets/Scripts/Plane/PlaneController.cs`

```
PlaneController : MonoBehaviour
├── [SerializeField] PlaneConfig config       // ScriptableObject cu toate tunable-urile
├── [SerializeField] Rigidbody rb
├── [SerializeField] Transform centerOfMass   // empty child, pus puțin în față
│
├── State public (read-only properties pentru HUD/AI)
│   ├── ForwardSpeed  (m/s)
│   ├── Altitude      (m)
│   ├── Throttle      (0..1)
│   ├── IsStalling    (bool)
│   └── Velocity      (Vector3)
│
├── Awake()
│   └── rb.centerOfMass = centerOfMass.localPosition
│
├── Update()
│   └── ReadInput()  // doar citește, nu aplică
│
├── FixedUpdate()
│   ├── ApplyThrust()
│   ├── ApplyLift()
│   ├── ApplyDrag()
│   ├── ApplyGravity()
│   ├── ApplyControlTorques()
│   ├── ApplyAutoLevel()      // dacă config.autoLevelEnabled
│   └── UpdateStallState()
│
└── OnDrawGizmos()  // vectori velocity, forward, thrust — esențial pentru tuning
```

### `Assets/Scripts/Plane/PlaneConfig.cs`

ScriptableObject. **Toate** valorile numerice trăiesc aici, nu în cod, ca să putem face A/B în Inspector fără recompile.

Câmpuri grupate:
- **Thrust**: `maxThrust`, `boostMultiplier`, `boostDuration`, `throttleResponseSpeed`
- **Lift & Drag**: `liftCoefficient`, `dragForward`, `dragSide`, `dragVertical`, `gravityMultiplier`
- **Control**: `pitchPower`, `yawPower`, `rollPower`, `controlResponseSpeed`, `referenceSpeedForControl`
- **Stall**: `stallSpeed`, `stallPitchAngle`, `stallRecoveryTorque`
- **Auto-level**: `autoLevelEnabled`, `autoLevelStrength`

Valori inițiale recomandate (le tunăm la testare):
```
maxThrust = 80
liftCoefficient = 0.5
pitchPower = 25, rollPower = 40, yawPower = 8
stallSpeed = 15
gravityMultiplier = 1.0
```

### `Assets/Scripts/Plane/PlaneInputReader.cs` (opțional, doar dacă PlaneController crește)

Separă citirea Input System de logica de fizică. Expune `PitchInput`, `RollInput`, etc. ca proprietăți.

---

## 6. Cameră

Script separat: `Assets/Scripts/Plane/PlaneFollowCamera.cs`.

- Lerp poziție către `target.position - target.forward * distance + target.up * height` cu `positionLerpSpeed`
- SLerp rotație către `Quaternion.LookRotation(target.position - cam.position, target.up)` cu `rotationLerpSpeed`
- Adaugă un mic offset bazat pe `target.GetComponent<Rigidbody>().angularVelocity` (camera „rămâne în urmă" la viraj brusc — feel arcade)
- FOV dinamic: `60 + (forwardSpeed - cruiseSpeed) * 0.2`, clamp [55, 85] — accentuează viteza

Nu o face copilul avionului — vrem damping/lag controlat.

---

## 7. Iterații pe Zile (din planul de 7 zile)

### Ziua 1 — Setup și mișcare brută
- [ ] Asset `PlaneInput.inputactions` cu pitch/roll/yaw/throttle
- [ ] Prefab `Player_Plane` cu Rigidbody, MeshCollider (sau colliders primitive) și un placeholder mesh (cube alungit până avem model)
- [ ] `PlaneController.cs` — thrust + control torques + drag direcțional
- [ ] `PlaneConfig.asset` cu valorile inițiale
- [ ] `PlaneFollowCamera.cs`
- [ ] Test playable: avionul zboară, virează, dă roll. **Nu trebuie să fie frumos, trebuie să zboare.**

### Ziua 2 — Polish fizic
- [ ] Lift + stall logic
- [ ] Auto-level slab (cu toggle)
- [ ] Tuning sesiune 30 min — fixează `pitchPower`, `rollPower`, `maxThrust` până „simți" bine
- [ ] Gizmos de debug în scene view
- [ ] Boost (afterburner) — multiplier temporar pe thrust, cooldown 5s

### Ziua 3 (mai târziu) — Combat hooks
- `PlaneController` expune `ForwardSpeed`, poziție, etc. → `WeaponSystem` și `PlaneHealth` se atașează separate. **Nu adăugăm arme în PlaneController.**

---

## 8. Cum testăm

**Manual / feel**:
1. Pornesc o scenă goală cu un sky și un plan pentru sol.
2. Spawnez 5 spheres mari în aer ca repere de manevră.
3. Verific:
   - Loop complet 360° fără pierdere severă de altitudine la `maxThrust`
   - Barrel roll lin în <2 secunde la viteză de cruise
   - Stall: nas sus + throttle 0 → după ~3s începe să cadă, recovery natural când nasul coboară
   - Sub `stallSpeed` cu nasul drept, avionul **coboară**, nu rămâne în loc
   - Schimbarea de la pitch=+1 la pitch=-1 nu e instantanee (smoothing funcționează)

**Validare cod** (dacă rămâne timp):
- Edit-mode test simplu: instanțiez `PlaneController`, simulez 60 FixedUpdate, verific că `Velocity.magnitude > 0` și că un torque pe pitch schimbă `transform.forward.y`.

---

## 9. Riscuri și Mitigări

| Risc | Mitigare |
|---|---|
| Avionul se simte „flutters" / oscilează | Crește damping angular, scade `controlResponseSpeed`. Verifică să nu aplici torque și rotație manuală în paralel. |
| Stall e prea agresiv / nu se mai recuperează | Adaugă un floor la lift = 0.1 * normal, ca avionul să nu pice ca o cărămidă |
| Mouse input se simte prost | Default rămâne tastatura. Mouse pe sticks doar dacă rămâne timp în ziua 7 (polish). |
| FOV dinamic dă rău de mașini | Toggle în settings, default ON dar cu factor mic (0.1, nu 0.2) |
| Cameră intră prin geometria avionului la pitch extrem | Clamp distanța minimă în follow camera; raycast scurt din target spre cameră ca să detecteze blocaje |

---

## 10. Definition of Done — Ziua 2 seara

Avionul **e gata** când pot:
1. Decola dintr-un spawn în aer, accelerând la viteză de cruise în <5s
2. Face un loop complet și un barrel roll fără să mor de frustrare
3. Pune throttle la 0, nasul sus, intra în stall, recupera prin coborârea nasului
4. Filmez un clip de 20s în care zborul „arată ca un joc de zbor", nu ca un cube cu Rigidbody

Dacă oricare din punctele 1-4 nu trece la sfârșitul zilei 2, **nu** avansez la AI inamic — tunez în continuare. Tot restul jocului depinde de cât de bine se simte zborul.

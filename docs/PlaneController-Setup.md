# PlaneController — Setup în Unity

Pași minimi ca să zbori avionul după ce am scris codul.

## 1. Crează `PlaneConfig` asset

În Project window:
- Right-click → `Create / WarGame / Plane Config`
- Numește-l `PlaneConfig_Default`
- Lasă valorile default (sunt deja tunate ca punct de pornire)
- Salvează-l în `Assets/Settings/`

## 2. Crează prefab-ul avionului

În scenă:
1. `GameObject / Create Empty` → numește-l `Plane_Player`
2. Adaugă un copil mesh placeholder: `GameObject / 3D Object / Cube`, scale pe `(3, 0.5, 6)` (alungit pe Z). Atribuie un material vizibil (galben/roșu).
3. Pe `Plane_Player` adaugă:
   - **Rigidbody** (auto-adăugat de `PlaneController`, dar verifică). Setează `Mass = 1`, `Use Gravity = OFF`. PlaneController.Reset() face deja asta dar ne asigurăm.
   - **BoxCollider** care învelește mesh-ul
   - **PlaneInputReader** → drag `PlaneConfig_Default` în `config`
   - **PlaneController** → drag `PlaneConfig_Default` în `config`, drag componenta `PlaneInputReader` în `input`
4. Crează un copil `CenterOfMass` (GameObject gol), pune-l la `(0, 0, 0.3)` — puțin în față de centru. Drag-uiește-l în câmpul `Center Of Mass` al PlaneController.
5. Drag `Plane_Player` din ierarhie în `Assets/Content/Prefabs/` ca să-l faci prefab.

## 3. Setează camera

1. Selectează `Main Camera` din scenă
2. Adaugă componenta **PlaneFollowCamera**
3. În inspector:
   - `Target` → `Plane_Player` (transform-ul lui)
   - `Plane` → componenta `PlaneController` de pe `Plane_Player`
   - `Cam` → componenta `Camera` (același obiect)
4. Lasă valorile default.

## 4. Crează un mediu de test

Scenă minimă pentru tuning:
1. Spawn `Plane_Player` la `(0, 200, 0)` cu `rotation = (0, 0, 0)` (în aer, orientat spre +Z)
2. Adaugă un `Terrain` sau un `Plane` mare la `y = 0` ca referință de sol
3. Pune câteva sfere mari (`Scale = 10`) la `(50, 200, 100)`, `(-50, 250, 200)`, etc. — repere de manevră

## 5. Controale default

| Acțiune | Tastatură | Gamepad |
|---|---|---|
| Pitch up/down | S / W | Stick stâng Y (invers) |
| Roll stânga/dreapta | A / D | Stick stâng X |
| Yaw stânga/dreapta | Q / E | Trigger L / R |
| Throttle sus/jos | Shift / Ctrl | Stick drept Y |
| Boost | Tab | A (south) |
| Foc | Space / Click | RB |

## 6. Tuning rapid

Dacă avionul:
- **Se rotește prea greu** → crește `Pitch Power` / `Roll Power` (25/40 → 35/55)
- **Oscilează / fluttering** → crește `Angular Damping` (1.5 → 2.5)
- **Cade prea repede** → crește `Lift Coefficient` (0.5 → 0.7) SAU scade `Gravity Multiplier`
- **Nu pierde altitudine în viraj** → scade `Lift Coefficient` (lift cosθ devine mai pronunțat)
- **Stall imposibil de recuperat** → crește `Stall Lift Floor` (0.1 → 0.2) și `Stall Recovery Torque`
- **Pare „înghețat" la viteză mică** → scade `Reference Speed For Control` (25 → 15)

Toate sunt în `PlaneConfig_Default`. Schimbările au efect imediat în Play mode.

## 7. Definition of Done (Ziua 2)

Verifică manual:
- [ ] Loop complet 360° fără să prăbușești
- [ ] Barrel roll lin în <2s
- [ ] Throttle 0 + nas sus → după ~3s intră în STALL → nasul cade → recuperare
- [ ] Auto-level: dă drumul la roll input → avionul se nivelează singur
- [ ] Cameră urmărește fără să intre în mesh
- [ ] FOV crește la viteză mare (vizibil când pornești boost)

Dacă toate trec, e gata pentru AI inamic.

# Game Design Document

## 1. Game Overview & Concept

### 1.1 Genre
Arcade Air-Combat Flight Action — single-player, mission-based WWII dogfighter.

### 1.2 Concept
A fast-paced arcade flight game where you fly a WWII fighter on the Eastern Front. You take off from a friendly airfield, intercept incoming enemy squadrons from every direction, and fight outnumbered against AI fighters and bombers across a short campaign of missions — first defending your airfield, then going on the offensive to clear the skies.

### 1.3 Inspirations

SimpleFlightController (bzgeb, GitHub: https://github.com/bzgeb/SimpleFlightController.git) — the starting point for the core flight controller, before barrel rolls, banking turns and the rest of the model were built on top of it.

Top Gun: Maverick — inspiration for mission pacing and the "fly the canyon, get the shot, get out" feel of the engagements.

IL-2 Sturmovik / War Thunder — genre touchstone for WWII prop-fighter combat: bomber strike runs on a ground target, energy-based dogfighting, no overheating the guns.

Tudor Greceanu — the Romanian WWII fighter ace the project is named for, and the historical anchor for the Eastern-Front setting and aircraft (P-39 Airacobra, Messerschmitt Bf 109).

### 1.4 Game Pillars

**Skill-Based Air Combat:** The flight model is easy to fly but rewards energy management. Climbing bleeds airspeed, diving builds it back, a slow nose-high attitude stalls the wing, and the engine runs out of breath at the service ceiling. Mastery is about trading speed and altitude well, then putting the reticle exactly where the enemy will be.

**Outnumbered & Outgunned:** You and a handful of allies fight squadrons of 20–30. Tension comes from a fragile objective on a wave timer, enemies attacking from all four compass directions at once, and a swarm that is deliberately capped and distracted so it can pressure you without dogpiling you into an unwinnable corner.

**An Authored Theatre of War:** The world is a procedurally generated landscape with a carved river valley, a hard map boundary and service ceiling, and missions staged as scripted compass-direction waves — a believable, replayable battlefield to fight over rather than an empty arena.

## 2. Gameplay

### 2.1 Objective
Survive and complete every mission in the campaign.

- **Mission 1 — Makievska (airfield defense):** Keep your airfield alive while shooting down every incoming wave. The mission is won when the final wave is cleared with the airfield still standing; it is lost if the airfield's integrity reaches zero or you die.
- **Mission 2 — Vest (clear the sky):** Destroy every enemy squadron. The mission is won once every enemy spawner has emitted its full complement and none remain alive, with the player still flying.

### 2.2 Core Loop

**Takeoff Phase (Mission 1):** Start parked on the runway. Spool the throttle, taxi and steer down the strip, and at rotation speed pull the nose up and fly off — there is no automatic liftoff, you have to fly it off the ground.

**Engagement Phase:** Waves of enemies spawn from the North, South, East and West and converge on the battlefield. Some aircraft are strikers that ignore you and run bombing passes on the airfield — those have to be shot down before they reach it. The rest are hunters that come after you and your allies.

**Combat Phase:** Trail the enemy with the auto-following chase camera, place the free-aim reticle, let the soft lock-on settle, and fire — while managing gun heat, throttle/boost energy, the altitude ceiling and the map boundary.

**Resolution:** Clearing a wave triggers the next one after a short delay. Clearing the final wave (objective intact) completes the mission, plays a cutscene, and loads the next mission. Death, or losing the protected objective, fails the mission and restarts it.

### 2.3 Mechanics

**Flight Model:** Thrust is interpreted directly as cruise speed. Holding the boost input spools the throttle from cruise up toward war-emergency power and firms up handling as it spools. Climbing bleeds airspeed through drag and diving regains it; too slow with the nose too high stalls the wing and forces the nose down until speed recovers. A service ceiling and a scene-placed map-boundary box take control away and push the plane back when it strays too high or too far. Banking turns the plane (bank-to-turn coupling), with auto-level when the stick is centered. The whole world runs at roughly one-third speed scale for physics stability, surfaced back to real km/h only on the HUD.

**Takeoff & Taxi (player, Mission 1):** A self-contained ground model separate from flight — throttle is a slow lever, the nosewheel steers like a car once rolling, and only at rotation speed does pulling back lift the nose. Control authority eases in over the first few seconds of the climb so the plane flies off the strip smoothly instead of snapping to full agility.

**Gunnery:** A mouse-driven free-aim reticle (it moves the aim point, not the camera). Bullets converge on the world point under the reticle, so a target under the crosshair is a hit regardless of where the nose points. A soft lock-on box snaps the crosshair onto the nearest in-box hostile after a short acquire time. Guns build heat and lock out on overheat, then cool back down — sustained fire has to be paced.

**Camera:** A third-person chase cam that auto-trails behind the plane's heading, matches part of its climb, and leans toward the reticle so the target stays framed. Holding the right mouse button pans a free-look offset around the aircraft (which freezes the reticle and aim so the guns don't slew with the view). A cockpit/first-person view can be toggled.

**Enemy & Ally AI:** An abstract AI controller with enemy and ally subclasses. AI fly with collision avoidance, engage/disengage states (they will break off to give the player a chance), a global cap on how many can attack the player at once, and per-aircraft distraction so the swarm wanders instead of dogpiling. Gunnery is accurate but softened by a tunable aim-noise knob that is the single difficulty dial; AI also respect a terrain-relative altitude floor so they never fly into the hills.

**Objective Systems:** *Airfield* — a protect-target with an integrity readout that fails the mission at zero. *WaveDirector* — an ordered list of waves, each a set of compass spawn groups, splitting each group into airfield strikers and player hunters. *EliminationObjective* — the Mission 2 "clear every enemy spawner" win condition.

**HUD & Feedback:** Speed/altitude readout, health bar, gun-heat gauge, minimap, ceiling and boundary proximity warnings, hit markers, a contextual takeoff/controls tutorial, and a global HUD toggle. Damage drives a crash model with fireballs and camera shake.

**Fun Mode (sandbox):** A cheat layer dropped on any scene object — instakill nukes with mushroom fireballs and bullet-time, OP guns, warp speed, chaos audio, and bullets that erupt the terrain into flying voxel chunks. It operates on runtime clones of the stat assets, so the shipped balance and AI are never touched.

### 2.4 Controls
- **W / S** = pitch (nose up / down)
- **A / D** = roll
- **Q / E** = yaw / rudder (steer on the ground, barrel roll in the air)
- **Space** = boost / throttle (accelerate on the ground)
- **S (on the ground, at rotation speed)** = rotate and take off
- **Mouse** = move the free-aim reticle
- **Left Mouse Button** = fire guns
- **Hold Right Mouse Button** = free-look camera pan (freezes the reticle/aim)
- **C** = toggle cockpit / chase camera
- The plane restarts the mission automatically on death.

## 3. Technical Design

### 3.1 Engine & Tools

- Unity (Universal Render Pipeline) and C#, with the new Unity Input System
- Blender for importing and decimating high-poly aircraft and prop models
- Sketchfab and Fab for 3D models; ambientcg.com for CC0 textures
- Pixabay for sound effects and music; Audacity for mixing

### 3.2 Prototyping

The first step was to implement the core mechanic: the aircraft. I started by looking for a flight controller on GitHub and found bzgeb's SimpleFlightController, a good starting point. I took the code, brought it into Unity, and then built more mechanics on top of it — barrel rolls and left/right banking turns.

I exposed the handling values so I could tune the feel: thrust (plane speed), an agility multiplier applied while boosting, pitch increase speed (how fast you go up/down), roll increase speed (how fast you bank left/right), barrel roll speed (Q/E), roll auto-level speed (how quickly the plane returns to its default rotation — left at 0 because it felt like more control without it), and bank turn speed (how hard it pulls toward the direction you're pointed). I made a build, tested the mechanic, and adjusted the values until it felt right. I sent the build to a few friends for feedback and tweaked the numbers based on their suggestions. With the core mechanic done, it was time for the next step: shooting and enemies.

Team-versus-team combat surfaced a lot of problems I hadn't anticipated. The AI collided with each other and with the environment because they had no avoidance logic, so I added it. They also chased the player relentlessly, so I added a new AI state that breaks them off temporarily to give you a chance to fight back. To keep all of this manageable I grouped the stats into ScriptableObjects, and built a terrain generator for the battlefield.

### 3.3 Textures & Models

For the aircraft I used Soviet and German WWII fighters — a P-39 Airacobra / Yak-15 for the player side and a Messerschmitt Bf 109 for the enemy — sourced from Sketchfab and Fab and decimated in Blender. For the Makievska airfield scene I used a free "Aircraft Hangar" model and concrete-fence models from Sketchfab. The landscape itself is not modelled by hand: a custom terrain generator produces the heightfield, bakes a four-layer splatmap, and carves a river valley along an authored spline that doubles as the flight corridor.

### 3.4 Audio

I implemented a small audio layer (engine loop, weapons, explosions, music) and sourced the sound effects and music from Pixabay:

- Sound Effect by [freesound_community](https://pixabay.com/users/freesound_community-46691455/) from Pixabay
- EAS / air-raid alarm — Jeremay Jimenez (Pixabay)
- Explosion — Finnegan Cramer (Pixabay)
- Explosions — DRAGON-STUDIO (Pixabay)
- Metal impact — Spin Opel (Pixabay)
- Music — Dmitry Taras (Pixabay)
- Music — Dmitrii Kolesnikov (Pixabay)

### 3.5 Final Adjustments

With the core combat working, I built out the campaign frame and polish. A single source of truth drives the scene-flow chain — MainMenu → cutscene → Makievska → cutscene → Vest → cutscene → MainMenu — with a skippable video/cutscene loader between missions. I added the main menu, a contextual takeoff and controls tutorial, a minimap, a HUD toggle, and a Fun Mode sandbox for messing around.

The last pass was balance, mostly from playtest feedback. The AI was retuned to be accurate but beatable, with a single aim-noise difficulty knob; a global attacker cap plus per-aircraft distraction lets a handful of planes survive against 20–30; the AI altitude floor was made terrain-relative so they stop flying into the hills; and a scene-placed map-boundary box now defines both the horizontal turn-back limit and the altitude ceiling so the fight stays where it should.

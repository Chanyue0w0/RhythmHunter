# Otter Aquarium Prototype scenes

- `OtterAquarium.unity` is the top-down sea otter movement, surface transition, slide, and water VFX prototype.
- `OtterAquariumCombat.unity` uses the PirateFightScene battle staging and its Cinemachine combat/boss camera transition.
- `OtterShellBeatLab.unity` is a fixed-camera, one-button call-and-response playtest. The crab demonstrates a rhythm and the otter repeats it one bar later.
- `OtterZooGoblinDemo1.unity` is the first music-authored battle demo for `Goblin Patrol`: the axe goblin gives one of two fixed beginner cues, then the otter responds.
- The combat scene intentionally removes pirate ocean waves, animated ocean surfaces, ship motion, and the ocean tuning panel.
- `Assets/PirateOceanPrototype/Scenes/PirateFightScene.unity` remains the source reference. Continue future scene work in this folder.

## Shell Beat Lab

- **Audio status:** cue, hit, miss, and success sound effects have not been added yet. Their FMOD event fields are intentionally left empty for later production integration.
- Open `OtterShellBeatLab.unity` and press Play. Use Space, Enter, left click, or the gamepad south button.
- The prototype uses `event:/Combat soundtracks/Combat 01` at 100 BPM as temporary music.
- Edit `Assets/OtterAquariumPrototype/Data/OtterShellBeatLevel.asset` to replace the optional cue, hit, miss, and success FMOD event paths.
- The chart uses 480 PPQ and contains 11 two-bar call/response phrases. The final two phrases select Assist, Standard, or Challenge patterns from recent performance without changing judgement windows.
- Rebuild or validate from `Rhythm Hunter > Otter Aquarium > Build/Validate Shell Beat Lab`.

## Rhythm level authoring

- Open `Rhythm Hunter > Otter Aquarium > Open Rhythm Level Editor`.
- Create or duplicate a level asset, then use a Chinese rhythm preset, a quick progression template, or the 16-step grid.
- Each phrase supports fixed timing or Assist / Standard / Challenge adaptive variants.
- `Chart Offset (ms)` shifts the full authored chart against the first FMOD timeline beat without changing input calibration.
- Apply the selected asset to `OtterShellBeatLab.unity` with `Apply To Test Scene` in the editor window.
- Full JSON can round-trip level data. Producer CSV expands cue and response events into bars, beats, PPQ ticks, absolute beats, and timeline seconds.

## Zoo Goblin Demo 1

- Open `OtterZooGoblinDemo1.unity`, press Play, and defend with Space, Enter, left click, or gamepad south. Press R to restart.
- FMOD music is `event:/ZooGoblinFight/BGM/Goblin Patrol`, authored at 120 BPM / 4-4 with its Tempo Marker at 0 ms. Gameplay resolves after 33 complete bars (66 seconds), leaving the final second as an audio tail.
- Demo1 uses two primitive reactions: `X _ → X'` and `X X X _ → X' _ X' _ X'`. Later phrases combine those same primitives without adding another input rule.
- `Warning` plays on each enemy `X`; `AxeGoblin_NormalAttack` plays on `_`; a successful Perfect or Good block plays `BeatTapping`. Miss/result-specific SFX fields remain available for later production audio.
- Demo1 uses `zoo_fightingbackground.png` at full color with a light contrast wash. The Goblin stands on the left sand at X=-4.65, while the otter occupies the right pool at X=4.65; both are scaled to 72% so they sit inside the environment rather than covering it. On `X`, the goblin holds `attack_3` (raised axe); on `_`, it switches to `attack_4` and spawns the rotating `GoblinFlyingAxe` prefab. Triple catches use alternating beats: `X' _ X' _ X'`.
- Projectile motion uses the reusable `RhythmTimelineProjectile` component. Prefabs receive launch and target FMOD timeline positions rather than a hard-coded travel duration, so constant BPM and chart-target changes automatically retime their flight. Mid-song tempo maps remain a separate future feature.
- Demo1 BGM volume is set to 55% in its level asset so warning and attack cues remain readable.
- The otter has exactly 3 HP. Every unresolved attack is a Miss and deals exactly 1 damage; Perfect and Good block all damage, while Perfect also shows a counter response.
- The Goblin Patrol chart has 15 phrases. Bars 5–9 teach three Singles; bar 11 introduces two `X _` attacks in one measure; bar 13 introduces alternating-beat Triple catches; bars 17–31 alternate denser Double Single and Triple → Single combinations; bar 33 closes with a Triple before the audio tail. Victory waits until its final catch has been judged.
- Rebuild, validate, or run the FMOD play test from `Rhythm Hunter > Otter Aquarium > ... Zoo Goblin Demo 1`.

## Aquarium area authoring

- Open `Rhythm Hunter > Otter Aquarium > Open Area Authoring` to create movement zones and solid obstacles.
- Surface zones are trigger colliders: deep water priority 50, shallow water priority 20, and walkable land priority 100.
- Solid obstacles are non-trigger polygon colliders used for rocks, walls, and blocking decorations.
- Select any polygon and use `PolygonCollider2D > Edit Collider` to adjust its shape in the Scene view.
- After editing water or land zones, press `Rebake Water Mask From Zones` so the animated water overlay follows the new polygons.
- `Rebuild Prototype Scene...` requires confirmation because rebuilding replaces all hand-authored scene geometry.

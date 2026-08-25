# Otter Aquarium Prototype scenes

- `OtterAquarium.unity` is the top-down sea otter movement, surface transition, slide, and water VFX prototype.
- `OtterAquariumCombat.unity` uses the PirateFightScene battle staging and its Cinemachine combat/boss camera transition.
- `OtterShellBeatLab.unity` is a fixed-camera, one-button call-and-response playtest. The crab demonstrates a rhythm and the otter repeats it one bar later.
- `OtterZooGoblinDemo1.unity` is the first music-authored battle demo for `Otter's Revenge`: the axe goblin warns with a rhythm, optionally leaves a gap, then attacks with the same pattern.
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
- FMOD music is `event:/ZooGoblinFight/BGM/Otter's Revenge`, authored at 153.1 BPM / 4-4 with a +49 ms music-grid alignment.
- `Warning` plays on every authored warning hit: a pattern such as `x_xx` plays it exactly three times, while rests stay silent. `AxeGoblin_NormalAttack` plays each real attack. Perfect, Good, and Miss result-SFX fields remain intentionally empty for later production audio.
- Demo1 BGM volume is set to 55% in its level asset so warning and attack cues remain readable.
- The otter has exactly 3 HP. Every unresolved attack is a Miss and deals exactly 1 damage; Perfect and Good block all damage, while Perfect also shows a counter response.
- The chart contains quarter-note, rest, safe eighth-note syncopation, delayed-response, and two-bar phrases arranged across the analyzed song sections. Edit `Assets/OtterAquariumPrototype/Data/OtterZooGoblinDemo1Level.asset` for later tuning.
- Rebuild, validate, or run the FMOD play test from `Rhythm Hunter > Otter Aquarium > ... Zoo Goblin Demo 1`.

## Aquarium area authoring

- Open `Rhythm Hunter > Otter Aquarium > Open Area Authoring` to create movement zones and solid obstacles.
- Surface zones are trigger colliders: deep water priority 50, shallow water priority 20, and walkable land priority 100.
- Solid obstacles are non-trigger polygon colliders used for rocks, walls, and blocking decorations.
- Select any polygon and use `PolygonCollider2D > Edit Collider` to adjust its shape in the Scene view.
- After editing water or land zones, press `Rebake Water Mask From Zones` so the animated water overlay follows the new polygons.
- `Rebuild Prototype Scene...` requires confirmation because rebuilding replaces all hand-authored scene geometry.

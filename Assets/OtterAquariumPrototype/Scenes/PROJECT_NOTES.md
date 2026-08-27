# Otter Aquarium Prototype scenes

- Project startup is passive: legacy FightDemo, RhythmDemo, and PirateOcean scenes are no longer built, opened, validated, or played automatically. Their manual editor menu commands remain available.
- Build Settings contains only the four scenes under `Assets/OtterAquariumPrototype/Scenes`.

- `OtterAquarium.unity` is the top-down sea otter movement, surface transition, slide, and water VFX prototype.
- `OtterAquariumCombat.unity` uses the PirateFightScene battle staging and its Cinemachine combat/boss camera transition.
- `OtterShellBeatLab.unity` is a fixed-camera, one-button call-and-response playtest. The crab demonstrates a rhythm and the otter repeats it one bar later.
- `OtterZooGoblinDemo1.unity` is the shared music-authored battle Scene. Its characters, background, UI, input, SFX, and projectile presentation stay fixed while the selected Demo1 LevelData supplies the song and complete chart.
- `OtterZooGoblinDemo1Level.asset` keeps the `Goblin Patrol` chart, while `OtterZooGoblinOtterVsLevel.asset` independently keeps the 120 BPM / 32-bar `event:/ZooGoblinFight/BGM/Otter vs` chart. Editing either asset never changes the other.
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

- Open `OtterZooGoblinDemo1.unity`, press Play, and defend with Space, Enter, left click, or gamepad south. Press R to restart. Press H to hide or restore the rhythm-detection and judgement HUD while keeping the title and failure counter visible. Before Play, select `Demo1CombatController` and clear `Show Diagnostic HUD On Start` on `Otter Goblin Demo1 Presenter`; the same HUD hides immediately in Edit Mode and remains hidden when Play begins.
- FMOD music is `event:/ZooGoblinFight/BGM/Goblin Patrol`, authored at 120 BPM / 4-4 with its Tempo Marker at 0 ms. Gameplay resolves after 33 complete bars (66 seconds), leaving the final second as an audio tail.
- Demo1 uses two fixed primitive reactions: short `X _ → X'` and triple `X X X _ → X' _ X' _ X'`. Their internal timing never changes; difficulty grows only by chaining the two bases into longer and denser groups.
- `Warning` plays on each enemy `X`; `AxeGoblin_NormalAttack` plays on the short pattern's `_`, while the triple pattern's rest stays silent to avoid a false throw cue. A successful Perfect or Good block plays `BeatTapping`. Miss/result-specific SFX fields remain available for later production audio.
- Demo1 uses `zoo_fightingbackground.png` at full color with a light contrast wash. The Goblin stands on the left sand at X=-4.65, while the otter occupies the right pool at X=4.65; both are scaled to 72% so they sit inside the environment rather than covering it. On `X`, the goblin holds `attack_3` (raised axe); on `_`, it switches to `attack_4` and spawns the rotating `GoblinFlyingAxe` prefab. Triple catches use alternating beats: `X' _ X' _ X'`.
- Projectile motion uses the reusable `RhythmTimelineProjectile` component. Prefabs receive launch and target FMOD timeline positions rather than a hard-coded travel duration, so constant BPM and chart-target changes automatically retime their flight. Mid-song tempo maps remain a separate future feature.
- Each Demo1 phrase now exposes one projectile Prefab slot for every player catch `X'` in the Rhythm Level Editor. Leave a slot empty to use `BeatProjectiles/axe`, or drag in any Prefab that contains `RhythmTimelineProjectile`; triple and combined phrases keep their catch-order mapping.
- The objects under the shared Scene's inactive `ItemTMP` root are available as reusable Prefabs in `Assets/OtterAquariumPrototype/Prefabs/BeatProjectiles`. Toggle `Rotate During Flight` on each Prefab's `RhythmTimelineProjectile` component to control whether that object spins. After adding another sprite object under `ItemTMP`, use `Rhythm Hunter > Otter Aquarium > Create Missing Beat Projectile Prefabs From ItemTMP` if Unity has not generated it automatically.
- Triple axes are pre-scheduled against their individual catch targets. Each axe becomes visible exactly one beat before its own `X'`, matching the single-axe reaction time while preserving `X' _ X' _ X'` input spacing.
- Demo1 BGM volume is set to 55% in its level asset so warning and attack cues remain readable.
- Demo1 has no HP or death condition. Every unresolved axe increments `FAILURES`; the song and chart continue regardless of the count. Perfect and Good avoid a failure, while Perfect also shows a counter response.
- Pressing defend with no attack inside the Good window triggers `BeatMiss`, adds one Extra, and locks defend input for half a music beat. Inputs during that lock are ignored; the stray press itself deals no damage, but an incoming projectile can still become a normal damaging Miss.
- The Goblin Patrol chart has 24 phrases. Bars 3–9 teach the two bases; phrases connect continuously from Bar 10 through the end of Bar 26; Bars 27–28 are a full rest; Bars 29–33 increase pressure through short/triple combinations while preserving the same fixed reactions. Victory waits until the final catch has been judged.
- `Rhythm Hunter > Otter Aquarium > Open Rhythm Level Editor` accepts both Shell Beat `OtterRhythmLevelData` and Demo1 `OtterGoblinDemo1LevelData`. Demo1 mode exposes level/combat/FMOD settings, Phrase start bar plus within-bar sixteenth offset, a fixed-pattern restore button, and a 120-tick three-lane stepper for warning `X`, fixed axe `_`, and player catch `X'`. Editable lanes must retain the attack type's required hit count.
- Select either Demo1 LevelData and press `套用並開啟共用 Scene` in its Inspector, or `套用至共用 Demo1 Scene` in the rhythm editor. This updates the shared Scene's LevelData, FMOD event, volume, start delay, title, BPM/status display, and chart together. The Runner also resynchronizes FMOD before playback begins, preventing a new chart from playing against the previously selected song.
- Rebuild, validate, or run the FMOD play test from `Rhythm Hunter > Otter Aquarium > ... Zoo Goblin Demo 1`.

## Aquarium area authoring

- Open `Rhythm Hunter > Otter Aquarium > Open Area Authoring` to create movement zones and solid obstacles.
- Surface zones are trigger colliders: deep water priority 50, shallow water priority 20, and walkable land priority 100.
- Solid obstacles are non-trigger polygon colliders used for rocks, walls, and blocking decorations.
- Select any polygon and use `PolygonCollider2D > Edit Collider` to adjust its shape in the Scene view.
- After editing water or land zones, press `Rebake Water Mask From Zones` so the animated water overlay follows the new polygons.
- `Rebuild Prototype Scene...` requires confirmation because rebuilding replaces all hand-authored scene geometry.

## Pixel Craft VFX URP

- The project default pipeline uses `Assets/Settings/Renderer2D.asset` at Renderer Index 0. For gameplay scenes, choose the package Prefabs whose names start with `VFX_2D_`; they reference the package's `Shared/2D Materials` and shaders with a `Universal2D` pass.
- `Assets/Settings/UniversalVFXRenderer.asset` is a secondary Universal/Forward Renderer at Index 1. The project default remains Renderer2D, so existing 2D scenes do not change.
- The vendor Demo Scene references the standard `VFX_` Prefabs, whose shaders use `UniversalForwardOnly`. Open a vendor Demo and run `Rhythm Hunter > Rendering > Use Universal VFX Renderer On Open Scene Cameras`, then save only if the change should persist. The command also enables per-camera Depth/Opaque Texture for soft particles and distortion without imposing that cost on every 2D camera. Use the corresponding Renderer2D command to restore the renderer.
- Cartoon FX Remaster is not Forward-only: its four custom `.cfxrshader` sources contain both `UniversalForward` and `Universal2D` passes. They were previously imported for the Built-in pipeline, so the rendering tool checks this once per Editor session and reimports mismatched shaders for URP; `Rhythm Hunter > Rendering > Repair Cartoon FX Shaders For URP` also runs the repair manually.
- After that repair, Cartoon FX Prefabs can render directly in the normal Renderer2D gameplay scene. Its Universal2D passes intentionally disable soft-particle depth fading; use the Universal VFX Renderer for vendor previews or effects that specifically require the Forward/depth path.
- Both renderers support URP Volume post-processing. Run `Rhythm Hunter > Rendering > Enable Post-processing On Open Scene Cameras`, then add a Global Volume and Volume Profile. This command does not add or tune effects automatically.
- Importing both variants initially produced 382 duplicate-GUID warnings. Unity regenerated the conflicting 2D asset GUIDs; the current asset tree has zero duplicate GUIDs and the standard Demo container's 210 Prefab references all resolve, so no destructive reimport is currently required.

Art2 cave prototype — battle and perspective preview

Scene: Assets/FightDemo/Scenes/Art2_FightScene.unity
Environment: Assets/FightDemo/Prefabs/ArtBattle/CaveEnvironment_2_5D.prefab

Art2's existing combat controller, battlefield, UI and EventSystem are active again.
The original Main Camera, flat CaveEnvironment and ForestEnvironment remain inactive.
The static display hero is disabled; normal battle spawning supplies the actual characters.
Main_FightScene, shared character prefabs, battle scripts, source art, Input Actions,
Build Settings and global URP settings have not been modified by this prototype.

Coordinates: X left/right, Y height, Z depth.
Ground_XZ: center (0,0,-2), 24 x 14 units (front Z=-9, rear Z=5).
The original cave_08 floor region is still mapped to this plane without editing the image.
Rear cards are enlarged to 1.85 and placed 1.8 units further back.
Foreground cards have scale 1.65; middle cards retain scale 1.4861112.
All cards have a 45-degree visual tilt. GroundAnchor objects control placement.

Art2_TestCamera:
- Perspective by default, FOV 40, pitch 45, framing half-height 5.35.
- Focus (0,0,0), focus distance approximately 14.699.
- Existing URP renderer index 1; post-processing enabled, MSAA disabled on this camera.
- One Unity AudioListener and one FMOD StudioListener on the test camera.

Play Mode controls (Game view focused):
- Arrow keys: pan along world X/Z.
- Mouse wheel: zoom.
- P: toggle orthographic/perspective while matching vertical framing at the focus plane.
- Home: restore the initial position, projection, zoom and angle.
- Existing battle inputs are unchanged.

Inspector on Art2_TestCamera > Art2CameraController:
Perspective, Pitch (30-60), Field Of View, Framing Half Height, Focus,
Pan Speed, Zoom Speed, Zoom Limits, Enable Keyboard And Wheel.
The component owns the camera transform/projection during Play Mode.

Art2ActorPresentation (on Art2_2_5D_Prototype) sets spawned actor sprites to the same
sorting layer/order as the environment so foreground cards can occlude them by depth.
This only changes runtime renderer instances, restores their original sorting when
its component is disabled, and handles replacement actors at wave/formation changes.
Slot transforms are tilted 45 degrees in Art2, allowing spawned animations and authored
child effect anchors to inherit that angle. Combat ordering and rules remain unchanged.

Known limits:
- Source art includes painted perspective; it is still a layered-card scene, not a 3D cave mesh.
- Uses material-based distant blur and wrapped diffuse lighting, not full-screen depth of field.
- Ground contact uses soft blob shadows, not shadow maps or per-pixel normal maps.
- Moving/zooming can expose canvas edges or transparent sorting intersections.
- Fixed orthographic translation alone cannot create perspective motion parallax.
- Legacy world-space damage numbers, status icons and detached projectile effects keep
  their existing presentation rules; their 2.5D alignment needs runtime visual review.
- Pixel shimmering remains possible at noninteger scales and angled views.

Validation: Rhythm Hunter > Art2 > Validate Cave Layout, with Art2 open.
C# build and serialized activation/reference checks pass. Unity Play Mode, actual
shader rendering, floor contact and combat/VFX appearance are not yet verified here.

Scene view preview:
FightVfxScenePreview temporarily selects UniversalVFXRenderer only while rendering
Scene views for Art2 or the CaveEnvironment_2_5D prefab stage. The project default
Renderer2D setting is restored immediately afterwards and is not saved/modified.
Use Rhythm Hunter > Art2 > Frame Scene Preview to exit 2D/wireframe view and frame
approximately the test camera composition. Scene view aspect/FOV may differ from Game.

Moving the battle lineup down on screen while retaining ground contact:
Select BattlefieldWorld and decrease Transform Position Z (e.g. 0 to -0.5).
This moves all six battle slots; the active cave is under a separate root.
Keep Position Y unchanged so characters do not sink below the ground.
For heroes only, select HeroSlot_Front/Middle/Rear together and drag the blue Z-axis
Move gizmo with Global orientation (use a shared movement delta, not one absolute Z
value, to preserve the existing depth spacing). Make persistent edits outside Play Mode.

Atmosphere controls (Art2 only):
- Hierarchy Art2_2_5D_Prototype > Art2_Atmosphere contains a cool directional fill,
  three warm point lights and Art2_PostProcessing. Lights cast no shadow maps.
- Art2_PostProcessing > Volume > Weight: 0 disables grading/Bloom for comparison.
- Profile: Art2AtmosphereProfile.asset. Bloom intensity 0.16, threshold 1.1,
  scatter 0.5, clamp 4; exposure +0.1, contrast +4, saturation -4;
  Neutral tonemapping, vignette intensity 0.08.
- Volume uses existing TransparentFX layer; Art2 camera volume mask selects only
  that layer. Main camera's existing Default-only volume mask remains unchanged.
- Art2ActorLit.mat: Ambient Tint/Strength and Light Influence control actor shading.
  Materials replace only spawned Art2 renderer instances and restore on disable.
- CaveDistantLit.mat: Distant Blur (Source Pixels)=1.1 and Atmosphere Mix=0.06.
  Set both to 0 for crisp/clear distant art. Only wall/moss/sidewalls/door use it.
- CaveMiddleLit.mat keeps rocks/barricades sharp; original black foreground stays unlit.
- CaveTorchLit.mat: Emission Strength=1.8 supplies bright pixels for restrained Bloom.
- CaveGround.mat: ground lighting strength and ambient tint; original floor texture retained.
- Art2ActorPresentation > Show Contact Shadows toggles six reusable slot shadows.
  Shadow Size, Shadow Ground Offset and Ground Height align the floor contacts.
  Art2ContactShadow.mat > Shadow Color alpha controls darkness (default 0.35).

Performance/quality:
- Far cards use nine alpha-aware texture samples; sharp materials use one.
- No full-screen DoF blur, additional cameras, real-time shadow maps or extra depth passes.
- One shadow object per slot with one shared quad mesh, reused across attacks/waves.
- Original Point filtering, sprite PPU and shared role prefabs are unchanged.
- New shaders preserve SpriteRenderer color/alpha/flip for actor feedback.
- Scene preview: Rhythm Hunter > Art2 > Frame Scene Preview enables scene lights/effects.
- Check menus: Validate Cave Layout, Validate Atmosphere.
- C# compilation and serialized references verified. Unity batch startup still exits
  before project import, so GPU shader compilation, appearance, runtime restoration and
  actual frame-time measurements require Unity Editor verification.

Structure and shadow sample (2026-11-07):
- CaveStructureSample.prefab contains seven solid blocks and two faceted rocks.
- Door_Thickness: left/right jambs, lintel, threshold and front step; the original
  painted door remains visible in the recessed opening.
- Left_Wall_Volume / Right_Wall_Volume: angled solid side walls.
- Two_Faceted_Rocks: individually movable Rock_Left and Rock_Right.
- Shapes use matte URP/Lit materials with colors chosen for the cave palette.
  These are initial structural sample materials, not newly painted pixel textures.
- Art2StructureMesh builds flat-normal geometry in Edit and Play modes. Size controls
  dimensions; Top Depth Offset Per Height shears upper vertices along Z while keeping
  the base flat on the floor. Rock Sides and Rock Seed adjust the two rocks.
- Meshes rebuild only when configuration changes; generated meshes are hidden transient
  resources and are released on disable. No colliders or gameplay components are added.
- Default geometry budget: 168 triangles total across nine meshes.

Shadow light controls:
- Select ShadowTestLight_MoveMe under Art2_2_5D_Prototype and move its Transform.
- Art2ShadowTestLight automatically aims it at ShadowTest_AimTarget in Edit/Play modes.
- This is a directional light: moving it changes direction through automatic aiming;
  position alone does not create distance falloff. Disable Aim At Target to rotate manually.
- Light > Intensity / Color / Shadows / Strength control the light and its shadow.
- Bias and Normal Bias use this light's local settings rather than the global pipeline values.
- Main-light shadows already exist in the pipeline; additional-light and soft shadows
  remain disabled globally. This sample uses hard shadows without changing global URP.
- RenderSettings.sun in Art2 references this light. The other four atmosphere lights remain.
- New solid geometry casts shadows onto the cave floor. Characters retain their existing
  blob contact shadows; they do not acquire sprite silhouette shadow casting in this step.
- Ground ambient strength is reduced to 0.6 so cast shadows can be distinguished.
- Ground and lit cards sample the main-light shadow map; original transparent/VFX rules remain.

Review:
- Disable CaveStructureSample to compare the added geometry; the original cave remains.
- Volume Weight=0 and CaveDistantLit Blur Radius=0 permit a structure-only comparison.
- Menu Rhythm Hunter > Art2 > Validate Structure Sample checks generated meshes,
  normals, shadow configuration and existing atmosphere setup in the Editor.
- Your hidden HUD, rock/barricade overrides and camera settings are preserved.
- C# build and serialized reference/override checks pass. Actual GPU shadows, material
  rendering and geometry appearance still require an Editor visual check.

Sprite silhouette shadows (Art2 only)
------------------------------------
WoodenBarricades and Rocks use Two Sided shadow casting and CaveMiddleLit.
SpriteCardLit has an alpha-clipped ShadowCaster pass; transparent canvas pixels
never cast a rectangular shadow. No source sprite crop/PPU changes are required.
Art2ActorPresentation applies Art2ActorLit and Two Sided casting to spawned actors,
including replacement actors on later waves, and restores original settings when disabled.
Animation frames and SpriteRenderer flip/color are used by the shadow pass.

Inspector controls:
- Art2_2_5D_Prototype > Art2ActorPresentation > Cast Sprite Shadows: actor projection.
- Show Contact Shadows: independent soft oval grounding (optional, not light projection).
- Art2ActorLit / CaveMiddleLit > Shadow Alpha Cutoff (default 0.1): transparency threshold.
- ShadowTestLight_MoveMe: move it while Aim At Target is enabled to change shadow direction.
  This is a directional main light; distance alone does not change brightness.
- Light > Shadows > Strength / Bias / Normal Bias: darkness and contact offset.
  Excessive bias detaches shadows from feet. Existing point lights do not cast shadows.

Rhythm Hunter > Art2 > Validate Sprite Shadows checks imported materials and scene setup.
Visual checks: transparent margins cast no shadow; animated actors update silhouettes;
move the main light to inspect wood/rock/actor shadows on CaveGround; restart/change waves.
These are flat sprite-card silhouettes, not volumetric surfaces or per-pixel normal maps.
Original unlit foreground framing and luminous torch cards remain non-casters.

Red torch ambience:
Art2_Atmosphere contains Torch_Left/Right_RedFlicker and
Torch_InnerLeft/InnerRight_RedFlicker, aligned to the four painted flames.
Light Color and Range are editable on each Light (red, range 5 by default).
Art2TorchFlicker: Base Intensity 10, Flicker Amount 0.15, Speed 2.5;
unique Noise Seeds keep the four lights independent. Smooth variation runs
in Play Mode using unscaled time, without altering battle timing. Disable
the component to restore steady light. No extra shadow maps are generated.
The existing main shadow light and door fill remain unchanged.

Torch visibility correction:
Art2_TestCamera now selects Art2ForwardPlusRenderer (registered at renderer index 2).
The default renderer and existing UniversalVFXRenderer remain unchanged. Scene View
uses the Art2 renderer only for this scene / its Cave prefab previews. Sprite/floor
shaders support the URP 17 clustered light loop so large cards are not restricted
to four per-object additional lights. Main light settings are untouched.
Art2TorchFlicker > Preview In Editor enables Scene View flicker outside Play Mode;
preview intensity is restored after rendering, rather than saved into the Light.
Enable Scene Lighting via Rhythm Hunter > Art2 > Frame Scene Preview if needed.
The earlier Play Mode-only preview limitation no longer applies.

Visible circular torch glow:
All four red torch lights now include Art2TorchHalo. Radius (default 0.95)
and Brightness (1.4) control the visible additive radial halo independently
of the Light's illumination range. Light color and intensity drive the halo,
including flicker in Scene View and Play Mode. Disable Art2TorchHalo to remove
only the visible glow, retaining real lighting. Local Offset moves its centre.
A shared TorchHalo material and four persistent camera-facing procedural quads
are used; no textures, particle spawning or per-frame material creation.
Runtime/preview quads are not saved into the scene and are cleaned up on disable.
The halo is a visual glow, not volumetric scattering; transparent sprite occlusion
follows Unity transparent sorting and does not provide volumetric shadowing.

Scenery surface-lighting prototype (rocks / wooden barricades)
------------------------------------------------------------
CaveEnvironment_2_5D now assigns CaveRocksSurfaceLit and CaveWoodSurfaceLit to
Rocks_GroundAnchor / WoodenBarricades_GroundAnchor > Tilt_45deg > Sprite_FullCanvas.
These use dedicated 1280x720 normal textures aligned to the unmodified source canvases.
Normals describe authored broad rock faces, post sides and beam tops; painted brightness
is not interpreted as height. Texture import: Normal Map, linear, Point, no compression
or mipmaps. The original sprite textures, pivots and PPU are unchanged.

Material Inspector:
- Use Surface Normal Map: toggle directional surface relief for comparison.
- Surface Normal Strength: default 0.85; set 0 for a flat normal.
- Ambient Strength: default 0.62 (previous middle material: 0.8).
- Light Influence: default 0.85 (previous: 0.65).
For an exact pre-prototype material comparison, assign CaveMiddleLit temporarily.
Actors/distant scenery retain their previous materials and normal mapping is off by default.
Normal maps must match the source sprite UV canvas; these cards are not atlas-packed.

On each Sprite_FullCanvas, Art2SceneryContactShadows provides:
- Show Contact Shadows and Opacity (default 0.28).
- Ground Height: default 0.025, the world Y of the floor.
- Contacts: top-left source Foot Pixel, Width Pixels, world Depth and World Offset (X/Z).
Two contacts per card follow its transform/flip/scale, with persistent world-XZ quads.
Disable the component to compare grounding without changing projected light shadows.

Rebuild authored normal textures on Windows using:
  powershell -File Tools/Art2/Build-CaveSurfaceNormals.ps1
The script uses editable polygon directions on a four-source-pixel grid and never writes
original art. Keep PNG .meta files to preserve material references. Regeneration overwrites
only the two generated normal PNGs, so retain a copy before hand-painting them further.

Unity menu: Rhythm Hunter > Art2 > Validate Scenery Surface Lighting.
Visual acceptance: compare Use Surface Normal Map on/off while moving a test light;
rock upper/side faces and wood sides should differ, while the sprite outline remains fixed.
Inspect contact placement at the current camera and after moving a card. This first pass
is broad faceted shading, not 3D thickness, displacement, or physically accurate cavity AO.
C# and static references/import settings were checked; Unity GPU/Play Mode visual review
is still required before accepting the exact normal strength and shadow appearance.

Independent cropped cave objects (supersedes the two-card surface setup above)
-----------------------------------------------------------------------------
Art2 CaveEnvironment_2_5D now has eleven rendered cards. The previous full-canvas
wood/rock renderer objects are transform-only containers, retaining scene overrides.
The four active objects/materials are:
- WoodenBarricades_GroundAnchor/Tilt_45deg/Wood_SplitObjects/WoodenBarricade_Left
  cave_02_00 / CaveWoodLeftLit / CaveWoodLeftNormal
- .../WoodenBarricade_Right: cave_02_01 / CaveWoodRightLit / CaveWoodRightNormal
- Rocks_GroundAnchor/Tilt_45deg/Rocks_SplitObjects/Rock_Left
  cave_01_00 / CaveRockLeftLit / CaveRockLeftNormal
- .../Rock_Right: cave_01_01 / CaveRockRightLit / CaveRockRightNormal

The new art is exactly twice the original pixel scale. PPU remains 100; child scale
0.5 compensates. Old-canvas crop rectangles (top-left x,y,width,height) are:
rock left (100,344,232,96), right (1080,352,148,72),
wood left (368,276,144,68), right (852,208,160,144).
Their corresponding child offsets preserve the original world-space artwork positions.
Every opaque source pixel was matched at 2x scale; geometric centre/corner equivalence
and untouched scene/ancestor transforms were checked. New PNGs were not rewritten.
New sprite imports use Point, uncompressed, Single, Full Rect; pivots/PPU unchanged.

Each object has its own cropped normal material, Two Sided casting, receiving and
one independently adjustable contact shadow. Move the named child object to reposition
one prop. Default surface settings: normal strength 1, ambient 0.48, light influence 1,
Direct Light Wrap 0.1. Lower wrap preserves a more distinct shaded side. Existing actor,
floor and other background material lighting retains its prior defaults.
Use Surface Normal Map toggles only surface normals; the contact component has its own
Show Contact Shadows switch. The old full-canvas materials remain as comparison assets.
Build-CaveSurfaceNormals.ps1 now regenerates the two full maps plus four aligned split
maps; avoid running it over hand-edited normal PNGs without preserving those edits.
The main light, torch lights/VFX, camera and battle settings were not changed by the split.

Isolated surface lighting diagnostic
-----------------------------------
Menu: Rhythm Hunter > Art2 > Surface Lighting Diagnostic.
This Editor window creates a disposable PreviewScene with cloned materials, one white
main test light, and copies of cave_01_00 / cave_02_00. It does not open, save or change
Art2_FightScene, source materials, main light, torch lights, camera or object placement.
Five columns (rock above, wood below): original artwork; gray flat-normal control;
gray surface-normal result; world-space normal directions; artwork with surface lighting.
The gray comparison uses independent ambient controls. The final column retains the
source material's ambient/wrap settings so painted-color suppression can be diagnosed.
Use left/right light buttons, azimuth/elevation sliders or automatic sweep. Setting
normal strength to zero should remove face-dependent lighting in the gray-normal column.
Original artwork and flat-normal controls should not develop directional face patterns.

GPU check menu: Rhythm Hunter > Art2 > Verify Surface Light Response (GPU).
Renders the actual SpriteCardLit shader with white light at azimuth -65/+65, elevation15,
normal strength1 and gray ambient0.05. Reads 3x3 pixel patches on two authored faces of
both objects; requires each face to win under its corresponding light by >0.08 and the
flat-control inter-face difference to stay <0.025. A failure is explicitly reported.
Results and PNG captures: Temp/Art2SurfaceDiagnostic/{result.txt,left.png,right.png}.
This validates directional response and the normal transform, not final scene composition,
contact shadows, artistic normal accuracy or the final game's performance.
A one-shot Temp/Art2SurfaceDiagnostic.request can request the same check after editor
reload; it never runs automatically in ordinary project loads or during Play Mode.

Verified GPU diagnostic result (2026-11-07, Unity 6000.3.21f1 / DX11)
- Rock gray surface: left light left/right samples 0.439 / 0.024;
  right light 0.024 / 0.447. Flat-normal inter-face difference 0.000.
- Wood gray surface: left light 0.290 / 0.024;
  right light 0.184 / 0.451. Flat-normal inter-face difference 0.000.
- Active scene/root-count/dirty-state/main-light identity, transform and serialized
  settings preservation: PASS. Scene and cave prefab file hashes also unchanged.
- C# build: zero warnings/errors. Both GPU direction checks PASS.
URP Preview cameras require a render-scoped default-renderer override in this project;
the existing FightVfxScenePreview helper now recognizes only the named diagnostic
preview camera, restores the previous renderer after rendering, and never saves assets.
PNG exports are gamma-encoded for image viewing; measured values remain linear.
The gray maps show clear face changes while the original painted/dark colors diminish
the perceived difference. Passing these tests does not certify artistic normal accuracy.

Scene beat movement (Art2_FightScene)
- SceneBeatMotionManager is a scene root with an explicit reference to the existing
  FmodBeatClock (same instance as the combat controller). It samples song beat phase;
  there is no fallback BPM, independent timer, coroutine or event subscription.
- Targets: WoodenBarricade_Left / Right and Rock_Left / Right from CaveEnvironment_2_5D.
- Inspector: Motion Enabled toggles all movement; each Targets entry has Enabled,
  Target, Direction Space (World/Self), Direction, Amplitude (world units),
  Duration In Beats (0.05..1), Oscillations (1..8).
- Initial settings: world Y, amplitude 0.08, first 0.5 beat, one damped oscillation.
  Direction is normalized. Set X for sideways, Y for vertical, Z for depth motion.
- Add scene Transform references to Targets to animate other scenery. Each target
  should have only one motion owner; duplicate references within this manager are ignored.
- Pausing the song freezes phase; no song/timing anchor means no movement. Team Skill
  follows the same music grid. Disabling/removing targets or disabling the component
  removes its offsets. Authored transforms and original sprites remain unchanged.
- Movement runs before contact-shadow LateUpdate so shadows track the moved sprites.
  Pixel shimmer during fractional movement is expected; lower amplitude if distracting.

Beat-synced cave crowd sprites
- OverheadMoss_GroundAnchor retains its existing transform and scene overrides.
  Under Tilt_45deg / OverheadMoss_SplitObjects, CaveCrowd_Left and CaveCrowd_Right
  replace the old full-canvas sprite at its original image coordinates.
- Each has SceneBeatSpriteAnimator: Frames contains sprite and Duration In Beats
  together. 00 = 0.33, 01 = 0.33, 02 = 0.34; the loop is exactly one song beat.
- Art2_FightScene explicitly binds both components to the existing FmodBeatClock.
  No fallback tempo or frame-delta timer. Music pause freezes playback; before music
  or when disabled, the first sprite is shown. Other scenes using this prefab need
  to assign their own existing clock in the scene instance.
- Original image files and PPU remain unchanged; import is Single / Full Rect / Point
  without texture compression. Half-size transforms compensate for 2x source pixels.

3D status icons
- Art2 FightCombatController > Pooled Status Icons > Status Icon Camera references
  Art2_TestCamera. Leave it empty in 2D scenes to retain world-axis placement.
- Status Icon Offset is an additional camera-space offset (currently Y=0.15).
  X is screen right, Y screen up, negative Z toward camera. Individual StatusIcons
  prefabs retain Head Offset and Position Anchor controls; their offsets use the
  camera axes when a Status Icon Camera is supplied.
- SpriteTop uses the sprite-local top transformed into world space in 3D, including
  its existing transparent padding. Icons face the camera; lanes and block popup
  jumps follow screen axes. Pooled reuse restores rotation, scale and alpha.
- Validate Status Icon Pool now covers both perspective/orthographic tilted views,
  head placement, jump direction, and the existing lifecycle/1000 reuse tests.

ArtBattle character head anchors and 3D information
- All eight current ArtBattle character prefabs now expose HeadAnchor on their
  FightCharacterDefinition. It is an authored child of the actor root and inherits
  the existing card tilt. Adjust it in Prefab Mode for each character's anatomy.
- Initial placement uses the visible top silhouette of the default sprite, ignoring
  transparent canvas padding. It is stable across animation frames. Original sprite
  images, PPU, VFX_CastAnchor and VFX_ImpactAnchor are not changed.
- Art2 status icons and damage numbers use HeadAnchor. Damage Number Camera and
  Status Icon Camera reference Art2_TestCamera; their offsets are camera-relative.
  Empty camera references preserve the original 2D presentation.
- Art2ActorPresentation > Character Information: Information Camera controls text
  and health-bar facing; Health Bar Head Gap adjusts bar distance above HeadAnchor.
  Align Slot Information can be disabled; authored positions/rotations are restored.
- Tools > Rhythm Hunter > Art2 > Add Missing Character Head Anchors is idempotent:
  already assigned anchors retain their manually tuned positions.

Player HP / armor UI
- Prefabs/UI/PartyResourceHud.prefab is assigned to Art2 FightScenePresenter's
  Party Resource Hud Prefab. It is created under the existing scaled FightHudCanvas.
  The old TEAM DEFENSE debug panel is hidden when this prefab is assigned.
- Upper row reads PartyHp/MaxPartyHp; lower row reads PartyArmor/MaxPartyArmor.
  No separate resources are stored. Both icon order and horizontal fill run right
  to left. Each icon represents one point, showing empty/half/full in 0.5 steps.
- Prefab RectTransform controls the top-right margin (-32,-28). Icon Size (64),
  Icon Gap (8), Row Gap (12), and all four sprites are editable on the component.
- HP_empty / Armor_empty remain visible behind the filled sprites. Existing images
  are reused when maximum values shrink/grow; no per-hit instantiate/destroy.
- Validation: Rhythm Hunter > Validate Party Resource HUD. Covers 2.5/4 HP,
  1.5/3 armor, zero/full values, changing armor maximum, and image reuse.

Optional HUD sections and rhythm bounce
- FightScenePresenter > UI Visibility > Show Enemy Health Ui controls the enemy
  screen panel and enemy world health bars/numbers. Damage popups remain independent.
  Show Team Skill Gauge toggles the mana/ready panel, without affecting skill input
  or the separate active-Team-Skill remaining-beats display. Both default on and
  support changes during Play Mode.
- PartyResourceHud prefab > Beat Bounce: Enabled, Height (4 reference pixels),
  Duration Beats (0.45). One icon per row jumps each beat, left to right, then wraps.
  Both rows share the larger capacity as cycle length, including empty icons.
- Bounce directly samples FightCombatController.BeatClock's FMOD timeline and beat
  anchor; no separate clock or timer. Music pause holds phase; disabling restores
  the baseline. Changes to maximum HP/armor immediately update the shared cycle length.

Shared bounce example: 4 HP / 2 armor -> HP4, HP3, HP2+Armor2, HP1+Armor1.
Shorter rows wait until the shared left-to-right sweep reaches their columns.

Panel visibility buttons
- Bottom-right HudVisibilityButtons contains DefenseHud and FormationLockStatus
  ON/OFF buttons, outside the controlled panels so both can always be restored.
- FightScenePresenter > UI Visibility also exposes Show Defense Hud,
  Show Formation Lock Status, and Show Visibility Buttons for Inspector control.
- Hiding DefenseHud does not hide the new player HP/armor UI. Hiding the formation
  lock message does not change formation locking or the pre-battle controls.
- Other panel-specific visibility preferences are preserved when DefenseHud is hidden.

Player beat-timeline artwork (Art2)
- FightScenePresenter > Beat Timeline Artwork references bar(5 ver), Beat bar(blue),
  stone_1(blue), stone_2(blue), and stone_4(blue)_1..5. Original bottom offset,
  timeline width, normal/burst point counts, calibrated movement and half-beat
  burst spacing are unchanged. Other scenes without sprites retain the old visuals.
- The stone reads shared TeamSkillGauge / TeamSkillGaugeMax, filling from the bottom.
  At full MP, all five ready frames loop on the existing FMOD song grid. Default
  Stone Ready Loop Beats = 1 (0.2 beat/frame); consuming MP restores the empty/fill art.
- Successful Beat Stone Feedback controls Enabled, Direction (UI X/Y), Amplitude
  (reference pixels), Beats and Cycles. Misses do not start feedback; accepted
  Team Skill A half-beats also trigger it. No separate music timer is introduced.
- StoneSuccessEffectAnchor is generated at the centre of the stone. The optional
  Stone Success Effect Prefab is currently empty; it accepts a UI RectTransform
  effect. Particle-only world VFX will need a canvas-compatible rendering setup when
  the art is available. Lifetime 0 delegates cleanup to the effect prefab.
- Rhythm Hunter > Validate Timeline Artwork checks original geometry/count,
  shared MP fill, ready-frame sequence, directional shake/reset, misses and event
  subscription cleanup. Preview: Temp/TimelineArt-preview.png.
- The new artwork timeline is a direct child of FightHudCanvas at the same bottom
  coordinates, so the disabled legacy Bottom UI debug group does not hide it.
  Legacy statistics remain in the old hidden group.

Player/developer UI isolation and live-scene repair
- FightHudCanvas / PlayerUI: PartyResourceHud, EqualBeatTimeline, TeamSkillRemaining.
- FightHudCanvas / DeveloperUI: legacy top/bottom debug groups, DefenseHud,
  FormationLockStatus, visibility buttons, TeamSkillGauge, BossState and countdown text.
- FightHudCanvas / BattleMenus: battle preparation, pause button and pause overlay.
- FightScenePresenter > Player / Developer UI Groups > Show Developer Ui only toggles
  DeveloperUI. PlayerUiCanvas is explicit; inactive legacy text is not a dependency.
- Fixed the live Art2 scene's empty artwork references. Previously the disk scene
  preview had valid sprites while the loaded editor scene still had null references,
  causing fallback creation beneath the disabled legacy Bottom UI group.
- Rhythm Hunter > Art2 > Configure Player UI Artwork repairs the loaded Art2 scene
  references and saves that scene (use outside Play Mode). Existing layout settings
  and unrelated scene objects are retained.
- Verified in actual Play: player resource icons and stone timeline visible at
  1920x1080; timeline position (960,76), width 840, active parent chain. Capture:
  Temp/PlayerUI-Play.png. Returned Unity to Edit Mode after verification.

Timeline dimensions and accepted-point removal
- FightDemoController / FightScenePresenter / Equal Beat Timeline:
  Timeline Width = lane width, Timeline Bottom Offset = screen-bottom distance.
- Beat Timeline Artwork: Timeline Point Art Size = point width/height,
  Stone Art Size = centre stone width/height, Timeline Bar Height = background height
  (0 preserves its source aspect ratio). These values now update live in Play Mode.
- Accepted Beat Point Feedback: Remove Accepted Beat Points, Beat Point Hit Effect
  Prefab (optional UI effect), Beat Point Hit Effect Lifetime (0 = self-managed).
- Each left/right pair represents one judged beat. Consume its global half-beat ID,
  not the nearest pooled Image object: early input removes the approaching pair,
  late input never removes the following pair, and duplicate success does not replay
  the point effect. Normal beats and Team Skill half-beats use the same ID scale.
- Early hit effects spawn at the two removed point positions. If the judged pair
  has already passed the centre, one effect spawns at the centre instead. The existing
  Stone Success Effect Prefab remains an independent centre-stone feedback slot.
- Images remain pooled/reused for future beats; consumed IDs are bounded and reset
  when the battle roster is rebuilt. Misses do not consume any point.

Timeline tuning (Art2): FightScenePresenter Timeline Scale = 1.9, Timeline Bottom Offset = 200 (Canvas units from bottom to timeline center). Successful Beat Stone Feedback now uses Stone Bounce Enabled / Amount / Beats: 0.15 expansion, slight 0.03 undershoot, restored in 0.3 beat. Position remains fixed. Adjust presenter fields outside Play Mode to persist changes.

Updated timeline: Art2 bottom offset = 150, scale stays 1.9. Preview Beats = 5. Artwork anchors (inner to outer) = 92/366, 148/366, 204/366, 260/366, 316/366 of half width. Timeline Step Travel Fraction = 0.25: hold for 75% of each interval, then SmoothStep into next anchor on the next beat. Burst uses half-beat intervals with midpoint positions. Stone bounce uses elapsed FMOD timeline milliseconds and captures beat duration on successful input, independent of callback beat anchor changes; starts visibly expanded even on repeated input.

Timeline movement revision: restored original linear constant-speed convergence. Removed anchor stepping and its travel-fraction settings. Art2 retains preview 5, scale 1.9, bottom offset 150, successful stone bounce and Team Skill half-beat points.

Art2 selective focus (2026-11-09):
OverheadMoss_GroundAnchor is a legacy name for the animated GOBLIN CROWD, not moss.
CaveEnvironment_2_5D uses three independent focus materials (Art2Prototype folder):
- CaveFocusDistant: rear wall / side walls / closed door; Blur 1.35, Saturation 0.90, Contrast 0.96, Atmosphere Mix 0.08.
- CaveFocusCrowd: CaveCrowd_Left / Right; Blur 0.8, Saturation 0.88, Contrast 0.94, Atmosphere Mix 0.06. Existing beat animation unchanged.
- CaveFocusForeground: FrontRockEdges / ForegroundSilhouette; Blur 0.65. Neutral ambient and zero light influence preserve unlit artwork; ShadowCaster disabled.
Select the material asset in Project and adjust Distant Blur (Source Pixels). Zero disables blur. Focus Saturation / Focus Contrast = 1 preserve original color/contrast; lower values soften background detail. Atmosphere Mix = 0 removes the distant tint.
For a clear comparison, temporarily set Blur=0, Saturation=1, Contrast=1, Atmosphere Mix=0 on these three materials; restore the above values afterward.
Actors, rocks, barricades, floor, torch lights and UI settings are unchanged. No texture import changes, no full-screen blur, no additional cameras.
Shadow diagnosis: Art2_TestCamera uses perspective. ShadowTestLight_MoveMe is directional with shadows; cave fill and torch point lights have shadows disabled. Parallel world-space cast shadows may converge toward a screen-space vanishing point. Test Perspective off (Art2CameraController, or P if camera input enabled) to compare, without rotating the main light. Art2ActorPresentation also adds non-directional contact ellipses; Show Contact Shadows isolates those from Cast Sprite Shadows. Visual confirmation in Unity is still required.

Distant focus wall revision (supersedes individual far/crowd/door focus settings above):
CaveEnvironment_2_5D > DistantFocusWall_MoveMe > Art2FocusWall.
This is an invisible world-space plane, not a rendered wall or a screen capture. Its local +Z is the affected side. Default local position (0,0,0.5) is a provisional road-start boundary; adjust visually in Art2.
- Move world Z to move the beginning of distant effects; rotate Y 180 degrees to reverse the affected side.
- Transition Depth: distance in world units until full effect (default 3).
- Blur Pixels: source texture sampling radius (default 1.2, zero disables blur).
- Fog Density: fog color mix (default 0.16, zero disables fog). Source sprite opacity remains unchanged.
- Fog Color: tint of the distant veil.
- Disable Art2FocusWall to restore renderer property blocks / compare before and after.
- Guide Size changes only the Scene view gizmo, not the infinite plane coverage. Enable Gizmos and select the wall to see start/full-effect planes.
Ten explicit environment targets: floor, rear wall, side walls, door, both animated goblin crowds, four torch bodies. Actors, HUD, rocks and barricades are excluded. Fire/halo particles use separate shaders and are not blurred/fogged by this implementation.
CaveFocusDistant/Crowd/Door now have Blur=0, Atmosphere=0, Saturation=1, Contrast=1: all distant softening comes from the wall. Foreground edge softening remains separate.
Uses renderer property blocks, preserving shared materials; original blocks restored on disable. No global shader values, additional camera, lighting changes, or gameplay changes.
Unity Shader import and final road-start placement require visual verification; C# compilation alone does not validate GPU shader output.

Torch rig / focus integration:
Art2 scene: each WallTorch_* sprite owns FlameAnchor at its visible artwork top. The corresponding Torch_*_RedFlicker light is at the anchor origin; the existing flame particle root is at the same origin under the light. Particle simulation is Local. Adjust FlameAnchor local position for artistic placement, or move the torch sprite to move the complete rig. Keep the light and flame root local positions zero to avoid camera-angle-dependent separation.
DistantFocusWall_MoveMe > Additional Roots contains the four light/particle hierarchies. Runtime halo renderers are discovered without allocating new material instances. Existing target renderers remain unchanged.
Art2-only TorchFocus_M_VFX_2D_Fireball_Projectile_01_Color uses TorchParticleFocus.shader, preserving the source forward pass tint/emission/vertex-color modulation and alpha blending. Nine alpha-aware taps blur inside the current flipbook cell; samples are clamped to avoid neighbouring frames. Fog uses the same plane and density as scenery. Particle billboard animation remains enabled. Original Vefects assets are untouched.
Round halos already have smooth radial edges; fog now attenuates their additive light instead of drawing a fog-colored rectangle. Physical Light intensity/flicker is unchanged.
Controls remain on Art2FocusWall: Blur Pixels=0 disables fire/scenery blur; Fog Density=0 disables fog. Disable the wall to restore original property blocks. Fire materials retain their original appearance with these values at zero.
Editor utility: Rhythm Hunter > Art2 > Bind Torch Rigs And Focus. It preserves existing FlameAnchor placements on repeat runs and validates four linked rigs before saving the active Art2 scene.

Crowd/torch blur emphasis:
Art2FocusWall > Background Detail Blur now has Emphasized Roots (goblin crowd plus four complete torch hierarchies), Detail Blur Multiplier=1.5 and Detail Blur Transition Scale=0.2. Only blur reaches full strength sooner; fog and the general Transition Depth are unchanged. Multiplier=1 and Transition Scale=1 restore the original depth-based blur. Blur Pixels=0 still disables all wall-driven blur. Objects in front of the wall remain unaffected. Latest live Unity validation confirmed all ten scenery/fire renderers receive parameters and all three shaders import without errors.

Character directional shadows and door contact (2026-11-09):
Art2ActorPresentation: Show Contact Shadows=false; Cast Sprite Shadows=true; Align Feet To Ground=true; Actor Ground Height=0. Eight ArtBattle character prefabs now have a root FootAnchor fitted from the lower visible sprite silhouette. On spawn only, the actor visual root is translated vertically so FootAnchor reaches the ground; slot positions and gameplay are unchanged. Animated jump offsets remain free. Existing anchors are preserved by repeated setup. Moving FootAnchor upwards lowers the grounded actor, and vice versa. Reset/disable restores actor original local positions.
ShadowTestLight_MoveMe: Normal Bias reduced from 0.15 to 0.03 to reduce detached directional shadow edges. Light direction/color/intensity unchanged. Check real gameplay for acne or animation frames with feet above the reference pose; do not increase global offsets to compensate for individual artwork.
ClosedDoor_GroundAnchor > Tilt_45deg > Sprite_FullCanvas has Art2SceneryContactShadows as an Art2 scene-added component. Opacity=0.5, Depth=0.7, width=115% of visible bottom band; same material/shader as rocks. This door contact remains enabled even while character contact ellipses are disabled.
Editor menu: Rhythm Hunter/Art2/Configure Character Grounding And Door Shadow. Unity validation confirmed eight authored foot anchors can reach y=0 at left/center/right under 45-degree tilt. Actual game camera shadow appearance still needs visual verification.

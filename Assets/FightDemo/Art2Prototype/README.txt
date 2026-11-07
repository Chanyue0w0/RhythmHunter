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

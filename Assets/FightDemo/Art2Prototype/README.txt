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
- Existing URP renderer index 1; post-processing and MSAA disabled on this camera.
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
- No blur-based depth of field, HD-2D lighting or shadows have been added.
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

# Otter Aquarium Prototype scenes

- `OtterAquarium.unity` is the top-down sea otter movement, surface transition, slide, and water VFX prototype.
- `OtterAquariumCombat.unity` uses the PirateFightScene battle staging and its Cinemachine combat/boss camera transition.
- The combat scene intentionally removes pirate ocean waves, animated ocean surfaces, ship motion, and the ocean tuning panel.
- `Assets/PirateOceanPrototype/Scenes/PirateFightScene.unity` remains the source reference. Continue future scene work in this folder.

## Aquarium area authoring

- Open `Rhythm Hunter > Otter Aquarium > Open Area Authoring` to create movement zones and solid obstacles.
- Surface zones are trigger colliders: deep water priority 50, shallow water priority 20, and walkable land priority 100.
- Solid obstacles are non-trigger polygon colliders used for rocks, walls, and blocking decorations.
- Select any polygon and use `PolygonCollider2D > Edit Collider` to adjust its shape in the Scene view.
- After editing water or land zones, press `Rebake Water Mask From Zones` so the animated water overlay follows the new polygons.
- `Rebuild Prototype Scene...` requires confirmation because rebuilding replaces all hand-authored scene geometry.

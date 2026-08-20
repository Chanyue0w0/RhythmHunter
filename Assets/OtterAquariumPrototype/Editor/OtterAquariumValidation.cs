using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterAquariumValidation
    {
        [MenuItem("Rhythm Hunter/Validate Sea Otter Aquarium Prototype")]
        public static void ValidateFromMenu()
        {
            ValidateScene(true);
        }

        public static bool ValidateScene(bool logSuccess)
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(OtterAquariumSceneBuilder.ScenePath);
            if (asset == null)
            {
                Debug.LogError($"[OtterAquariumValidation] Scene is missing: {OtterAquariumSceneBuilder.ScenePath}");
                return false;
            }

            Scene scene = SceneManager.GetSceneByPath(OtterAquariumSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(OtterAquariumSceneBuilder.ScenePath, OpenSceneMode.Additive);

            bool hasMovement = FindInScene<OtterMovementController>(scene) != null;
            bool hasSensor = FindInScene<OtterSurfaceSensor>(scene) != null;
            bool hasVfx = FindInScene<OtterVfxController>(scene) != null;
            bool hasPresenter = FindInScene<OtterVisualPresenter>(scene) != null;
            bool hasCamera = FindInScene<OtterCameraFollow>(scene) != null;
            bool hasHud = FindInScene<OtterPrototypeHud>(scene) != null;
            ParticleSystemRenderer[] particleRenderers = FindAllInScene<ParticleSystemRenderer>(scene);
            Material expectedParticleMaterial = AssetDatabase.LoadAssetAtPath<Material>(OtterAquariumSceneBuilder.WaterParticleMaterialPath);
            int validParticleMaterials = System.Array.FindAll(
                particleRenderers,
                renderer => renderer.sharedMaterial == expectedParticleMaterial
                    && renderer.sharedMaterial != null
                    && renderer.sharedMaterial.shader != null
                    && renderer.sharedMaterial.shader.name != "Hidden/InternalErrorShader").Length;
            AquariumSurfaceZone[] zones = FindAllInScene<AquariumSurfaceZone>(scene);
            bool hasWater = System.Array.Exists(zones, zone => zone.SurfaceType == AquariumSurfaceType.Water);
            bool hasShallow = System.Array.Exists(zones, zone => zone.SurfaceType == AquariumSurfaceType.ShallowWater);
            int landZoneCount = System.Array.FindAll(zones, zone => zone.SurfaceType == AquariumSurfaceType.Land).Length;
            bool hasLand = landZoneCount >= 6;
            SpriteRenderer background = FindNamedInScene<SpriteRenderer>(scene, "ZooBackground");
            bool hasBackground = background != null
                && background.sprite != null
                && AssetDatabase.GetAssetPath(background.sprite) == OtterAquariumSceneBuilder.BackgroundSpritePath;
            bool hasBoundary = FindNamedInScene<EdgeCollider2D>(scene, "AquariumInnerWallBoundary") != null;

            Physics2D.SyncTransforms();
            AquariumSurfaceType? deepSample = GetSurfaceAtPixel(zones, new Vector2(720f, 650f));
            AquariumSurfaceType? shallowSample = GetSurfaceAtPixel(zones, new Vector2(170f, 260f));
            AquariumSurfaceType? landSample = GetSurfaceAtPixel(zones, new Vector2(1100f, 220f));
            bool imageSamplesMatch = deepSample == AquariumSurfaceType.Water
                && shallowSample == AquariumSurfaceType.ShallowWater
                && landSample == AquariumSurfaceType.Land;

            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);

            bool valid = hasMovement && hasSensor && hasVfx && hasPresenter && hasCamera && hasHud
                && hasWater && hasShallow && hasLand && hasBackground && hasBoundary && imageSamplesMatch
                && expectedParticleMaterial != null && validParticleMaterials >= 5;
            if (!valid)
            {
                Debug.LogError(
                    "[OtterAquariumValidation] Validation failed. "
                    + $"Movement={hasMovement}, Sensor={hasSensor}, VFX={hasVfx}, Presenter={hasPresenter}, "
                    + $"Camera={hasCamera}, HUD={hasHud}, Water={hasWater}, Shallow={hasShallow}, Land={hasLand}, "
                    + $"LandZones={landZoneCount}/6, Background={hasBackground}, Boundary={hasBoundary}, "
                    + $"Samples(Deep/Shallow/Land)={deepSample}/{shallowSample}/{landSample}, "
                    + $"ParticleMaterial={(expectedParticleMaterial != null)}, ValidParticleRenderers={validParticleMaterials}/5");
                return false;
            }

            if (logSuccess)
                Debug.Log("[OtterAquariumValidation] Scene passed all prototype checks.");
            return true;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            System.Collections.Generic.List<T> results = new();
            foreach (GameObject root in scene.GetRootGameObjects())
                results.AddRange(root.GetComponentsInChildren<T>(true));
            return results.ToArray();
        }

        private static T FindNamedInScene<T>(Scene scene, string objectName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    if (component.name == objectName)
                        return component;
                }
            }
            return null;
        }

        private static AquariumSurfaceType? GetSurfaceAtPixel(AquariumSurfaceZone[] zones, Vector2 pixel)
        {
            Vector2 point = OtterAquariumSceneBuilder.ImagePixelToWorld(pixel);
            AquariumSurfaceZone selected = null;
            foreach (AquariumSurfaceZone zone in zones)
            {
                Collider2D zoneCollider = zone.GetComponent<Collider2D>();
                if (zoneCollider != null
                    && zoneCollider.OverlapPoint(point)
                    && (selected == null || zone.Priority > selected.Priority))
                {
                    selected = zone;
                }
            }
            return selected != null ? selected.SurfaceType : null;
        }
    }
}

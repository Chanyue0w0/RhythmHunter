using System.Collections.Generic;
using RhythmHunter.OtterAquariumPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public sealed class OtterAquariumAuthoringWindow : EditorWindow
    {
        [MenuItem("Rhythm Hunter/Otter Aquarium/Open Area Authoring")]
        private static void Open()
        {
            GetWindow<OtterAquariumAuthoringWindow>("Aquarium Areas");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Movement Surface Zones", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Surface zones are triggers. They change movement and VFX but do not block the otter. "
                + "Select a created zone and use PolygonCollider2D > Edit Collider to shape it.",
                MessageType.Info);

            if (GUILayout.Button("Add Deep Water Zone"))
                CreateSurfaceZone(AquariumSurfaceType.Water, 50, 1f);
            if (GUILayout.Button("Add Shallow Water Zone"))
                CreateSurfaceZone(AquariumSurfaceType.ShallowWater, 20, 0.82f);
            if (GUILayout.Button("Add Walkable Land Zone"))
                CreateSurfaceZone(AquariumSurfaceType.Land, 100, 1f);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Solid Obstacles", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Obstacle polygons are solid colliders. Use them for rocks, walls, and decorations the otter cannot cross.",
                MessageType.Info);

            if (GUILayout.Button("Add Rock Obstacle"))
                CreateObstacle(AquariumObstacleType.Rock);
            if (GUILayout.Button("Add Wall Obstacle"))
                CreateObstacle(AquariumObstacleType.Wall);
            if (GUILayout.Button("Add Decoration Obstacle"))
                CreateObstacle(AquariumObstacleType.Decoration);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Water Surface", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "After changing any water or land polygon, rebake the mask so the animated overlay follows the new layout.",
                MessageType.Warning);
            if (GUILayout.Button("Rebake Water Mask From Zones", GUILayout.Height(30f)))
                OtterAquariumSceneBuilder.BakeWaterSurfaceFromActiveScene();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Select Movement Zones Root"))
                SelectNamedObject(OtterAquariumSceneBuilder.SurfaceZoneLayoutName);
            if (GUILayout.Button("Select Solid Obstacles Root"))
                SelectNamedObject(OtterAquariumSceneBuilder.ObstaclesRootName);
        }

        private static void CreateSurfaceZone(AquariumSurfaceType type, int priority, float speedMultiplier)
        {
            Transform parent = FindNamedTransform(OtterAquariumSceneBuilder.SurfaceZoneLayoutName);
            if (parent == null)
            {
                Debug.LogError("[OtterAquariumAuthoring] Open OtterAquarium.unity before creating a surface zone.");
                return;
            }

            string baseName = type switch
            {
                AquariumSurfaceType.Water => "DeepWater_Custom",
                AquariumSurfaceType.ShallowWater => "ShallowWater_Custom",
                _ => "Land_Custom"
            };
            string name = GameObjectUtility.GetUniqueNameForSibling(parent, baseName);
            GameObject zoneObject = new(name, typeof(PolygonCollider2D), typeof(AquariumSurfaceZone));
            Undo.RegisterCreatedObjectUndo(zoneObject, $"Create {name}");
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.position = GetCreationCenter();

            PolygonCollider2D polygon = zoneObject.GetComponent<PolygonCollider2D>();
            polygon.points = CreateDefaultSquare();
            polygon.isTrigger = true;
            zoneObject.GetComponent<AquariumSurfaceZone>().Configure(type, priority, speedMultiplier);
            FinishCreation(zoneObject);
        }

        private static void CreateObstacle(AquariumObstacleType type)
        {
            Transform parent = FindNamedTransform(OtterAquariumSceneBuilder.ObstaclesRootName);
            if (parent == null)
            {
                Debug.LogError("[OtterAquariumAuthoring] Open OtterAquarium.unity before creating an obstacle.");
                return;
            }

            string name = GameObjectUtility.GetUniqueNameForSibling(parent, $"{type}_Custom");
            GameObject obstacleObject = new(name, typeof(PolygonCollider2D), typeof(AquariumObstacle));
            Undo.RegisterCreatedObjectUndo(obstacleObject, $"Create {name}");
            obstacleObject.transform.SetParent(parent, false);
            obstacleObject.transform.position = GetCreationCenter();

            PolygonCollider2D polygon = obstacleObject.GetComponent<PolygonCollider2D>();
            polygon.points = CreateDefaultSquare();
            polygon.isTrigger = false;
            obstacleObject.GetComponent<AquariumObstacle>().Configure(type);
            FinishCreation(obstacleObject);
        }

        private static void FinishCreation(GameObject createdObject)
        {
            Selection.activeGameObject = createdObject;
            EditorGUIUtility.PingObject(createdObject);
            EditorSceneManager.MarkSceneDirty(createdObject.scene);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static Vector2[] CreateDefaultSquare()
        {
            return new[]
            {
                new Vector2(-0.75f, -0.75f),
                new Vector2(0.75f, -0.75f),
                new Vector2(0.75f, 0.75f),
                new Vector2(-0.75f, 0.75f)
            };
        }

        private static Vector3 GetCreationCenter()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            Vector3 pivot = sceneView != null ? sceneView.pivot : Vector3.zero;
            return new Vector3(pivot.x, pivot.y, 0f);
        }

        private static void SelectNamedObject(string objectName)
        {
            Transform target = FindNamedTransform(objectName);
            if (target != null)
            {
                Selection.activeGameObject = target.gameObject;
                EditorGUIUtility.PingObject(target.gameObject);
            }
        }

        private static Transform FindNamedTransform(string objectName)
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName)
                        return child;
                }
            }
            return null;
        }
    }

    internal static class OtterAquariumAuthoringGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
        private static void DrawSurfaceZone(AquariumSurfaceZone zone, GizmoType gizmoType)
        {
            PolygonCollider2D polygon = zone.GetComponent<PolygonCollider2D>();
            Color color = zone.SurfaceType switch
            {
                AquariumSurfaceType.Water => new Color(0.05f, 0.45f, 1f, 0.9f),
                AquariumSurfaceType.ShallowWater => new Color(0.05f, 1f, 0.9f, 0.9f),
                _ => new Color(1f, 0.72f, 0.08f, 0.9f)
            };
            DrawPolygon(polygon, color, (gizmoType & GizmoType.Selected) != 0, $"{zone.SurfaceType}  P{zone.Priority}  x{zone.SpeedMultiplier:0.##}");
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
        private static void DrawObstacle(AquariumObstacle obstacle, GizmoType gizmoType)
        {
            PolygonCollider2D polygon = obstacle.GetComponent<PolygonCollider2D>();
            Color color = obstacle.ObstacleType switch
            {
                AquariumObstacleType.Wall => new Color(1f, 0.12f, 0.12f, 0.95f),
                AquariumObstacleType.Decoration => new Color(0.9f, 0.2f, 0.75f, 0.95f),
                _ => new Color(1f, 0.3f, 0.08f, 0.95f)
            };
            DrawPolygon(polygon, color, (gizmoType & GizmoType.Selected) != 0, $"Solid {obstacle.ObstacleType}");
        }

        private static void DrawPolygon(PolygonCollider2D polygon, Color color, bool selected, string label)
        {
            if (polygon == null || polygon.pathCount == 0)
                return;

            Color previousColor = Handles.color;
            Handles.color = selected ? color : new Color(color.r, color.g, color.b, 0.42f);
            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                Vector2[] path = polygon.GetPath(pathIndex);
                if (path.Length < 2)
                    continue;

                List<Vector3> worldPoints = new(path.Length + 1);
                foreach (Vector2 point in path)
                    worldPoints.Add(polygon.transform.TransformPoint(point + polygon.offset));
                worldPoints.Add(worldPoints[0]);
                Handles.DrawAAPolyLine(selected ? 4f : 2f, worldPoints.ToArray());

                if (selected)
                    Handles.Label(worldPoints[0], label, EditorStyles.boldLabel);
            }
            Handles.color = previousColor;
        }
    }
}

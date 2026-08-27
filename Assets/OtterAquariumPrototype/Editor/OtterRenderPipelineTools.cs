using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RhythmHunter.OtterAquariumPrototypeEditor
{
    public static class OtterRenderPipelineTools
    {
        private const string MenuRoot = "Rhythm Hunter/Rendering/";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string Renderer2DName = "Renderer2D";
        private const string UniversalVfxRendererName = "UniversalVFXRenderer";
        private const string CartoonFxShaderFolder = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Shaders";
        private const string CartoonFxRepairSessionKey = "RhythmHunter.CartoonFxUrpRepairChecked.v2";

        [InitializeOnLoadMethod]
        private static void ScheduleCartoonFxUrpRepairCheck()
        {
            EditorApplication.delayCall += RepairCartoonFxShadersIfNeeded;
        }

        [MenuItem(MenuRoot + "Use Universal VFX Renderer On Open Scene Cameras")]
        private static void UseUniversalRendererOnOpenSceneCameras()
        {
            AssignRendererToOpenSceneCameras(UniversalVfxRendererName);
        }

        [MenuItem(MenuRoot + "Use Renderer2D On Open Scene Cameras")]
        private static void UseRenderer2DOnOpenSceneCameras()
        {
            AssignRendererToOpenSceneCameras(Renderer2DName);
        }

        [MenuItem(MenuRoot + "Use Universal VFX Renderer On Selected Cameras")]
        private static void UseUniversalRendererOnSelectedCameras()
        {
            AssignRendererToSelectedCameras(UniversalVfxRendererName);
        }

        [MenuItem(MenuRoot + "Use Renderer2D On Selected Cameras")]
        private static void UseRenderer2DOnSelectedCameras()
        {
            AssignRendererToSelectedCameras(Renderer2DName);
        }

        [MenuItem(MenuRoot + "Enable Post-processing On Open Scene Cameras")]
        private static void EnablePostProcessingOnOpenSceneCameras()
        {
            Camera[] cameras = FindOpenSceneCameras();
            if (cameras.Length == 0)
            {
                EditorUtility.DisplayDialog("Rhythm Hunter Rendering", "目前開啟的 Scene 找不到 Camera。", "確定");
                return;
            }

            Undo.SetCurrentGroupName("Enable camera post-processing");
            foreach (Camera camera in cameras)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Enable camera post-processing");
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
                EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            }

            Debug.Log($"[Rhythm Hunter Rendering] 已在 {cameras.Length} 台 Camera 啟用 Post-processing。仍需在 Scene 中加入 Volume 與 Profile 才會產生畫面效果。");
        }

        [MenuItem(MenuRoot + "Repair Cartoon FX Shaders For URP")]
        private static void RepairCartoonFxShadersForUrp()
        {
            int reimported = ReimportCartoonFxShaders(false);
            EditorUtility.DisplayDialog(
                "Rhythm Hunter Rendering",
                reimported > 0
                    ? $"已重新匯入 {reimported} 個 Cartoon FX Shader，並以目前 URP 設定編譯。"
                    : "Cartoon FX Shader 已經是目前 URP 版本，不需要重新匯入。",
                "確定");
        }

        [MenuItem(MenuRoot + "Validate Dual Renderer Setup")]
        private static void ValidateDualRendererSetup()
        {
            int renderer2DIndex = FindRendererIndex(Renderer2DName);
            int universalIndex = FindRendererIndex(UniversalVfxRendererName);
            bool valid = renderer2DIndex == 0 && universalIndex >= 0;
            string message = valid
                ? $"設定正確。\n\nDefault: {Renderer2DName} (Index {renderer2DIndex})\nVFX: {UniversalVfxRendererName} (Index {universalIndex})"
                : $"設定不完整。\n\n{Renderer2DName}: Index {renderer2DIndex}\n{UniversalVfxRendererName}: Index {universalIndex}";

            EditorUtility.DisplayDialog("Rhythm Hunter Dual Renderer", message, "確定");
            if (valid)
                Debug.Log("[Rhythm Hunter Rendering] Renderer2D 維持預設，UniversalVFXRenderer 可供 Forward VFX Demo 與特效 Camera 使用。");
            else
                Debug.LogError("[Rhythm Hunter Rendering] Dual Renderer 設定驗證失敗，請檢查 Assets/Settings/UniversalRP.asset。");
        }

        private static void AssignRendererToOpenSceneCameras(string rendererName)
        {
            AssignRenderer(FindOpenSceneCameras(), rendererName);
        }

        private static void AssignRendererToSelectedCameras(string rendererName)
        {
            var cameras = new List<Camera>();
            foreach (GameObject selected in Selection.gameObjects)
            {
                Camera camera = selected.GetComponent<Camera>();
                if (camera != null)
                    cameras.Add(camera);
            }

            if (cameras.Count == 0)
            {
                EditorUtility.DisplayDialog("Rhythm Hunter Rendering", "請先在 Hierarchy 選取一個或多個 Camera。", "確定");
                return;
            }

            AssignRenderer(cameras.ToArray(), rendererName);
        }

        private static void AssignRenderer(Camera[] cameras, string rendererName)
        {
            int rendererIndex = FindRendererIndex(rendererName);
            if (rendererIndex < 0)
            {
                EditorUtility.DisplayDialog("Rhythm Hunter Rendering", $"在 {PipelinePath} 找不到 {rendererName}。", "確定");
                return;
            }

            if (cameras.Length == 0)
            {
                EditorUtility.DisplayDialog("Rhythm Hunter Rendering", "目前範圍找不到 Camera。", "確定");
                return;
            }

            Undo.SetCurrentGroupName($"Assign {rendererName}");
            foreach (Camera camera in cameras)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, $"Assign {rendererName}");
                data.SetRenderer(rendererIndex);
                if (rendererName == UniversalVfxRendererName)
                {
                    data.requiresDepthOption = CameraOverrideOption.On;
                    data.requiresColorOption = CameraOverrideOption.On;
                }
                EditorUtility.SetDirty(data);
                EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            }

            string bufferNote = rendererName == UniversalVfxRendererName
                ? "，並啟用 Depth/Opaque Texture 供 Soft Particle 與 Distortion 使用"
                : string.Empty;
            Debug.Log($"[Rhythm Hunter Rendering] 已將 {cameras.Length} 台 Camera 設為 {rendererName} (Index {rendererIndex}){bufferNote}。請儲存 Scene 以保留設定。");
        }

        private static Camera[] FindOpenSceneCameras()
        {
            Camera[] allCameras = Resources.FindObjectsOfTypeAll<Camera>();
            var sceneCameras = new List<Camera>();
            foreach (Camera camera in allCameras)
            {
                Scene scene = camera.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded && !EditorUtility.IsPersistent(camera))
                    sceneCameras.Add(camera);
            }

            return sceneCameras.ToArray();
        }

        private static int FindRendererIndex(string rendererName)
        {
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
                return -1;

            var serializedPipeline = new SerializedObject(pipeline);
            SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
            if (rendererList == null)
                return -1;

            for (int i = 0; i < rendererList.arraySize; i++)
            {
                SerializedProperty rendererProperty = rendererList.GetArrayElementAtIndex(i);
                ScriptableRendererData renderer = rendererProperty.objectReferenceValue as ScriptableRendererData;
                if (renderer != null && renderer.name == rendererName)
                    return i;
            }

            return -1;
        }

        private static void RepairCartoonFxShadersIfNeeded()
        {
            if (SessionState.GetBool(CartoonFxRepairSessionKey, false))
                return;

            SessionState.SetBool(CartoonFxRepairSessionKey, true);
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                return;

            int reimported = ReimportCartoonFxShaders(true);
            if (reimported > 0)
                Debug.Log($"[Rhythm Hunter Rendering] 偵測到 Cartoon FX 仍是 Built-in Shader，已自動以 URP 重新匯入 {reimported} 個 Shader。");
        }

        private static int ReimportCartoonFxShaders(bool onlyMismatched)
        {
            string[] shaderGuids = AssetDatabase.FindAssets(string.Empty, new[] { CartoonFxShaderFolder });
            var shaderPaths = new HashSet<string>();
            foreach (string guid in shaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".cfxrshader", System.StringComparison.OrdinalIgnoreCase))
                    shaderPaths.Add(path);
            }

            int reimported = 0;
            foreach (string path in shaderPaths)
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                var serializedImporter = new SerializedObject(importer);
                SerializedProperty detectedPipeline = serializedImporter.FindProperty("detectedRenderPipeline");
                bool isUniversal = detectedPipeline != null
                    && detectedPipeline.stringValue.Contains("Universal");
                if (onlyMismatched && isUniversal)
                    continue;

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                reimported++;
            }

            return reimported;
        }
    }
}

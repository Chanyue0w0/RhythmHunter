using UnityEditor;
using UnityEngine;

namespace RhythmHunter.FightDemoEditor
{
    /// <summary>Defaults for new sprites only; never rewrites existing sprite IDs or authored slices.</summary>
    public sealed class FightSpriteImportDefaults : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (!assetPath.StartsWith("Assets/", System.StringComparison.Ordinal) ||
                !importer.importSettingsMissing || importer.textureType != TextureImporterType.Sprite)
                return;

            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}

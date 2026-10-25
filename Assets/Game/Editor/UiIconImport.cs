using UnityEditor;

namespace Game.Editor
{
    public sealed class UiIconImport : AssetPostprocessor
    {
        private const string IconsFolder = "Assets/Game/Art/Resources/Ui/Icons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconsFolder))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}

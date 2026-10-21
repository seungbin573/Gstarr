using UnityEditor;
using UnityEngine;

namespace Gstarr.EditorTools
{
    /// <summary>Assets/arts 에 처음 들어오는 이미지를 픽셀아트 스프라이트로 설정. 이후 Inspector 에서 바꾼 값은 유지됨.</summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/arts/";
        public const int PixelsPerUnit = 16;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder, System.StringComparison.OrdinalIgnoreCase)) return;
            if (!assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}

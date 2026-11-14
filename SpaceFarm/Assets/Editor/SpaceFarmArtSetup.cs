using UnityEditor;
using UnityEngine;

namespace SpaceFarm.Editor
{
    public static class SpaceFarmArtSetup
    {
        [MenuItem("Space Farm/Preparar arte 2D")]
        public static void Configure()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            Debug.Log($"SPACE_FARM_ART_READY: {guids.Length} texturas 2D preparadas.");
        }
    }
}

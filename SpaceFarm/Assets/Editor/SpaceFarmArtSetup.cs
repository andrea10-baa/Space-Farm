using System.IO;
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
                importer.spriteImportMode = SpaceFarmAtlasImporter.SlicesFor(path) == null
                    ? SpriteImportMode.Single : SpriteImportMode.Multiple;
                importer.alphaIsTransparency = true;
                bool terrainDetail = Path.GetFileNameWithoutExtension(path) == "mars-dust-surface";
                importer.mipmapEnabled = terrainDetail;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = terrainDetail ? FilterMode.Trilinear : FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.isReadable = false;
                SpriteMetaData[] slices = SpaceFarmAtlasImporter.SlicesFor(path);
                if (slices != null) SpaceFarmAtlasImporter.AssignSlices(importer, slices);
                importer.SaveAndReimport();
            }
            Debug.Log($"SPACE_FARM_ART_READY: {guids.Length} texturas 2D preparadas.");
        }
    }

    internal sealed class SpaceFarmAtlasImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            SpriteMetaData[] slices = SlicesFor(assetPath);
            if (assetImporter is not TextureImporter importer) return;
            if (Path.GetFileNameWithoutExtension(assetPath) == "mars-dust-surface")
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = false;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 2048;
                return;
            }
            if (slices == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            AssignSlices(importer, slices);
        }

        internal static SpriteMetaData[] SlicesFor(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            switch (name)
            {
                case "space-farm-resource-icons":
                    return Grid("icon", new[] { "iron", "energy", "oxygen", "water", "food", "colonists", "health", "shield" },
                        3, 3, 1254, 1254);
                case "space-farm-world-modules":
                    return Grid("", new[] { "rover-3d", "habitat-3d", "greenhouse-3d", "solar-3d", "oxygen-3d", "water-3d" },
                        3, 2, 1536, 1024);
                case "mars-rock-clusters":
                    return Grid("mars-rock", new[] { "scattered", "boulders", "slabs", "fractured", "gravel", "ridge" },
                        3, 2, 1536, 1024);
                case "space-farm-rover-directions":
                    return Grid("rover-facing", new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" },
                        3, 3, 1261, 1247);
                case "space-farm-colonist-directions":
                    return Grid("colonist-facing", new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" },
                        3, 3, 1261, 1247);
                case "space-farm-colonist-walk":
                    return Grid("colonist-step", new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" },
                        3, 3, 1261, 1247);
                case "space-farm-colonist-pass-a":
                    return Grid("colonist-pass-a", new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" },
                        3, 3, 1265, 1243);
                case "space-farm-colonist-pass-b":
                    return Grid("colonist-pass-b", new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" },
                        3, 3, 1265, 1243);
                case "space-farm-rover-wheels":
                    return Grid("rover-wheel", new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" },
                        3, 3, 1261, 1247);
                case "space-farm-iron-mine-animation":
                    return new[]
                    {
                        Slice("iron-mine-base-3d", 0, 0, 887, 887),
                        Slice("iron-mine-drill-3d", 887, 0, 887, 887)
                    };
                default: return null;
            }
        }

        private static SpriteMetaData[] Grid(string prefix, string[] names, int columns, int rows, int width, int height)
        {
            int cellWidth = width / columns;
            int cellHeight = height / rows;
            SpriteMetaData[] result = new SpriteMetaData[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int column = i % columns;
                int rowFromTop = i / columns;
                int x = column * cellWidth;
                int y = height - (rowFromTop + 1) * cellHeight;
                int sliceWidth = column == columns - 1 ? width - x : cellWidth;
                int sliceHeight = rowFromTop == rows - 1 ? height - rowFromTop * cellHeight : cellHeight;
                result[i] = Slice(prefix.Length == 0 ? names[i] : $"{prefix}-{names[i]}", x, y, sliceWidth, sliceHeight);
            }
            return result;
        }

        private static SpriteMetaData Slice(string name, float x, float y, float width, float height)
        {
            return new SpriteMetaData
            {
                name = name,
                rect = new Rect(x, y, width, height),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(.5f, .5f)
            };
        }

        internal static void AssignSlices(TextureImporter importer, SpriteMetaData[] slices)
        {
#pragma warning disable 0618
            importer.spritesheet = slices;
#pragma warning restore 0618
        }
    }
}

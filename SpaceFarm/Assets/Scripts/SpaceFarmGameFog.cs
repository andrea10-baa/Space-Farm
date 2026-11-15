using System.Collections.Generic;
using UnityEngine;

namespace SpaceFarm
{
    /// <summary>
    /// Fog of war for the large map. Only the cells that are still fading in are touched each tick and
    /// only their pixel rectangle is recomputed; an idle map costs nothing per frame.
    /// </summary>
    public sealed partial class SpaceFarmGame
    {
        private const int FogPixelsPerCell = 4;
        private readonly bool[,] revealed = new bool[Width, Height];
        private readonly float[,] revealAmount = new float[Width, Height];
        private readonly List<Vector2Int> fadingCells = new List<Vector2Int>();
        private Texture2D fogTexture;
        private Color32[] fogPixels;
        private bool fogDirty;
        private int dirtyMinX;
        private int dirtyMinY;
        private int dirtyMaxX;
        private int dirtyMaxY;
        private float fogRefreshTimer;

        private void CreateFog()
        {
            fogTexture = new Texture2D(Width * FogPixelsPerCell, Height * FogPixelsPerCell, TextureFormat.RGBA32, false);
            fogTexture.filterMode = FilterMode.Bilinear;
            fogTexture.wrapMode = TextureWrapMode.Clamp;
            fogPixels = new Color32[fogTexture.width * fogTexture.height];
            Color32 fog = SpaceFarmPalette.FogOverlay;
            for (int i = 0; i < fogPixels.Length; i++) fogPixels[i] = fog;
            fogTexture.SetPixels32(fogPixels);
            fogTexture.Apply(false);
            Sprite fogSprite = Sprite.Create(fogTexture, new Rect(0, 0, fogTexture.width, fogTexture.height),
                new Vector2(.5f, .5f), FogPixelsPerCell / TileSize, 0, SpriteMeshType.FullRect);
            CreateWorldSprite("Niebla de guerra", fogSprite, Vector3.zero,
                new Vector3(1f, SpaceFarmMap.TerrainPerspectiveYScale, 1f), Color.white, 0, worldRoot.transform);
            runtimeAssets.Add(fogSprite);
            runtimeAssets.Add(fogTexture);
            ClearDirty();
        }

        /// <summary>Reveals a disc of cells. Returns true when at least one new cell was uncovered.</summary>
        private bool Reveal(Vector2Int center, int radius)
        {
            bool any = false;
            for (int y = Mathf.Max(0, center.y - radius); y <= Mathf.Min(Height - 1, center.y + radius); y++)
            for (int x = Mathf.Max(0, center.x - radius); x <= Mathf.Min(Width - 1, center.x + radius); x++)
            {
                int dx = x - center.x;
                int dy = y - center.y;
                if (dx * dx + dy * dy > radius * radius || revealed[x, y]) continue;
                revealed[x, y] = true;
                revealAmount[x, y] = 0f;
                fadingCells.Add(new Vector2Int(x, y));
                any = true;
            }
            if (!any) return false;
            MarkMinimapDirty();
            if (depositViews == null) return true;
            foreach (DepositView view in depositViews)
            {
                if (view.Seen || !revealed[view.Info.Cell.x, view.Info.Cell.y]) continue;
                view.Seen = true;
                if (view.Info.Cell == roverDestination) continue;
                bool iron = view.Info.Kind == DepositKind.Iron;
                signalDuringTrip = view.Info;
                SetReport($"Señal de {(iron ? "hierro" : "hielo")} detectada: {view.Info.Name}, al {SpaceFarmMap.Compass(roverCell, view.Info.Cell)} del rover. Hacé clic en el marcador para enviarlo a estudiarla.");
                PlayWorldPing(CellPosition(view.Info.Cell), iron ? SpaceFarmPalette.Iron : SpaceFarmPalette.Ice);
            }
            return true;
        }

        private void UpdateFogReveal()
        {
            if (fadingCells.Count == 0 && !fogDirty) return;
            float step = Time.deltaTime * 3.4f;
            for (int i = fadingCells.Count - 1; i >= 0; i--)
            {
                Vector2Int cell = fadingCells[i];
                float amount = Mathf.MoveTowards(revealAmount[cell.x, cell.y], 1f, step);
                revealAmount[cell.x, cell.y] = amount;
                MarkDirty(cell);
                if (amount < 1f) continue;
                fadingCells[i] = fadingCells[fadingCells.Count - 1];
                fadingCells.RemoveAt(fadingCells.Count - 1);
            }
            fogRefreshTimer += Time.deltaTime;
            if (!fogDirty || fogRefreshTimer < .045f) return;
            fogRefreshTimer = 0f;
            UpdateFogTexture();
        }

        private void MarkDirty(Vector2Int cell)
        {
            fogDirty = true;
            dirtyMinX = Mathf.Min(dirtyMinX, cell.x);
            dirtyMinY = Mathf.Min(dirtyMinY, cell.y);
            dirtyMaxX = Mathf.Max(dirtyMaxX, cell.x);
            dirtyMaxY = Mathf.Max(dirtyMaxY, cell.y);
        }

        private void ClearDirty()
        {
            fogDirty = false;
            dirtyMinX = Width;
            dirtyMinY = Height;
            dirtyMaxX = -1;
            dirtyMaxY = -1;
        }

        private void UpdateFogTexture()
        {
            // A cell softens the fog up to one cell around it, so the rectangle grows by two cells.
            int minX = Mathf.Max(0, dirtyMinX - 2) * FogPixelsPerCell;
            int minY = Mathf.Max(0, dirtyMinY - 2) * FogPixelsPerCell;
            int maxX = (Mathf.Min(Width - 1, dirtyMaxX + 2) + 1) * FogPixelsPerCell;
            int maxY = (Mathf.Min(Height - 1, dirtyMaxY + 2) + 1) * FogPixelsPerCell;
            int pixelWidth = fogTexture.width;
            Color32 fogColor = SpaceFarmPalette.FogOverlay;
            float fullAlpha = fogColor.a;
            for (int py = minY; py < maxY; py++)
            for (int px = minX; px < maxX; px++)
            {
                float gridX = (px + .5f) / FogPixelsPerCell - .5f;
                float gridY = (py + .5f) / FogPixelsPerCell - .5f;
                int cellX = px / FogPixelsPerCell;
                int cellY = py / FogPixelsPerCell;
                float visibility = 0f;
                for (int y = Mathf.Max(0, cellY - 1); y <= Mathf.Min(Height - 1, cellY + 1) && visibility < 1f; y++)
                for (int x = Mathf.Max(0, cellX - 1); x <= Mathf.Min(Width - 1, cellX + 1); x++)
                {
                    if (!revealed[x, y]) continue;
                    float dx = Mathf.Max(0f, Mathf.Abs(gridX - x) - .5f);
                    float dy = Mathf.Max(0f, Mathf.Abs(gridY - y) - .5f);
                    float softEdge = 1f - Mathf.SmoothStep(0f, .92f, Mathf.Sqrt(dx * dx + dy * dy));
                    visibility = Mathf.Max(visibility, revealAmount[x, y] * softEdge);
                }
                fogColor.a = (byte)(fullAlpha * (1f - visibility));
                fogPixels[py * pixelWidth + px] = fogColor;
            }
            fogTexture.SetPixels32(fogPixels);
            fogTexture.Apply(false);
            ClearDirty();
        }
    }
}

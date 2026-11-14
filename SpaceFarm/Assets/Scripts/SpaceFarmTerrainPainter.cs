using UnityEngine;

namespace SpaceFarm
{
    /// <summary>
    /// Paints a generated map into one terrain texture (relief shading, dunes, rock fields, craters,
    /// ridges and a dry channel) plus one average colour per cell for the minimap.
    /// The whole region is a single sprite: no per-tile or per-rock GameObjects.
    /// </summary>
    public static class SpaceFarmTerrainPainter
    {
        public const int PixelsPerCell = 20;
        private const int Coarse = 5;

        private static readonly Color SoilDark = Hex(0x74, 0x43, 0x36);
        private static readonly Color SoilMid = Hex(0x8C, 0x55, 0x44);
        private static readonly Color SoilLight = Hex(0xA8, 0x6C, 0x4E);
        private static readonly Color DuneTint = Hex(0xB8, 0x7A, 0x50);
        private static readonly Color RockTint = Hex(0x5C, 0x39, 0x33);
        private static readonly Color FloorTint = Hex(0x66, 0x3A, 0x30);
        private static readonly Color RimTint = Hex(0xC0, 0x86, 0x62);

        public sealed class Result
        {
            public int Width;
            public int Height;
            public Color32[] Pixels;
            public Color32[] CellColors;
        }

        public static Result Paint(SpaceFarmMap map)
        {
            int width = SpaceFarmMap.Width * PixelsPerCell;
            int height = SpaceFarmMap.Height * PixelsPerCell;
            float[] elevation = new float[width * height];
            float[] tint = new float[width * height];
            float[] dune = new float[width * height];
            float[] rock = new float[width * height];

            // Slow fields are sampled on a coarse grid and interpolated; only grain is evaluated per pixel.
            int gridWidth = width / Coarse + 2;
            int gridHeight = height / Coarse + 2;
            float[] hillGrid = new float[gridWidth * gridHeight];
            float[] toneGrid = new float[gridWidth * gridHeight];
            float[] warpGrid = new float[gridWidth * gridHeight];
            float[] duneGrid = new float[gridWidth * gridHeight];
            float[] rockGrid = new float[gridWidth * gridHeight];
            for (int gy = 0; gy < gridHeight; gy++)
            for (int gx = 0; gx < gridWidth; gx++)
            {
                float x = CellCoordinate(gx * Coarse);
                float y = CellCoordinate(gy * Coarse);
                int index = gy * gridWidth + gx;
                hillGrid[index] = map.Hills(x, y);
                toneGrid[index] = map.Tone(x, y);
                warpGrid[index] = map.Warp(x, y);
                duneGrid[index] = map.DuneMask(x, y);
                rockGrid[index] = map.RockMask(x, y);
            }

            float angle = map.DuneAngle * Mathf.Deg2Rad;
            float duneX = Mathf.Cos(angle);
            float duneY = Mathf.Sin(angle);
            float[] tone = new float[width * height];
            for (int py = 0; py < height; py++)
            {
                float y = CellCoordinate(py);
                int gy = py / Coarse;
                float ty = (py % Coarse) / (float)Coarse;
                for (int px = 0; px < width; px++)
                {
                    float x = CellCoordinate(px);
                    int gx = px / Coarse;
                    float tx = (px % Coarse) / (float)Coarse;
                    int g = gy * gridWidth + gx;
                    float hills = Bilinear(hillGrid, g, gridWidth, tx, ty);
                    float warp = Bilinear(warpGrid, g, gridWidth, tx, ty);
                    float duneMask = Bilinear(duneGrid, g, gridWidth, tx, ty);
                    float rockMask = Bilinear(rockGrid, g, gridWidth, tx, ty);
                    float fade = map.BaseFade(x, y);
                    int index = py * width + px;

                    float value = hills * .9f;
                    float grain = map.ValueNoise(x * 2.3f, y * 2.3f, 101) - .5f;
                    float fine = map.ValueNoise(x * 6.1f, y * 6.1f, 131) - .5f;
                    value += grain * .035f * (.35f + .65f * fade) + fine * .012f;
                    if (duneMask > .01f)
                    {
                        // Asymmetric ripples: a long windward slope and a short slip face.
                        float phase = (x * duneX + y * duneY) * 4.2f + warp * 7f;
                        float wave = Mathf.Sin(phase + .55f * Mathf.Sin(phase));
                        value += wave * .05f * duneMask;
                    }
                    if (rockMask > .01f)
                    {
                        // Two rotated layers hide the noise lattice, so boulders read as scattered rock, not as a grid.
                        float boulders = (map.ValueNoise(x * 2.9f + y * 1.4f, y * 2.9f - x * 1.4f, 151) +
                            map.ValueNoise(x * 1.1f - y * 4.6f, y * 1.1f + x * 4.6f, 163)) * .5f;
                        value += (grain * .05f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.56f, .78f, boulders)) * .075f) * rockMask;
                    }
                    elevation[index] = value;
                    tone[index] = Bilinear(toneGrid, g, gridWidth, tx, ty) * .65f + hills * .35f + fine * .12f;
                    dune[index] = duneMask;
                    rock[index] = rockMask;
                }
            }

            foreach (MapRidge ridge in map.Ridges) StampRidge(map, ridge, elevation, tint, rock, width, height);
            StampChannel(map, elevation, tint, width, height);
            foreach (MapCrater crater in map.Craters) StampCrater(map, crater, elevation, tint, dune, rock, width, height);

            Result result = new Result { Width = width, Height = height, Pixels = new Color32[width * height] };
            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                int index = py * width + px;
                int left = py * width + Mathf.Max(0, px - 1);
                int right = py * width + Mathf.Min(width - 1, px + 1);
                int down = Mathf.Max(0, py - 1) * width + px;
                int up = Mathf.Min(height - 1, py + 1) * width + px;
                // Hard sun from the upper left, as in the art bible.
                float slope = (elevation[left] - elevation[right]) * .72f + (elevation[up] - elevation[down]) * .69f;
                float shade = Mathf.Clamp(1f + slope * 26f, .58f, 1.34f);

                float t = Mathf.Clamp01((tone[index] - .28f) * 2.1f);
                Color color = t < .5f ? Color.Lerp(SoilDark, SoilMid, t * 2f) : Color.Lerp(SoilMid, SoilLight, t * 2f - 1f);
                color = Color.Lerp(color, DuneTint, dune[index] * .55f);
                color = Color.Lerp(color, RockTint, rock[index] * .38f);
                float feature = tint[index];
                if (feature < 0f) color = Color.Lerp(color, FloorTint, Mathf.Min(1f, -feature));
                else if (feature > 0f) color = Color.Lerp(color, RimTint, Mathf.Min(1f, feature));
                result.Pixels[index] = new Color32(
                    (byte)Mathf.Clamp(color.r * shade * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(color.g * shade * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(color.b * shade * 255f, 0f, 255f), 255);
            }

            result.CellColors = new Color32[SpaceFarmMap.Width * SpaceFarmMap.Height];
            for (int cy = 0; cy < SpaceFarmMap.Height; cy++)
            for (int cx = 0; cx < SpaceFarmMap.Width; cx++)
            {
                int r = 0, g = 0, b = 0, samples = 0;
                for (int sy = 2; sy < PixelsPerCell; sy += 5)
                for (int sx = 2; sx < PixelsPerCell; sx += 5)
                {
                    Color32 pixel = result.Pixels[(cy * PixelsPerCell + sy) * width + cx * PixelsPerCell + sx];
                    r += pixel.r;
                    g += pixel.g;
                    b += pixel.b;
                    samples++;
                }
                result.CellColors[cy * SpaceFarmMap.Width + cx] =
                    new Color32((byte)(r / samples), (byte)(g / samples), (byte)(b / samples), 255);
            }
            return result;
        }

        /// <summary>Seamless grain tile drawn over the terrain so close zoom keeps texture.</summary>
        public static Color32[] PaintGrain(int size, int seed)
        {
            SpaceFarmRandom random = new SpaceFarmRandom((ulong)(uint)seed * 31UL + 7UL);
            const int lattice = 16;
            float[] values = new float[lattice * lattice];
            for (int i = 0; i < values.Length; i++) values[i] = random.Value();
            float[] fineValues = new float[64 * 64];
            for (int i = 0; i < fineValues.Length; i++) fineValues[i] = random.Value();
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float coarse = Periodic(values, lattice, x * lattice / (float)size, y * lattice / (float)size);
                float fine = Periodic(fineValues, 64, x * 64f / size, y * 64f / size);
                float value = (coarse - .5f) * .7f + (fine - .5f) * .9f;
                byte alpha = (byte)Mathf.Clamp(Mathf.Abs(value) * 150f, 0f, 70f);
                pixels[y * size + x] = value < 0f ? new Color32(40, 18, 14, alpha) : new Color32(255, 214, 176, alpha);
            }
            return pixels;
        }

        private static float Periodic(float[] values, int lattice, float x, float y)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = ((ix % lattice) + lattice) % lattice;
            int y0 = ((iy % lattice) + lattice) % lattice;
            int x1 = (x0 + 1) % lattice;
            int y1 = (y0 + 1) % lattice;
            return Mathf.Lerp(Mathf.Lerp(values[y0 * lattice + x0], values[y0 * lattice + x1], fx),
                Mathf.Lerp(values[y1 * lattice + x0], values[y1 * lattice + x1], fx), fy);
        }

        private static void StampCrater(SpaceFarmMap map, MapCrater crater, float[] elevation, float[] tint,
            float[] dune, float[] rock, int width, int height)
        {
            float reach = crater.Radius * 1.9f;
            float amplitude = SpaceFarmMap.CraterAmplitude(crater.Radius);
            PixelBounds(crater.X - reach, crater.Y - reach, crater.X + reach, crater.Y + reach, width, height,
                out int minX, out int minY, out int maxX, out int maxY);
            for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                float x = CellCoordinate(px);
                float y = CellCoordinate(py);
                float dx = x - crater.X;
                float dy = y - crater.Y;
                // A wobbling rim keeps craters from looking like stamped circles.
                float wobble = 1f + (map.ValueNoise(x * .9f + crater.X, y * .9f + crater.Y, 211) - .5f) * .16f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (crater.Radius * wobble);
                if (d > 1.9f) continue;
                int index = py * width + px;
                elevation[index] += SpaceFarmMap.CraterProfile(d) * amplitude;
                if (d < .8f)
                {
                    float inside = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .8f, d));
                    tint[index] = Mathf.Min(tint[index], -.6f * inside);
                    dune[index] *= 1f - inside;
                    rock[index] *= 1f - inside * .6f;
                }
                else if (d < 1.9f)
                {
                    float rim = 1f - Mathf.Abs(d - 1.02f) / .28f;
                    float rays = map.ValueNoise(Mathf.Atan2(dy, dx) * 5f + crater.X, crater.Radius, 223);
                    float ejecta = (1f - Mathf.InverseLerp(1.1f, 1.9f, d)) * Mathf.Max(0f, rays - .45f) * .7f;
                    float light = Mathf.Max(Mathf.Max(0f, rim) * .55f, ejecta);
                    if (light > 0f && tint[index] >= 0f) tint[index] = Mathf.Max(tint[index], light);
                }
            }
        }

        private static void StampRidge(SpaceFarmMap map, MapRidge ridge, float[] elevation, float[] tint,
            float[] rock, int width, int height)
        {
            float reach = ridge.Width * 1.8f;
            PixelBounds(Mathf.Min(ridge.From.x, ridge.To.x) - reach, Mathf.Min(ridge.From.y, ridge.To.y) - reach,
                Mathf.Max(ridge.From.x, ridge.To.x) + reach, Mathf.Max(ridge.From.y, ridge.To.y) + reach,
                width, height, out int minX, out int minY, out int maxX, out int maxY);
            for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                float x = CellCoordinate(px);
                float y = CellCoordinate(py);
                float jag = .65f + map.ValueNoise(x * 1.3f, y * 1.3f, 241) * .7f;
                float d = SpaceFarmMap.SegmentDistance(new Vector2(x, y), ridge.From, ridge.To) / (ridge.Width * jag);
                if (d > 1.8f) continue;
                int index = py * width + px;
                float crest = Mathf.Exp(-d * d * 1.9f);
                elevation[index] += crest * (.2f + map.ValueNoise(x * 2.6f, y * 2.6f, 251) * .14f);
                rock[index] = Mathf.Max(rock[index], crest * .85f);
                if (crest > .75f) tint[index] = Mathf.Max(tint[index], (crest - .75f) * 1.2f);
            }
        }

        private static void StampChannel(SpaceFarmMap map, float[] elevation, float[] tint, int width, int height)
        {
            for (int i = 0; i < map.Channel.Count - 1; i++)
            {
                Vector2 a = map.Channel[i];
                Vector2 b = map.Channel[i + 1];
                float reach = map.ChannelWidth * 2.4f;
                PixelBounds(Mathf.Min(a.x, b.x) - reach, Mathf.Min(a.y, b.y) - reach,
                    Mathf.Max(a.x, b.x) + reach, Mathf.Max(a.y, b.y) + reach,
                    width, height, out int minX, out int minY, out int maxX, out int maxY);
                for (int py = minY; py <= maxY; py++)
                for (int px = minX; px <= maxX; px++)
                {
                    float x = CellCoordinate(px);
                    float y = CellCoordinate(py);
                    float meander = (map.ValueNoise(x * .55f, y * .55f, 261) - .5f) * 1.1f;
                    float d = (SpaceFarmMap.SegmentDistance(new Vector2(x, y), a, b) + meander) / map.ChannelWidth;
                    if (d > 2.2f) continue;
                    int index = py * width + px;
                    float cut = -.2f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1.5f, Mathf.Max(0f, d))));
                    // Segments overlap at the joints: keep the deepest cut instead of adding them up.
                    if (cut < 0f)
                    {
                        float carved = Mathf.Min(0f, cut - Mathf.Min(0f, tint[index] / 2.4f));
                        elevation[index] += carved;
                        tint[index] = Mathf.Min(tint[index], cut * 2.4f);
                    }
                }
            }
        }

        private static float CellCoordinate(int pixel) => (pixel + .5f) / PixelsPerCell - .5f;

        private static void PixelBounds(float minCellX, float minCellY, float maxCellX, float maxCellY,
            int width, int height, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = Mathf.Clamp(Mathf.FloorToInt((minCellX + .5f) * PixelsPerCell), 0, width - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt((minCellY + .5f) * PixelsPerCell), 0, height - 1);
            maxX = Mathf.Clamp(Mathf.CeilToInt((maxCellX + .5f) * PixelsPerCell), 0, width - 1);
            maxY = Mathf.Clamp(Mathf.CeilToInt((maxCellY + .5f) * PixelsPerCell), 0, height - 1);
        }

        private static float Bilinear(float[] grid, int index, int gridWidth, float tx, float ty)
        {
            float bottom = grid[index] + (grid[index + 1] - grid[index]) * tx;
            float top = grid[index + gridWidth] + (grid[index + gridWidth + 1] - grid[index + gridWidth]) * tx;
            return bottom + (top - bottom) * ty;
        }

        private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}

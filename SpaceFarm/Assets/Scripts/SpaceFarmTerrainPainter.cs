using UnityEngine;

namespace SpaceFarm
{
    /// <summary>
    /// Paints broad terrain relief, dunes, craters and a dry channel into one ground texture,
    /// plus one average colour per cell for the minimap. Rock sprites are placed separately.
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
                    // Fine dust belongs in the colour, not the height field: sharp height noise
                    // made every grain cast an oversized black/white shadow at game zoom.
                    value += grain * .008f * (.35f + .65f * fade) + fine * .002f;
                    if (duneMask > .01f)
                    {
                        // Asymmetric ripples: a long windward slope and a short slip face.
                        float phase = (x * duneX + y * duneY) * 4.2f + warp * 13f;
                        float wave = Mathf.Sin(phase + .55f * Mathf.Sin(phase));
                        value += wave * .022f * duneMask;
                    }
                    if (rockMask > .01f)
                    {
                        // Two rotated layers hide the noise lattice, so boulders read as scattered rock, not as a grid.
                        float boulders = (map.ValueNoise(x * 2.9f + y * 1.4f, y * 2.9f - x * 1.4f, 151) +
                            map.ValueNoise(x * 1.1f - y * 4.6f, y * 1.1f + x * 4.6f, 163)) * .5f;
                        value += (grain * .012f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.56f, .78f, boulders)) * .025f) * rockMask;
                    }
                    elevation[index] = value;
                    tone[index] = Bilinear(toneGrid, g, gridWidth, tx, ty) * .65f + hills * .35f + fine * .035f;
                    dune[index] = duneMask;
                    rock[index] = rockMask;
                }
            }

            foreach (MapRidge ridge in map.Ridges) StampRidge(map, ridge, elevation, tint, rock, width, height);
            StampChannel(map, elevation, tint, width, height);
            foreach (MapCrater crater in map.Craters) StampCrater(map, crater, elevation, tint, dune, rock, width, height);
            ScatterBoulders(map, elevation, tint, rock, width, height);

            Result result = new Result { Width = width, Height = height, Pixels = new Color32[width * height] };
            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                int index = py * width + px;
                // Shade the large landforms, not each dust pixel. The wider sample preserves
                // readable crater walls without turning the plain into bright noise.
                const int reliefSample = 5;
                int left = py * width + Mathf.Max(0, px - reliefSample);
                int right = py * width + Mathf.Min(width - 1, px + reliefSample);
                int down = Mathf.Max(0, py - reliefSample) * width + px;
                int up = Mathf.Min(height - 1, py + reliefSample) * width + px;
                float slope = (elevation[left] - elevation[right]) * .72f + (elevation[up] - elevation[down]) * .69f;
                float shade = Mathf.Clamp(1f + slope * 6f, .78f, 1.22f);

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
                Vector2 radial = new Vector2(dx, dy).normalized;
                float sunFacing = Mathf.Clamp01(.5f + Vector2.Dot(radial, new Vector2(-.58f, .81f)) * .55f);
                elevation[index] += SpaceFarmMap.CraterProfile(d) * amplitude;
                if (d < .8f)
                {
                    float inside = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .8f, d));
                    float nearLip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.58f, .8f, d));
                    float castShadow = inside * (.38f + (1f - sunFacing) * .45f * nearLip);
                    tint[index] = Mathf.Min(tint[index], -.72f * inside - castShadow * .35f);
                    dune[index] *= 1f - inside;
                    rock[index] *= 1f - inside * .6f;
                }
                else if (d < 1.9f)
                {
                    float rim = 1f - Mathf.Abs(d - 1.02f) / .28f;
                    float rays = map.ValueNoise(Mathf.Atan2(dy, dx) * 5f + crater.X, crater.Radius, 223);
                    float ejecta = (1f - Mathf.InverseLerp(1.1f, 1.9f, d)) * Mathf.Max(0f, rays - .45f) * .7f;
                    float darkLip = Mathf.Max(0f, rim) * (1f - sunFacing) * .6f;
                    if (darkLip > 0f && tint[index] >= 0f) tint[index] = Mathf.Min(tint[index], -darkLip);
                    float light = Mathf.Max(Mathf.Max(0f, rim) * (.35f + sunFacing * .75f), ejecta * (.45f + sunFacing * .55f));
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

        private static void ScatterBoulders(SpaceFarmMap map, float[] elevation, float[] tint, float[] rock,
            int width, int height)
        {
            int count = SpaceFarmMap.Width * SpaceFarmMap.Height / 42;
            Vector2 lightDirection = new Vector2(-.58f, .81f);
            for (int i = 0; i < count; i++)
            {
                float sample = i + 1f;
                float centerX = map.ValueNoise(sample * 13.17f, sample * 2.31f, 347) * (SpaceFarmMap.Width - 2) + 1f;
                float centerY = map.ValueNoise(sample * 5.93f, sample * 17.41f, 353) * (SpaceFarmMap.Height - 2) + 1f;
                if (map.BaseFade(centerX, centerY) < .88f) continue;

                float radius = .18f + map.ValueNoise(sample * 7.73f, sample * 11.19f, 359) * .4f;
                float radiusY = radius * (.68f + map.ValueNoise(sample * 3.07f, sample * 19.67f, 367) * .48f);
                float angle = map.ValueNoise(sample * 23.09f, sample * 29.53f, 373) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float heightScale = .1f + map.ValueNoise(sample * 31.73f, sample * 37.61f, 379) * .2f;
                float phase = map.ValueNoise(sample * 41.27f, sample * 43.81f, 383) * Mathf.PI * 2f;
                float reach = radius * 1.75f;
                PixelBounds(centerX - reach, centerY - reach, centerX + reach, centerY + reach,
                    width, height, out int minX, out int minY, out int maxX, out int maxY);

                for (int py = minY; py <= maxY; py++)
                for (int px = minX; px <= maxX; px++)
                {
                    float x = CellCoordinate(px) - centerX;
                    float y = CellCoordinate(py) - centerY;
                    float localX = (x * cos + y * sin) / radius;
                    float localY = (-x * sin + y * cos) / radiusY;
                    float distance = Mathf.Sqrt(localX * localX + localY * localY);
                    float edge = .79f + Mathf.Sin(Mathf.Atan2(localY, localX) * 5f + phase) * .1f
                        + Mathf.Sin(Mathf.Atan2(localY, localX) * 3f - phase * 1.7f) * .07f;
                    int index = py * width + px;
                    if (distance <= edge)
                    {
                        float crown = 1f - Mathf.SmoothStep(0f, 1f, distance / edge);
                        float faceted = crown * (.82f + .18f * Mathf.Sin(Mathf.Atan2(localY, localX) * 3f + phase));
                        elevation[index] += heightScale * Mathf.Pow(Mathf.Max(0f, faceted), .72f);
                        rock[index] = Mathf.Max(rock[index], .72f + crown * .23f);
                        float litFace = Mathf.Clamp01(.5f + Vector2.Dot(new Vector2(x, y).normalized, lightDirection) * .55f);
                        if (tint[index] >= 0f) tint[index] = Mathf.Max(tint[index], crown * litFace * .22f);
                        continue;
                    }

                    // A compact southeast cast shadow makes each stone sit on the dust instead of reading as a flat mark.
                    float shadowX = x - radius * .34f;
                    float shadowY = y + radius * .38f;
                    float shadow = Mathf.Sqrt(shadowX * shadowX + shadowY * shadowY * .9f) / (radius * 1.45f);
                    if (shadow < 1f)
                    {
                        float strength = (1f - Mathf.SmoothStep(0f, 1f, shadow)) * .34f;
                        tint[index] = Mathf.Min(tint[index], -strength);
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFarm
{
    public enum DepositKind { Iron, Ice }

    public enum TerrainKind : byte { Plain, Dunes, Rocky, CraterFloor, CraterRim, Ridge, Channel }

    /// <summary>Own generator so a seed gives the same Mars on every platform and never touches UnityEngine.Random.</summary>
    public struct SpaceFarmRandom
    {
        private ulong state;

        public SpaceFarmRandom(ulong seed) { state = seed + 0x9E3779B97F4A7C15UL; }

        public uint NextUInt()
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (uint)((z ^ (z >> 31)) >> 32);
        }

        /// <summary>Integer in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        /// <summary>Float in [0, 1).</summary>
        public float Value() => (NextUInt() >> 8) / 16777216f;

        public float Range(float min, float max) => min + (max - min) * Value();
    }

    public sealed class MapCrater
    {
        public float X;
        public float Y;
        public float Radius;
    }

    public sealed class MapRidge
    {
        public Vector2 From;
        public Vector2 To;
        public float Width;
    }

    public sealed class MapDeposit
    {
        public int Id;
        public DepositKind Kind;
        public Vector2Int Cell;
        public int Distance;
        public string Name;
    }

    public sealed class MapPoint
    {
        public int Kind;
        public Vector2Int Cell;
    }

    /// <summary>
    /// One generated Mars region. Everything here derives from <see cref="Seed"/>: terrain fields,
    /// craters, landing site, deposits and points of interest. All formations are visual relief:
    /// the rover can drive over every cell, so no seed can wall off a resource.
    /// </summary>
    public sealed class SpaceFarmMap
    {
        public const int Width = 128;
        public const int Height = 80;
        public const float TerrainPerspectiveYScale = .78f;
        public const int MaxAttempts = 64;
        public const int BaseClearRadius = 5;
        public const int NearIronMaxDistance = 9;
        public const int NearIceMaxDistance = 10;
        public const int FarDistance = 22;
        public const int DiscoveryKindCount = 4;
        private const int EdgeMargin = 3;
        private const int DepositSeparation = 7;
        private const int PointSeparation = 5;

        public int Seed { get; private set; }
        public int Attempt { get; private set; }
        public Vector2Int BaseCell { get; private set; }
        public float DuneAngle { get; private set; }
        public readonly List<MapCrater> Craters = new List<MapCrater>();
        public readonly List<MapRidge> Ridges = new List<MapRidge>();
        public readonly List<Vector2> Channel = new List<Vector2>();
        public float ChannelWidth { get; private set; }
        public readonly List<MapDeposit> Deposits = new List<MapDeposit>();
        public readonly List<MapPoint> Points = new List<MapPoint>();
        public readonly TerrainKind[,] Terrain = new TerrainKind[Width, Height];
        public ulong Signature { get; private set; }

        private uint noiseSeed;

        private SpaceFarmMap() { }

        /// <summary>Builds the map for a seed. Invalid layouts are regenerated with the next attempt of the same seed.</summary>
        public static SpaceFarmMap Generate(int seed)
        {
            string lastError = "";
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                SpaceFarmMap map = new SpaceFarmMap { Seed = seed, Attempt = attempt };
                if (map.Build() && map.Validate(out lastError)) return map;
            }
            throw new InvalidOperationException($"No se pudo generar un mapa válido para la semilla {seed}: {lastError}");
        }

        public static int CellDistance(Vector2Int a, Vector2Int b)
        {
            int dx = a.x - b.x;
            int dy = a.y - b.y;
            return Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dy * dy));
        }

        public static string Compass(Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (dx == 0 && dy == 0) return "acá";
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            string[] names = { "este", "noreste", "norte", "noroeste", "oeste", "suroeste", "sur", "sureste" };
            return names[Mathf.RoundToInt(angle / 45f) % 8];
        }

        public bool Inside(Vector2Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public MapDeposit DepositAt(Vector2Int cell)
        {
            for (int i = 0; i < Deposits.Count; i++)
                if (Deposits[i].Cell == cell) return Deposits[i];
            return null;
        }

        public int PointAt(Vector2Int cell)
        {
            for (int i = 0; i < Points.Count; i++)
                if (Points[i].Cell == cell) return i;
            return -1;
        }

        public MapDeposit Nearest(DepositKind kind)
        {
            for (int i = 0; i < Deposits.Count; i++)
                if (Deposits[i].Kind == kind) return Deposits[i];
            return null;
        }

        // ---------- Terrain fields, shared by the cell classification and the texture painter ----------

        /// <summary>0 at the landing site, 1 away from it. Keeps the colony area flat and clean.</summary>
        public float BaseFade(float x, float y)
        {
            float dx = x - BaseCell.x;
            float dy = y - BaseCell.y;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 7.5f, Mathf.Sqrt(dx * dx + dy * dy)));
        }

        public float Hills(float x, float y) => Fbm(x * .05f, y * .05f, 4, 11);
        public float Tone(float x, float y) => Fbm(x * .09f + 40f, y * .09f, 3, 23);
        public float Warp(float x, float y) => Fbm(x * .07f, y * .07f + 90f, 2, 37);
        private float RawDune(float x, float y) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.50f, .64f, Fbm(x * .03f + 100f, y * .03f, 2, 53)));
        private float RawRock(float x, float y) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.54f, .68f, Fbm(x * .042f, y * .042f + 300f, 2, 71)));
        public float DuneMask(float x, float y) => RawDune(x, y) * BaseFade(x, y);
        public float RockMask(float x, float y) => RawRock(x, y) * (1f - RawDune(x, y)) * BaseFade(x, y);

        /// <summary>Crater height profile for a normalized distance (0 center, 1 rim crest).</summary>
        public static float CraterProfile(float d)
        {
            if (d < 1f) return Mathf.Lerp(-1f, .55f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, 1f, d)));
            return .55f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 1.45f, d)));
        }

        public static float CraterAmplitude(float radius) => Mathf.Min(.66f, .16f + .052f * radius);

        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length = ab.sqrMagnitude;
            float t = length < 1e-5f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / length);
            return Vector2.Distance(p, a + ab * t);
        }

        public float ValueNoise(float x, float y, uint channel)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            fy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float a = Hash(ix, iy, channel);
            float b = Hash(ix + 1, iy, channel);
            float c = Hash(ix, iy + 1, channel);
            float d = Hash(ix + 1, iy + 1, channel);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private float Fbm(float x, float y, int octaves, uint channel)
        {
            float sum = 0f;
            float amplitude = .5f;
            float total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, channel + (uint)i * 7919u) * amplitude;
                total += amplitude;
                amplitude *= .5f;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.7f;
            }
            return sum / total;
        }

        private float Hash(int x, int y, uint channel)
        {
            uint h = noiseSeed ^ (channel * 0x9E3779B1u);
            h ^= (uint)x * 0x85EBCA6Bu;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xC2B2AE35u;
            h *= 0x27D4EB2Fu;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return (h >> 8) / 16777216f;
        }

        // ---------- Generation ----------

        private bool Build()
        {
            ulong mixed = unchecked((ulong)(uint)Seed * 0x9E3779B97F4A7C15UL + (ulong)Attempt * 0xD1B54A32D192ED03UL + 0x5F4AUL);
            SpaceFarmRandom random = new SpaceFarmRandom(mixed);
            noiseSeed = random.NextUInt();
            DuneAngle = random.Range(15f, 75f) * (random.Range(0, 2) == 0 ? 1f : -1f);
            ChooseBase();
            PlaceCraters(ref random);
            PlaceRidges(ref random);
            PlaceChannel(ref random);
            if (!PlaceDeposits(ref random)) return false;
            if (!PlacePoints(ref random)) return false;
            ClassifyTerrain();
            ComputeSignature();
            return true;
        }

        /// <summary>The lander looks for the calmest ground near the middle of the region.</summary>
        private void ChooseBase()
        {
            float best = float.MaxValue;
            Vector2Int chosen = new Vector2Int(Width / 2, Height / 2);
            for (int y = Height / 2 - 4; y <= Height / 2 + 4; y++)
            for (int x = Width / 2 - 7; x <= Width / 2 + 7; x++)
            {
                float rough = 0f;
                for (int oy = -3; oy <= 3; oy += 3)
                for (int ox = -3; ox <= 3; ox += 3)
                    rough += RawDune(x + ox, y + oy) + RawRock(x + ox, y + oy);
                float slope = Mathf.Abs(Hills(x + 2, y) - Hills(x - 2, y)) + Mathf.Abs(Hills(x, y + 2) - Hills(x, y - 2));
                float score = rough + slope * 12f;
                if (score < best - 1e-4f)
                {
                    best = score;
                    chosen = new Vector2Int(x, y);
                }
            }
            BaseCell = chosen;
        }

        private void PlaceCraters(ref SpaceFarmRandom random)
        {
            for (int tries = 0; tries < 800 && Craters.Count < 2; tries++)
            {
                float radius = tries > 400 && Craters.Count == 0 ? 5f : random.Range(5.2f, 7.4f);
                TryAddCrater(ref random, radius, true);
            }
            int medium = random.Range(5, 9);
            for (int i = 0, tries = 0; i < medium && tries < 300; tries++)
                if (TryAddCrater(ref random, random.Range(2.4f, 3.9f), true)) i++;
            int small = random.Range(26, 43);
            for (int i = 0, tries = 0; i < small && tries < 1200; tries++)
                if (TryAddCrater(ref random, random.Range(.7f, 1.7f), false)) i++;
        }

        private bool TryAddCrater(ref SpaceFarmRandom random, float radius, bool separated)
        {
            float reach = radius * 1.45f + 1f;
            float x = random.Range(reach, Width - 1 - reach);
            float y = random.Range(reach, Height - 1 - reach);
            float dx = x - BaseCell.x;
            float dy = y - BaseCell.y;
            if (Mathf.Sqrt(dx * dx + dy * dy) < radius * 1.45f + BaseClearRadius + 1.5f) return false;
            foreach (MapCrater other in Craters)
            {
                float distance = Mathf.Sqrt((other.X - x) * (other.X - x) + (other.Y - y) * (other.Y - y));
                float limit = separated ? (other.Radius + radius) * 1.5f : (other.Radius + radius) * .9f;
                if (distance < limit) return false;
            }
            Craters.Add(new MapCrater { X = x, Y = y, Radius = radius });
            return true;
        }

        private void PlaceRidges(ref SpaceFarmRandom random)
        {
            int count = random.Range(4, 7);
            for (int i = 0, tries = 0; i < count && tries < 200; tries++)
            {
                Vector2 from = new Vector2(random.Range(6f, Width - 7f), random.Range(5f, Height - 6f));
                float angle = random.Range(0f, Mathf.PI * 2f);
                float length = random.Range(10f, 23f);
                Vector2 to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                if (to.x < 4f || to.x > Width - 5f || to.y < 4f || to.y > Height - 5f) continue;
                if (SegmentDistance(BaseCell, from, to) < BaseClearRadius + 4f) continue;
                Ridges.Add(new MapRidge { From = from, To = to, Width = random.Range(1.1f, 1.8f) });
                i++;
            }
        }

        private void PlaceChannel(ref SpaceFarmRandom random)
        {
            ChannelWidth = random.Range(.7f, 1.1f);
            for (int tries = 0; tries < 60; tries++)
            {
                List<Vector2> path = new List<Vector2>();
                Vector2 point = new Vector2(random.Range(8f, Width - 9f), random.Range(6f, Height - 7f));
                float heading = random.Range(0f, Mathf.PI * 2f);
                int nodes = random.Range(10, 17);
                bool valid = true;
                for (int i = 0; i < nodes && valid; i++)
                {
                    path.Add(point);
                    heading += random.Range(-.75f, .75f);
                    point += new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * random.Range(3.5f, 5.5f);
                    if (point.x < 2f || point.x > Width - 3f || point.y < 2f || point.y > Height - 3f) break;
                    if (Vector2.Distance(point, BaseCell) < BaseClearRadius + 4f) valid = false;
                }
                if (!valid || path.Count < 5) continue;
                for (int i = 0; i < path.Count - 1 && valid; i++)
                    if (SegmentDistance(BaseCell, path[i], path[i + 1]) < BaseClearRadius + 3f) valid = false;
                if (!valid) continue;
                Channel.AddRange(path);
                return;
            }
        }

        private bool PlaceDeposits(ref SpaceFarmRandom random)
        {
            int ironCount = random.Range(6, 11);
            int iceCount = random.Range(4, 7);
            List<Vector2Int> iron = new List<Vector2Int>();
            List<Vector2Int> ice = new List<Vector2Int>();

            // Early access first: one iron and one ice deposit within a short drive of the lander.
            if (!PickRing(ref random, 5, 8, null, 0, out Vector2Int nearIron)) return false;
            iron.Add(nearIron);
            if (!PickRing(ref random, 5, 9, iron, 5, out Vector2Int nearIce)) return false;
            ice.Add(nearIce);

            // The rest are spread with best-candidate sampling so exploring pays off in every direction.
            for (int i = 0; i < ironCount + iceCount - 2; i++)
            {
                bool wantIron = iron.Count < ironCount && (ice.Count >= iceCount || i % 3 != 2);
                List<Vector2Int> same = wantIron ? iron : ice;
                List<Vector2Int> other = wantIron ? ice : iron;
                if (!PickSpread(ref random, same, other, out Vector2Int cell)) return false;
                same.Add(cell);
            }

            AddDeposits(iron, DepositKind.Iron, "Fe");
            AddDeposits(ice, DepositKind.Ice, "Hielo");
            return true;
        }

        private void AddDeposits(List<Vector2Int> cells, DepositKind kind, string prefix)
        {
            Vector2Int home = BaseCell;
            List<Vector2Int> ordered = new List<Vector2Int>(cells);
            // Stable insertion sort by distance, then by coordinates: ids never depend on List.Sort internals.
            for (int i = 1; i < ordered.Count; i++)
            {
                Vector2Int item = ordered[i];
                int j = i - 1;
                while (j >= 0 && Closer(item, ordered[j], home))
                {
                    ordered[j + 1] = ordered[j];
                    j--;
                }
                ordered[j + 1] = item;
            }
            for (int i = 0; i < ordered.Count; i++)
                Deposits.Add(new MapDeposit
                {
                    Id = Deposits.Count,
                    Kind = kind,
                    Cell = ordered[i],
                    Distance = CellDistance(ordered[i], home),
                    Name = $"{prefix}-{i + 1}"
                });
        }

        private static bool Closer(Vector2Int a, Vector2Int b, Vector2Int home)
        {
            int da = (a.x - home.x) * (a.x - home.x) + (a.y - home.y) * (a.y - home.y);
            int db = (b.x - home.x) * (b.x - home.x) + (b.y - home.y) * (b.y - home.y);
            if (da != db) return da < db;
            return a.x != b.x ? a.x < b.x : a.y < b.y;
        }

        private bool PickRing(ref SpaceFarmRandom random, int minDistance, int maxDistance,
            List<Vector2Int> avoid, int separation, out Vector2Int cell)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int y = EdgeMargin; y < Height - EdgeMargin; y++)
            for (int x = EdgeMargin; x < Width - EdgeMargin; x++)
            {
                int dx = x - BaseCell.x;
                int dy = y - BaseCell.y;
                int squared = dx * dx + dy * dy;
                if (squared < minDistance * minDistance || squared > maxDistance * maxDistance) continue;
                Vector2Int candidate = new Vector2Int(x, y);
                if (avoid != null && MinSquared(candidate, avoid) < separation * separation) continue;
                candidates.Add(candidate);
            }
            cell = default;
            if (candidates.Count == 0) return false;
            cell = candidates[random.Range(0, candidates.Count)];
            return true;
        }

        private bool PickSpread(ref SpaceFarmRandom random, List<Vector2Int> same, List<Vector2Int> other, out Vector2Int cell)
        {
            cell = default;
            int bestScore = -1;
            for (int round = 0; round < 5 && bestScore < 0; round++)
            for (int i = 0; i < 30; i++)
            {
                Vector2Int candidate = new Vector2Int(random.Range(EdgeMargin, Width - EdgeMargin),
                    random.Range(EdgeMargin, Height - EdgeMargin));
                int home = (candidate.x - BaseCell.x) * (candidate.x - BaseCell.x) + (candidate.y - BaseCell.y) * (candidate.y - BaseCell.y);
                if (home < 11 * 11) continue;
                int sameDistance = MinSquared(candidate, same);
                int otherDistance = MinSquared(candidate, other);
                if (sameDistance < DepositSeparation * DepositSeparation || otherDistance < DepositSeparation * DepositSeparation) continue;
                // Same-kind deposits repel each other more, so iron and ice each cover the whole region.
                int score = Mathf.Min(sameDistance, otherDistance * 2);
                if (score > bestScore)
                {
                    bestScore = score;
                    cell = candidate;
                }
            }
            return bestScore >= 0;
        }

        private static int MinSquared(Vector2Int cell, List<Vector2Int> others)
        {
            int best = int.MaxValue;
            for (int i = 0; i < others.Count; i++)
            {
                int dx = cell.x - others[i].x;
                int dy = cell.y - others[i].y;
                best = Mathf.Min(best, dx * dx + dy * dy);
            }
            return best;
        }

        private bool PlacePoints(ref SpaceFarmRandom random)
        {
            int count = random.Range(4, 7);
            int firstKind = random.Range(0, DiscoveryKindCount);
            List<Vector2Int> taken = new List<Vector2Int>();
            foreach (MapDeposit deposit in Deposits) taken.Add(deposit.Cell);
            List<Vector2Int> placed = new List<Vector2Int>();
            for (int i = 0; i < count; i++)
            {
                int bestScore = -1;
                Vector2Int best = default;
                for (int tries = 0; tries < 60; tries++)
                {
                    Vector2Int candidate = new Vector2Int(random.Range(EdgeMargin, Width - EdgeMargin),
                        random.Range(EdgeMargin, Height - EdgeMargin));
                    int home = (candidate.x - BaseCell.x) * (candidate.x - BaseCell.x) + (candidate.y - BaseCell.y) * (candidate.y - BaseCell.y);
                    if (home < 8 * 8) continue;
                    if (MinSquared(candidate, taken) < PointSeparation * PointSeparation) continue;
                    int score = placed.Count == 0 ? 100000 : MinSquared(candidate, placed);
                    if (score < PointSeparation * PointSeparation) continue;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
                if (bestScore < 0) return false;
                placed.Add(best);
                Points.Add(new MapPoint { Kind = (firstKind + i) % DiscoveryKindCount, Cell = best });
            }
            return true;
        }

        private void ClassifyTerrain()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                TerrainKind kind = TerrainKind.Plain;
                if (DuneMask(x, y) > .5f) kind = TerrainKind.Dunes;
                else if (RockMask(x, y) > .5f) kind = TerrainKind.Rocky;
                Vector2 p = new Vector2(x, y);
                foreach (MapRidge ridge in Ridges)
                    if (SegmentDistance(p, ridge.From, ridge.To) < ridge.Width) kind = TerrainKind.Ridge;
                for (int i = 0; i < Channel.Count - 1; i++)
                    if (SegmentDistance(p, Channel[i], Channel[i + 1]) < ChannelWidth) kind = TerrainKind.Channel;
                foreach (MapCrater crater in Craters)
                {
                    float d = Mathf.Sqrt((crater.X - x) * (crater.X - x) + (crater.Y - y) * (crater.Y - y)) / crater.Radius;
                    if (d < .8f) kind = TerrainKind.CraterFloor;
                    else if (d < 1.2f) kind = TerrainKind.CraterRim;
                }
                Terrain[x, y] = kind;
            }
        }

        private void ComputeSignature()
        {
            ulong hash = 14695981039346656037UL;
            void Mix(int value)
            {
                unchecked
                {
                    hash ^= (uint)value;
                    hash *= 1099511628211UL;
                }
            }
            Mix(BaseCell.x);
            Mix(BaseCell.y);
            foreach (MapCrater crater in Craters)
            {
                Mix(Mathf.RoundToInt(crater.X * 16f));
                Mix(Mathf.RoundToInt(crater.Y * 16f));
                Mix(Mathf.RoundToInt(crater.Radius * 16f));
            }
            foreach (MapRidge ridge in Ridges)
            {
                Mix(Mathf.RoundToInt(ridge.From.x * 16f));
                Mix(Mathf.RoundToInt(ridge.To.y * 16f));
            }
            foreach (Vector2 node in Channel)
            {
                Mix(Mathf.RoundToInt(node.x * 16f));
                Mix(Mathf.RoundToInt(node.y * 16f));
            }
            foreach (MapDeposit deposit in Deposits)
            {
                Mix((int)deposit.Kind);
                Mix(deposit.Cell.x);
                Mix(deposit.Cell.y);
            }
            foreach (MapPoint point in Points)
            {
                Mix(point.Kind);
                Mix(point.Cell.x);
                Mix(point.Cell.y);
            }
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                Mix((int)Terrain[x, y]);
            Signature = hash;
        }

        // ---------- Validation: every rule the brief asks for, checked for every generated map ----------

        public int Count(DepositKind kind)
        {
            int count = 0;
            foreach (MapDeposit deposit in Deposits) if (deposit.Kind == kind) count++;
            return count;
        }

        public bool Validate(out string error)
        {
            int iron = Count(DepositKind.Iron);
            int ice = Count(DepositKind.Ice);
            if (iron < 6 || iron > 10) return Invalid($"hierro fuera de rango ({iron})", out error);
            if (ice < 4 || ice > 6) return Invalid($"hielo fuera de rango ({ice})", out error);
            if (Points.Count < 4 || Points.Count > 6) return Invalid($"puntos de interés fuera de rango ({Points.Count})", out error);

            List<Vector2Int> cells = new List<Vector2Int>();
            foreach (MapDeposit deposit in Deposits) cells.Add(deposit.Cell);
            foreach (MapPoint point in Points) cells.Add(point.Cell);
            for (int i = 0; i < cells.Count; i++)
            {
                if (!Inside(cells[i])) return Invalid("elemento fuera del mapa", out error);
                int dx = cells[i].x - BaseCell.x;
                int dy = cells[i].y - BaseCell.y;
                if (dx * dx + dy * dy < BaseClearRadius * BaseClearRadius)
                    return Invalid("elemento dentro de la zona de la base y sus barreras", out error);
                for (int j = i + 1; j < cells.Count; j++)
                {
                    int ex = cells[i].x - cells[j].x;
                    int ey = cells[i].y - cells[j].y;
                    if (ex * ex + ey * ey < PointSeparation * PointSeparation)
                        return Invalid("elementos superpuestos o demasiado juntos", out error);
                }
            }

            MapDeposit nearIron = Nearest(DepositKind.Iron);
            MapDeposit nearIce = Nearest(DepositKind.Ice);
            if (nearIron == null || nearIron.Distance > NearIronMaxDistance) return Invalid("sin hierro cercano", out error);
            if (nearIce == null || nearIce.Distance > NearIceMaxDistance) return Invalid("sin hielo cercano", out error);
            if (Quadrants(DepositKind.Iron) < 3) return Invalid("hierro concentrado en pocas regiones", out error);
            if (Quadrants(DepositKind.Ice) < 2) return Invalid("hielo concentrado en una región", out error);
            bool far = false;
            foreach (MapDeposit deposit in Deposits) far |= deposit.Distance >= FarDistance;
            if (!far) return Invalid("sin yacimientos lejanos", out error);

            bool big = false;
            foreach (MapCrater crater in Craters)
            {
                big |= crater.Radius >= 5f;
                float distance = Mathf.Sqrt((crater.X - BaseCell.x) * (crater.X - BaseCell.x) + (crater.Y - BaseCell.y) * (crater.Y - BaseCell.y));
                if (distance < crater.Radius * 1.45f + BaseClearRadius)
                    return Invalid("cráter sobre la base", out error);
            }
            if (!big || Craters.Count < 8) return Invalid("faltan cráteres", out error);
            if (BaseCell.x < 12 || BaseCell.x >= Width - 12 || BaseCell.y < 10 || BaseCell.y >= Height - 10)
                return Invalid("base demasiado cerca del borde", out error);
            error = "";
            return true;
        }

        private int Quadrants(DepositKind kind)
        {
            int mask = 0;
            foreach (MapDeposit deposit in Deposits)
            {
                if (deposit.Kind != kind) continue;
                int quadrant = (deposit.Cell.x >= BaseCell.x ? 1 : 0) + (deposit.Cell.y >= BaseCell.y ? 2 : 0);
                mask |= 1 << quadrant;
            }
            int count = 0;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
            return count;
        }

        private static bool Invalid(string reason, out string error)
        {
            error = reason;
            return false;
        }

        public string Describe()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            text.Append($"semilla {Seed} (intento {Attempt}) firma {Signature:X16} base {BaseCell.x},{BaseCell.y} cráteres {Craters.Count} ");
            foreach (MapDeposit deposit in Deposits)
                text.Append($"{deposit.Name}@{deposit.Cell.x},{deposit.Cell.y}(d{deposit.Distance}) ");
            foreach (MapPoint point in Points) text.Append($"P{point.Kind}@{point.Cell.x},{point.Cell.y} ");
            return text.ToString();
        }
    }
}

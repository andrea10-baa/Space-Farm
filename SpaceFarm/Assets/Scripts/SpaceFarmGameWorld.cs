using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SpaceFarm
{
    /// <summary>World objects: terrain sprite, deposits and their installations, barriers, solar array and astronauts.</summary>
    public sealed partial class SpaceFarmGame
    {
        private sealed class MineDrillVisual
        {
            public Transform Transform;
            public Vector3 RestPosition;
            public float Phase;
        }

        private sealed class UpgradeProgressVisual
        {
            public SpriteRenderer Track;
            public SpriteRenderer Fill;
            public GameObject Root;
        }

        private sealed class AstronautView
        {
            public Transform Root;
            public SpriteRenderer Body;
            public Vector3 BaseScale;
            public SpaceFarmRandom Random;
            public Vector2 TargetOffset;
            public float Wait;
            public float Speed;
            public float WalkCycle;
            public int FacingIndex = 4;
        public Vector2Int GoalCell;
        public List<Vector2Int> Path = new List<Vector2Int>();
        public int PathIndex;
        public bool PathDirty = true;
        public float RetryPathAt;
        }

        private sealed class DepositView
        {
            public MapDeposit Info;
            public SpriteRenderer Marker;
            public GameObject Building;
            public Vector3 BuildingScale;
            public TextMeshPro Label;
            public SpriteRenderer LabelBacking;
            public bool Seen;
            public int ShownLevel;
            public int ShownVisualLevel;
            public RectTransform MiniMarker;
        }

        private readonly Dictionary<string, UpgradeProgressVisual> upgradeProgressVisuals = new Dictionary<string, UpgradeProgressVisual>();
        private readonly HashSet<string> activeUpgradeProgressKeys = new HashSet<string>();
        private readonly Dictionary<string, Sprite> upgradeArtCache = new Dictionary<string, Sprite>();
        // The generated sheets have uneven gutters. These are the visible column bounds for
        // each atlas and row, measured in pixels from the left edge (end values are exclusive).
        private static readonly int[][][] UpgradeColumnBounds =
        {
            new[]
            {
                new[] { 49, 297, 349, 644, 675, 975, 1001, 1317, 1336, 1658 },
                new[] { 46, 269, 353, 613, 675, 961, 1001, 1306, 1335, 1656 },
                new[] { 31, 273, 341, 613, 660, 954, 980, 1299, 1320, 1663 }
            },
            new[]
            {
                new[] { 65, 257, 359, 588, 661, 927, 969, 1268, 1299, 1645 },
                new[] { 60, 254, 356, 572, 664, 909, 993, 1256, 1321, 1634 },
                new[] { 39, 270, 334, 597, 643, 927, 960, 1257, 1290, 1648 }
            },
            new[]
            {
                new[] { 43, 278, 346, 598, 652, 948, 988, 1302, 1328, 1666 },
                new[] { 17, 272, 308, 600, 634, 949, 971, 1291, 1311, 1666 },
                new[] { 25, 272, 331, 602, 654, 948, 989, 1293, 1331, 1666 }
            }
        };
        private static readonly int[][] UpgradeRowBounds =
        {
            new[] { 25, 265, 316, 567, 610, 880 },
            new[] { 32, 355, 381, 610, 631, 896 },
            new[] { 27, 300, 359, 584, 635, 884 }
        };
        private readonly List<AstronautView> astronauts = new List<AstronautView>();
        private readonly List<MineDrillVisual> mineDrills = new List<MineDrillVisual>();
        private readonly Vector2[] astronautStops =
        {
            new Vector2(-1.6f, -2.3f), new Vector2(-.7f, -3.1f),
            new Vector2(.2f, -2.4f), new Vector2(1.1f, -3.2f),
            new Vector2(2f, -2.5f), new Vector2(-1.8f, -3.8f),
            new Vector2(-.9f, -4.3f), new Vector2(0f, -3.9f),
            new Vector2(1f, -4.4f), new Vector2(2.1f, -3.9f)
        };
        private DepositView[] depositViews;
        private SpriteRenderer[] discoveryMarkers;
        private Color32[] terrainCellColors;
        private GameObject barrierPreview;
        private GameObject solarWorld;
        private GameObject dormWorld;
        private GameObject habitatWorld;
        private GameObject oxygenPlantWorld;
        private readonly List<GameObject> greenhouseWorlds = new List<GameObject>();
        private readonly List<GameObject> oxygenWorlds = new List<GameObject>();
        private readonly List<GameObject> warehouseWorlds = new List<GameObject>();
        private bool barrierMode;
        private bool collectionActive;
        private Vector2Int hoveredBarrierCell;
        private int observedPopulation;
        private int observedHabitatLevel;
        private Vector3 dormBaseScale;
        private int observedSolarLevel;
        private Vector3 solarBaseScale;
        private Sprite[] astronautFacingSprites;
        private Sprite[] astronautPassASprites;
        private Sprite[] astronautWalkingSprites;
        private Sprite[] astronautPassBSprites;
        private static readonly string[] AstronautDirectionNames =
        {
            "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"
        };

        private void CreateWorld()
        {
            worldRoot = new GameObject("Marte");
            worldRoot.transform.SetParent(transform);
            CreateTerrain();
            CreateTerrainRocks();
            CreateFog();
            CreateDepositViews();

            habitatWorld = CreateArtwork("Nave y hábitat", "Art/space-farm-world-modules#habitat-3d", CellPosition(BaseCell) + new Vector3(0, 0, -.2f),
                3.1f, 2.07f, 5, worldRoot.transform).gameObject;
            SetUpgradeArtwork(habitatWorld, 'b', 2, 1, 3.1f, 2.07f);
            oxygenPlantWorld = CreateArtwork("Planta de oxígeno", "Art/space-farm-world-modules#oxygen-3d",
                CellPosition(BaseCell + new Vector2Int(2, 0)) + new Vector3(0, 0, -.2f),
                1.7f, 1.14f, 6, worldRoot.transform).gameObject;
            SetUpgradeArtwork(oxygenPlantWorld, 'b', 1, 1, 1.7f, 1.14f);
            roverRenderer = CreateArtwork("Rover 1", "Art/space-farm-rover-directions#rover-facing-south",
                CellPosition(roverCell) + new Vector3(0f, 0f, -.5f),
                1.55f, 1.55f, 8, worldRoot.transform);
            rover = roverRenderer.gameObject;
            // Optional buildings are created only after a player confirms their chosen cell.
            CreateBarrierWorld();
            CreateSolarWorld();
            dormWorld = CreateArtwork("Dormitorio de astronautas", "Art/space-farm-world-modules#habitat-3d",
                CellPosition(BaseCell + new Vector2Int(-1, 2)) + new Vector3(0f, 0f, -.22f),
                1.65f, 1.1f, 6, worldRoot.transform).gameObject;
            SetUpgradeArtwork(dormWorld, 'c', 0, 1, 1.65f, 1.1f);
            dormBaseScale = dormWorld.transform.localScale;
            CreatePointsOfInterest();
            routeMarker = CreateWorldSprite("Destino del rover", ringSprite, CellPosition(roverCell),
                new Vector3(.65f, .65f, 1f), SpaceFarmPalette.Ice, 9, worldRoot.transform);
            routeMarker.enabled = false;
            scanPulse = CreateWorldSprite("Pulso de exploración", ringSprite, CellPosition(roverCell),
                new Vector3(.75f, .75f, 1f), SpaceFarmPalette.Ice, 10, worldRoot.transform);
            scanPulse.enabled = false;
            signalPing = CreateWorldSprite("Señal de descubrimiento", ringSprite, CellPosition(roverCell),
                new Vector3(.8f, .8f, 1f), SpaceFarmPalette.Ice, 11, worldRoot.transform);
            signalPing.enabled = false;
            CreateScanTrail();
        }

        /// <summary>The whole 128×80 region is one painted ground texture.</summary>
        private void CreateTerrain()
        {
            Sprite dustSurface = Resources.Load<Sprite>("Art/mars-dust-surface");
            SpaceFarmTerrainPainter.Result painted = SpaceFarmTerrainPainter.Paint(map);
            terrainCellColors = painted.CellColors;
            Texture2D texture = new Texture2D(painted.Width, painted.Height, TextureFormat.RGB24, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(painted.Pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, painted.Width, painted.Height), new Vector2(.5f, .5f),
                SpaceFarmTerrainPainter.PixelsPerCell / TileSize, 0, SpriteMeshType.FullRect);
            SpriteRenderer terrainRenderer = CreateWorldSprite("Terreno generado", sprite, Vector3.zero,
                new Vector3(1f, SpaceFarmMap.TerrainPerspectiveYScale, 1f), Color.white, -11, worldRoot.transform);

            Shader terrainShader = Resources.Load<Shader>("Shaders/SpaceFarmTerrain");
            if (dustSurface != null && terrainShader != null)
            {
                Material terrainMaterial = new Material(terrainShader);
                terrainMaterial.SetTexture("_DustTex", dustSurface.texture);
                terrainMaterial.SetVector("_TileCount", new Vector4(
                    Width / 24f, Height / 24f,
                    map.ValueNoise(map.Seed * .019f, map.Seed * .037f, 419),
                    map.ValueNoise(map.Seed * .031f, map.Seed * .017f, 421)));
                terrainMaterial.SetFloat("_DustStrength", .82f);
                terrainRenderer.sharedMaterial = terrainMaterial;
                runtimeAssets.Add(terrainMaterial);
            }

            runtimeAssets.AddRange(new UnityEngine.Object[] { sprite, texture });
        }

        /// <summary>Scatters shaded rock clusters over explored terrain so the ground reads as rocky Mars soil.</summary>
        private void CreateTerrainRocks()
        {
            Sprite[] variants = Resources.LoadAll<Sprite>("Art/mars-rock-clusters");
            if (variants == null || variants.Length == 0) return;

            SpaceFarmRandom random = new SpaceFarmRandom((ulong)(uint)map.Seed * 47UL + 193UL);
            int desiredCount = SpaceFarmMap.Width * SpaceFarmMap.Height / 13;
            int placed = 0;
            int attempts = desiredCount * 5;
            List<Vector3> vertices = new List<Vector3>(desiredCount * 4);
            List<Vector2> uvs = new List<Vector2>(desiredCount * 4);
            List<int> triangles = new List<int>(desiredCount * 6);
            for (int i = 0; i < attempts && placed < desiredCount; i++)
            {
                float x = random.Range(2f, SpaceFarmMap.Width - 3f);
                float y = random.Range(2f, SpaceFarmMap.Height - 3f);
                float dx = x - map.BaseCell.x;
                float dy = y - map.BaseCell.y;
                if (dx * dx + dy * dy < 18f || random.Value() < .16f - map.RockMask(x, y) * .10f) continue;

                Sprite sprite = variants[random.Range(0, variants.Length)];
                float width = random.Range(.82f, 1.47f);
                float depth = random.Range(.78f, .98f);
                Vector3 position = CellPosition(new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y)));
                position.x += random.Range(-.16f, .16f);
                position.y += random.Range(-.11f, .11f) * SpaceFarmMap.TerrainPerspectiveYScale;
                position.z = -.08f;
                float scale = width / sprite.bounds.size.x;
                Vector2[] spriteVertices = sprite.vertices;
                Vector2[] spriteUvs = sprite.uv;
                ushort[] spriteTriangles = sprite.triangles;
                int firstVertex = vertices.Count;
                for (int vertex = 0; vertex < spriteVertices.Length; vertex++)
                {
                    Vector2 local = spriteVertices[vertex];
                    vertices.Add(new Vector3(position.x + local.x * scale,
                        position.y + local.y * scale * depth, position.z));
                    uvs.Add(spriteUvs[vertex]);
                }
                for (int triangle = 0; triangle < spriteTriangles.Length; triangle++)
                    triangles.Add(firstVertex + spriteTriangles[triangle]);
                placed++;
            }

            if (vertices.Count == 0) return;
            GameObject root = new GameObject("Rocas y grava marciana");
            root.transform.SetParent(worldRoot.transform, false);
            Mesh mesh = new Mesh { name = "Rocas y grava combinadas", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Destroy(root);
                Destroy(mesh);
                return;
            }
            Material material = new Material(shader) { mainTexture = variants[0].texture };
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -1;
            runtimeAssets.Add(mesh);
            runtimeAssets.Add(material);
        }

        private void CreateDepositViews()
        {
            Transform root = new GameObject("Yacimientos").transform;
            root.SetParent(worldRoot.transform);
            depositViews = new DepositView[map.Deposits.Count];
            for (int i = 0; i < depositViews.Length; i++)
            {
                MapDeposit info = map.Deposits[i];
                bool iron = info.Kind == DepositKind.Iron;
                DepositView view = new DepositView { Info = info };
                view.Marker = CreateArtwork(iron ? $"Yacimiento {info.Name}" : $"Depósito {info.Name}",
                    iron ? "Art/iron-deposit" : "Art/ice-deposit",
                    CellPosition(info.Cell) + new Vector3(0, 0, -.1f), 1.55f, 1.03f, 3, root);
                view.Marker.enabled = false;
                Vector3 labelPosition = CellPosition(info.Cell) + new Vector3(0f, -.78f, -.4f);
                view.LabelBacking = CreateWorldSprite("Fondo de etiqueta", squareSprite,
                    labelPosition + new Vector3(0, 0, .03f), new Vector3(1.2f, .36f, 1f),
                    new Color(.07f, .1f, .14f, .86f), 11, root);
                GameObject label = new GameObject($"Etiqueta {info.Name}", typeof(TextMeshPro));
                label.transform.SetParent(root);
                label.transform.position = labelPosition;
                label.transform.localScale = Vector3.one * .3f;
                view.Label = label.GetComponent<TextMeshPro>();
                view.Label.font = TMP_Settings.defaultFontAsset;
                view.Label.fontSize = 4f;
                view.Label.fontStyle = FontStyles.Bold;
                view.Label.alignment = TextAlignmentOptions.Center;
                view.Label.textWrappingMode = TextWrappingModes.NoWrap;
                view.Label.color = iron ? SpaceFarmPalette.Iron : SpaceFarmPalette.Ice;
                view.Label.sortingOrder = 12;
                label.SetActive(false);
                view.LabelBacking.gameObject.SetActive(false);
                depositViews[i] = view;
            }
        }

        private void CreateSolarWorld()
        {
            solarWorld = CreateArtwork("Matriz solar", "Art/space-farm-world-modules#solar-3d",
                CellPosition(BaseCell) + new Vector3(-2.1f,-.8f,-.25f), 2.1f, 1.4f, 6, worldRoot.transform).gameObject;
            SetUpgradeArtwork(solarWorld, 'b', 0, 1, 2.1f, 1.4f);
            solarBaseScale = solarWorld.transform.localScale;
        }

        private Sprite GetUpgradeSprite(char atlas, int row, int level)
        {
            string key = $"{atlas}:{row}:{Mathf.Clamp(level, 1, 5)}";
            if (upgradeArtCache.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>($"Art/space-farm-upgrade-atlas-{atlas}");
            if (texture == null) return null;

            int atlasIndex = atlas - 'a';
            if (atlasIndex < 0 || atlasIndex >= UpgradeColumnBounds.Length || row < 0 || row >= 3) return null;
            int column = Mathf.Clamp(level, 1, 5) - 1;
            int[] columns = UpgradeColumnBounds[atlasIndex][row];
            int[] rows = UpgradeRowBounds[atlasIndex];
            const int padding = 3;
            int left = Mathf.Max(0, columns[column * 2] - padding);
            int right = Mathf.Min(texture.width, columns[column * 2 + 1] + padding);
            int top = Mathf.Max(0, rows[row * 2] - padding);
            int bottom = Mathf.Min(texture.height, rows[row * 2 + 1] + padding);
            Rect rect = new Rect(left, texture.height - bottom, right - left, bottom - top);
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = $"space-farm-tier-{atlas}-{row + 1}-{level}";
            upgradeArtCache[key] = sprite;
            runtimeAssets.Add(sprite);
            return sprite;
        }

        private bool SetUpgradeArtwork(GameObject building, char atlas, int row, int level, float width, float height)
        {
            if (building == null) return false;
            Sprite sprite = GetUpgradeSprite(atlas, row, level);
            SpriteRenderer renderer = building.GetComponent<SpriteRenderer>();
            if (sprite == null || renderer == null) return false;
            renderer.sprite = sprite;
            building.transform.localScale = UpgradeSpriteScale(sprite, width, height);
            return true;
        }

        private static Vector3 UpgradeSpriteScale(Sprite sprite, float width, float height)
        {
            // Preserve the previous artwork's on-map size after trimming transparent gutters.
            float referenceWidth = (sprite.texture.width / 5f - 10f) / 100f;
            float referenceHeight = (sprite.texture.height / 3f - 10f) / 100f;
            return new Vector3(width / referenceWidth,
                height / referenceHeight, 1f);
        }

        private static Color DiscoveryColor(int kind)
        {
            switch (kind)
            {
                case 0: return SpaceFarmPalette.Iron;
                case 1: return SpaceFarmPalette.Ice;
                case 2: return SpaceFarmPalette.Oxygen;
                default: return SpaceFarmPalette.Greenhouse;
            }
        }

        private void CreatePointsOfInterest()
        {
            discoveryMarkers = new SpriteRenderer[map.Points.Count];
            for (int i = 0; i < discoveryMarkers.Length; i++)
            {
                discoveryMarkers[i] = CreateWorldSprite("Punto de interés", ringSprite,
                    CellPosition(map.Points[i].Cell) + new Vector3(0,0,-.12f),
                    new Vector3(.7f,.7f,1f), DiscoveryColor(map.Points[i].Kind), 4, worldRoot.transform);
                discoveryMarkers[i].enabled = false;
            }
        }

        private bool TryHandleWorldClick(Vector3 world)
        {
            if (barrierMode)
            {
                Vector2Int cell = WorldToCell(world);
                if (!map.Inside(cell))
                    SetReport("Elegí un sector dentro del mapa.");
                else if (IsActorOccupyingCell(cell))
                    SetReport("No coloqués el muro debajo del rover.");
                else
                {
                    bool built = state.BuildBarrierSegment(cell, false, revealed[cell.x, cell.y], out string message);
                    SetReport(message);
                    if (built)
                    {
                        PlayWorldPing(CellPosition(cell), SpaceFarmPalette.Iron);
                        RefreshWorld();
                        RefreshHud();
                    }
                }
                RefreshSystems();
                return true;
            }
            if (depositViews == null) return false;
            foreach (DepositView view in depositViews)
            {
                if (!revealed[view.Info.Cell.x, view.Info.Cell.y]) continue;
                if (Vector2.Distance(world, CellPosition(view.Info.Cell)) >= .8f) continue;
                SpaceFarmState.DepositState deposit = state.Deposit(view.Info.Id);
                if (deposit.Surveyed) return SelectDeposit(view.Info.Id);
                // A visible but unstudied signal: the click is the order to go and study it.
                HideWorldSelection();
                SendRoverTo(view.Info.Cell);
                return true;
            }
            Vector3 basePosition = CellPosition(BaseCell);
            for (int i = 0; i < greenhouseWorlds.Count; i++)
                if (Vector2.Distance(world, greenhouseWorlds[i].transform.position) < .85f)
                    return SelectWorldBuilding("greenhouse", i);
            for (int i = 0; i < oxygenWorlds.Count; i++)
                if (Vector2.Distance(world, oxygenWorlds[i].transform.position) < .8f)
                    return SelectWorldBuilding("oxygen2", i);
            for (int i = 0; i < warehouseWorlds.Count; i++)
                if (Vector2.Distance(world, warehouseWorlds[i].transform.position) < 1f)
                    return SelectWorldBuilding("warehouse", i);
            if (Vector2.Distance(world, CellPosition(BaseCell + new Vector2Int(2,0))) < .78f)
                return SelectWorldBuilding("oxygen1");
            if (Vector2.Distance(world, solarWorld.transform.position + new Vector3(.33f,0,0)) < .9f)
                return SelectWorldBuilding("solar");
            if (Vector2.Distance(world, dormWorld.transform.position) < .7f) return SelectWorldBuilding("dormitory");
            if (Vector2.Distance(world, basePosition) < 1.1f) return SelectWorldBuilding("habitat");
            HideWorldSelection();
            return false;
        }

        private void UpdateSystems(Mouse mouse, Keyboard keyboard)
        {
            if (keyboard != null)
            {
                Vector2 direction = Vector2.zero;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x--;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x++;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.y--;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.y++;
                if (direction.sqrMagnitude > 0f)
                {
                    if (followRover)
                    {
                        followRover = false;
                        RefreshFollowButton();
                    }
                    // Speed follows the zoom so crossing the big map never feels slow or twitchy.
                    float speed = gameCamera.orthographicSize * 1.7f;
                    gameCamera.transform.position += (Vector3)(direction.normalized * (speed * Time.deltaTime));
                    ClampCamera();
                }
            }
            if (barrierMode && mouse != null && map != null && state != null && barrierPreview != null)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                Vector3 world = ScreenToWorld(mouse.position.ReadValue());
                hoveredBarrierCell = WorldToCell(world);
                bool inside = map.Inside(hoveredBarrierCell);
                bool actorCollision = inside && IsActorOccupyingCell(hoveredBarrierCell);
                string reason = overUi ? "Apuntá al mapa" : !inside ? "Fuera del mapa" : actorCollision ? "No colocar bajo el rover" :
                    state.BarrierBlocker(hoveredBarrierCell, revealed[hoveredBarrierCell.x, hoveredBarrierCell.y]);
                bool valid = reason.Length == 0;
                if (buildModeText != null)
                    buildModeText.text = $"Muro · 1 Fe (tenés {state.Iron})\n" +
                        (valid ? "Se une solo a muros vecinos · clic para colocar" : reason);
                UpdateWallPreview(hoveredBarrierCell, !overUi && inside, valid);
            }
            else HideWallPreview();
            AnimateAstronauts();
        }

        private bool IsActorOccupyingCell(Vector2Int cell)
        {
            return rover != null && WorldToCell(rover.transform.position) == cell;
        }

        private bool IsAstronautPositionBlocked(Vector3 position, AstronautView moving)
        {
            Vector2Int cell = WorldToCell(position);
            if (!map.Inside(cell) || state.HasBarrierAt(cell) || IsAstronautInsideStructure(position)) return true;
            if (rover != null && Vector2.Distance(position, rover.transform.position) < .72f) return true;
            for (int i = 0; i < astronauts.Count; i++)
            {
                AstronautView other = astronauts[i];
                if (other != moving && other.Root != null && Vector2.Distance(position, other.Root.position) < .42f) return true;
            }
            return false;
        }

        /// <summary>Syncs every world object with the rules. Called on state or fog changes, never per frame.</summary>
        private void RefreshWorld()
        {
            if (state == null || worldRoot == null) return;
            foreach (DepositView view in depositViews) RefreshDeposit(view);

            if (state.RoverLevel != previousRoverLevel)
            {
                roverBaseScale *= 1.12f;
                PlayWorldPing(rover.transform.position, SpaceFarmPalette.Ice);
            }
            while (greenhouseWorlds.Count < state.GreenhouseCells.Count)
            {
                int index = greenhouseWorlds.Count;
                GameObject built = CreateBuilding($"Invernadero {index + 1}", "Art/space-farm-world-modules#greenhouse-3d",
                    state.GreenhouseCells[index], worldRoot.transform);
                SetUpgradeArtwork(built, 'a', 0, state.ColonyLevel, 1.75f, 1.17f);
                greenhouseWorlds.Add(built);
                if (greenhouse == null) greenhouse = built;
                AnimatePop(built.transform, built.transform.localScale);
            }
            while (oxygenWorlds.Count < state.OxygenCells.Count)
            {
                int index = oxygenWorlds.Count;
                GameObject built = CreateBuilding($"Planta de oxígeno {index + 2}", "Art/space-farm-world-modules#oxygen-3d",
                    state.OxygenCells[index], worldRoot.transform);
                SetUpgradeArtwork(built, 'b', 1, state.ColonyLevel, 1.75f, 1.17f);
                oxygenWorlds.Add(built);
                if (oxygenPlantTwo == null) oxygenPlantTwo = built;
                AnimatePop(built.transform, built.transform.localScale);
            }
            while (startingBaseLanded && warehouseWorlds.Count < state.WarehouseCells.Count)
            {
                int index = warehouseWorlds.Count;
                GameObject built = CreateArtwork(index == 0 ? "Almacén central" : $"Almacén de recursos {index + 1}",
                    "Art/space-farm-warehouse", CellPosition(state.WarehouseCells[index]) + new Vector3(0f, 0f, -.18f),
                    2.05f, 1.42f, 6, worldRoot.transform).gameObject;
                SetUpgradeArtwork(built, 'c', 1, state.ColonyLevel, 2.05f, 1.42f);
                warehouseWorlds.Add(built);
                AnimatePop(built.transform, built.transform.localScale);
            }
            SetUpgradeArtwork(oxygenPlantWorld, 'b', 1, state.ColonyLevel, 1.7f, 1.14f);
            if (state.ColonyLevel != observedHabitatLevel)
            {
                observedHabitatLevel = state.ColonyLevel;
                Vector3 size = dormBaseScale * (1f + .08f * (state.ColonyLevel - 1));
                SetUpgradeArtwork(habitatWorld, 'b', 2, state.ColonyLevel, 3.1f, 2.07f);
                SetUpgradeArtwork(dormWorld, 'c', 0, state.ColonyLevel, 1.65f, 1.1f);
                dormWorld.transform.localScale = size;
                if (state.ColonyLevel > 1) AnimatePop(dormWorld.transform, size);
                for (int i = 0; i < greenhouseWorlds.Count; i++)
                    SetUpgradeArtwork(greenhouseWorlds[i], 'a', 0, state.ColonyLevel, 1.75f, 1.17f);
                for (int i = 0; i < oxygenWorlds.Count; i++)
                    SetUpgradeArtwork(oxygenWorlds[i], 'b', 1, state.ColonyLevel, 1.75f, 1.17f);
                for (int i = 0; i < warehouseWorlds.Count; i++)
                    SetUpgradeArtwork(warehouseWorlds[i], 'c', 1, state.ColonyLevel, 2.05f, 1.42f);
                SetUpgradeArtwork(solarWorld, 'b', 0, state.SolarLevel, 2.1f, 1.4f);
                solarWorld.transform.localScale = solarBaseScale * (1f + .12f * (state.SolarLevel - 1));
            }
            if (state.SolarLevel != observedSolarLevel)
            {
                observedSolarLevel = state.SolarLevel;
                Vector3 size = solarBaseScale * (1f + .12f * (state.SolarLevel - 1));
                SetUpgradeArtwork(solarWorld, 'b', 0, state.SolarLevel, 2.1f, 1.4f);
                solarWorld.transform.localScale = size;
                if (state.SolarLevel > 1) AnimatePop(solarWorld.transform, size);
            }
            previousGreenhouse = state.Greenhouse;
            previousRoverLevel = state.RoverLevel;
            previousOxygenPlants = state.OxygenPlants;
            RefreshDamageVisuals();
            RefreshSystems();
        }

        private void RefreshDamageVisuals()
        {
            TintDamage(solarWorld, state.IsDamaged(DamagedStructureKind.Solar));
            TintDamage(dormWorld, state.IsDamaged(DamagedStructureKind.Dormitory));
            TintDamage(rover, state.IsDamaged(DamagedStructureKind.Rover));
            for (int i = 0; i < greenhouseWorlds.Count; i++)
                TintDamage(greenhouseWorlds[i], state.IsDamaged(DamagedStructureKind.Greenhouse, i));
            for (int i = 0; i < depositViews.Length; i++)
                if (depositViews[i].Building != null)
                    TintDamage(depositViews[i].Building, state.IsDamaged(DamagedStructureKind.Mine, i));
        }

        private static void TintDamage(GameObject target, bool damaged)
        {
            if (target == null) return;
            Color tint = damaged ? new Color(1f, .42f, .31f, 1f) : Color.white;
            foreach (SpriteRenderer sprite in target.GetComponentsInChildren<SpriteRenderer>()) sprite.color = tint;
        }

        private void RefreshDeposit(DepositView view)
        {
            SpaceFarmState.DepositState deposit = state.Deposit(view.Info.Id);
            bool visible = revealed[view.Info.Cell.x, view.Info.Cell.y];
            bool iron = deposit.Kind == DepositKind.Iron;
            if (deposit.HasBuilding && view.Building == null)
            {
                view.Building = CreateBuilding(iron ? $"Mina {deposit.Name}" : $"Extractor {deposit.Name}",
                    iron ? "Art/space-farm-iron-mine-animation#iron-mine-base-3d" : "Art/space-farm-world-modules#water-3d",
                    view.Info.Cell, view.Marker.transform.parent);
                int visualLevel = iron ? Mathf.Max(deposit.MineLevel, state.ColonyLevel) : state.ColonyLevel;
                SetUpgradeArtwork(view.Building, 'a', iron ? 1 : 2, visualLevel, 1.75f, 1.17f);
                if (iron) AddMineDrill(view.Building);
                view.BuildingScale = view.Building.transform.localScale;
                AnimatePop(view.Building.transform, view.BuildingScale);
                view.ShownLevel = deposit.MineLevel;
                view.ShownVisualLevel = visualLevel;
            }
            view.Marker.enabled = visible && !deposit.HasBuilding;
            view.Marker.color = deposit.Surveyed ? Color.white : new Color(1f, 1f, 1f, .8f);
            if (view.Building != null)
            {
                int visualLevel = iron ? Mathf.Max(deposit.MineLevel, state.ColonyLevel) : state.ColonyLevel;
                bool mineImproved = iron && deposit.MineLevel != view.ShownLevel;
                if (visualLevel != view.ShownVisualLevel || mineImproved)
                {
                    SetUpgradeArtwork(view.Building, 'a', iron ? 1 : 2, visualLevel, 1.75f, 1.17f);
                    Vector3 scale = view.BuildingScale * (iron && deposit.MineLevel >= 2 ? 1.16f : 1f);
                    AnimatePop(view.Building.transform, scale);
                }
                view.ShownVisualLevel = visualLevel;
                if (iron) view.ShownLevel = deposit.MineLevel;
            }

            string text;
            if (!deposit.Surveyed) text = $"SEÑAL {deposit.Name}";
            else if (deposit.MineLevel > 0 && deposit.MineStored > 0)
                text = deposit.MineStored == deposit.MineCapacity
                    ? $"{deposit.Name} · LLENO {deposit.MineStored}/{deposit.MineCapacity} Fe"
                    : $"{deposit.Name} · RECOGER {deposit.MineStored} Fe";
            else text = deposit.Name;
            view.Label.gameObject.SetActive(visible);
            view.LabelBacking.gameObject.SetActive(visible);
            if (!visible || view.Label.text == text) return;
            view.Label.text = text;
            view.LabelBacking.transform.localScale = new Vector3(.3f + text.Length * .125f, .36f, 1f);
        }

        private void RefreshSystems()
        {
            if (state == null || worldRoot == null) return;
            RefreshWallNetwork();
            for (int i = 0; i < discoveryMarkers.Length; i++)
                if (discoveryMarkers[i] != null)
                    discoveryMarkers[i].enabled = revealed[map.Points[i].Cell.x, map.Points[i].Cell.y] && !state.HasDiscovery(i);
            SyncAstronauts();
            RefreshSystemsHud();
            MarkMinimapDirty();
        }

        private void RefreshUpgradeProgressVisuals()
        {
            if (state.Upgrades.Count == 0 && upgradeProgressVisuals.Count == 0) return;
            activeUpgradeProgressKeys.Clear();
            foreach (SpaceFarmState.UpgradeTask task in state.Upgrades)
            {
                GameObject target = UpgradeTarget(task);
                if (target == null) continue;
                string key = $"{task.Kind}:{task.Target}";
                activeUpgradeProgressKeys.Add(key);
                if (!upgradeProgressVisuals.TryGetValue(key, out UpgradeProgressVisual visual) || visual.Root == null)
                {
                    visual = CreateUpgradeProgressVisual(key);
                    if (visual == null) continue;
                    upgradeProgressVisuals[key] = visual;
                }

                SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
                if (targetRenderer == null) continue;
                Bounds bounds = targetRenderer.bounds;
                Vector3 position = new Vector3(bounds.center.x, bounds.max.y + .16f, -.42f);
                visual.Track.transform.position = position;
                float progress = task.Duration <= 0f ? 1f : 1f - Mathf.Clamp01(task.Remaining / task.Duration);
                float fullWidth = .82f;
                float fillWidth = Mathf.Max(.025f, fullWidth * progress);
                visual.Fill.transform.position = position + new Vector3(-fullWidth * .5f + fillWidth * .5f, 0f, -.01f);
                visual.Fill.transform.localScale = new Vector3(fillWidth, .055f, 1f);
            }

            List<string> removed = null;
            foreach (KeyValuePair<string, UpgradeProgressVisual> entry in upgradeProgressVisuals)
            {
                if (activeUpgradeProgressKeys.Contains(entry.Key)) continue;
                if (entry.Value.Root != null) Destroy(entry.Value.Root);
                if (removed == null) removed = new List<string>();
                removed.Add(entry.Key);
            }
            if (removed != null)
                for (int i = 0; i < removed.Count; i++) upgradeProgressVisuals.Remove(removed[i]);
        }

        private GameObject UpgradeTarget(SpaceFarmState.UpgradeTask task)
        {
            if (task.Kind == "solar") return solarWorld;
            if (task.Kind == "rover") return rover;
            if (task.Kind == "mine" && depositViews != null && task.Target >= 0 && task.Target < depositViews.Length)
                return depositViews[task.Target].Building;
            return null;
        }

        private UpgradeProgressVisual CreateUpgradeProgressVisual(string key)
        {
            if (worldRoot == null || squareSprite == null) return null;
            GameObject root = new GameObject($"Progreso de mejora {key}");
            root.transform.SetParent(worldRoot.transform, false);
            SpriteRenderer track = CreateWorldSprite("Fondo de progreso", squareSprite, Vector3.zero,
                new Vector3(.88f, .105f, 1f), new Color(.055f, .09f, .12f, .9f), 29, root.transform);
            SpriteRenderer fill = CreateWorldSprite("Carga de mejora", squareSprite, Vector3.zero,
                new Vector3(.025f, .055f, 1f), new Color(.96f, .62f, .29f, 1f), 30, root.transform);
            return new UpgradeProgressVisual { Root = root, Track = track, Fill = fill };
        }

        private void StartMineCollection(int depositId)
        {
            if (paused) return;
            if (state.IsDamaged(DamagedStructureKind.Rover))
            {
                SetReport($"El rover está averiado. Reparalo por {state.RepairIronCost} Fe y {state.RepairEnergyCost} energía.");
                return;
            }
            SpaceFarmState.DepositState deposit = state.Deposit(depositId);
            if (deposit == null || deposit.MineLevel == 0) { SetReport("Elegí una mina para recoger su hierro."); return; }
            if (!state.CanRoverReach(map.Deposits[depositId].Cell)) { SetReport("La mina queda fuera del alcance de esta colonia. Subí de nivel."); return; }
            if (state.GameOver) { SetReport("La misión terminó. Abrí PAUSA (Esc) para reiniciar."); return; }
            if (travel != null || collectionActive) { SetReport("Esperá a que el rover termine su recorrido."); return; }
            if (deposit.MineStored == 0) { SetReport($"La mina de {deposit.Name} todavía no tiene hierro almacenado."); return; }
            collectionActive = true;
            StartCoroutine(MineCollectionRoutine(depositId));
            RefreshHud();
        }

        private IEnumerator MineCollectionRoutine(int depositId)
        {
            if (!FindReachableCellNear(map.Deposits[depositId].Cell, out Vector2Int mineCell))
            {
                collectionActive = false;
                SetReport("No hay un camino libre hasta la mina. Retirá un muro que bloquee el acceso.");
                RefreshHud();
                yield break;
            }
            roverDestination = mineCell;
            routeMarker.transform.position = CellPosition(mineCell);
            routeMarker.enabled = true;
            SetReport($"Rover en camino a la mina de {map.Deposits[depositId].Name}.");
            travel = StartCoroutine(TravelTo());
            yield return travel;
            if (roverCell != mineCell)
            {
                collectionActive = false;
                RefreshHud();
                yield break;
            }
            state.LoadMineToRover(depositId, out string loaded);
            SetReport(loaded);
            RefreshWorld();
            RefreshHud();
            if (!FindNearestWarehouseDockCell(out Vector2Int dockCell))
            {
                collectionActive = false;
                SetReport("El rover quedó cargado porque no hay un camino libre hasta un almacén. Abrí un paso entre los muros.");
                RefreshHud();
                yield break;
            }
            roverDestination = dockCell;
            routeMarker.transform.position = CellPosition(dockCell);
            routeMarker.enabled = true;
            travel = StartCoroutine(TravelTo());
            yield return travel;
            if (roverCell != dockCell)
            {
                collectionActive = false;
                SetReport("El rover sigue cargado. Despejá el camino hasta la base para continuar.");
                RefreshHud();
                yield break;
            }
            // Cargo moves in two atomic steps (mine to rover, rover to habitat), so a pause in between cannot lose or duplicate iron.
            bool stored = state.UnloadRover(out string unloaded);
            collectionActive = false;
            SetReport(unloaded);
            if (stored) PlayWorldPing(CellPosition(dockCell), SpaceFarmPalette.Iron);
            RefreshWorld();
            RefreshHud();
        }

        private void OrderCargoDelivery()
        {
            if (paused || state.RoverCargo <= 0) return;
            if (state.IsDamaged(DamagedStructureKind.Rover))
            {
                SetReport("El rover está averiado. Reparalo antes de enviarlo al almacén.");
                return;
            }
            if (travel != null || collectionActive) { SetReport("Esperá a que el rover termine su recorrido."); return; }
            if (state.Iron >= state.WarehouseCapacity) { SetReport("El almacén está lleno. Construí otro antes de descargar."); return; }
            if (!FindNearestWarehouseDockCell(out Vector2Int dockCell))
            {
                SetReport("No hay una ruta libre hasta el almacén. Despejá un paso entre los muros.");
                return;
            }
            collectionActive = true;
            StartCoroutine(CargoDeliveryRoutine(dockCell));
            RefreshHud();
        }

        private IEnumerator CargoDeliveryRoutine(Vector2Int dockCell)
        {
            roverDestination = dockCell;
            routeMarker.transform.position = CellPosition(dockCell);
            routeMarker.enabled = true;
            SetReport("Rover en camino al almacén con los materiales recuperados.");
            travel = StartCoroutine(TravelTo());
            yield return travel;
            bool arrived = roverCell == dockCell;
            string report = string.Empty;
            bool stored = arrived && state.UnloadRover(out report);
            collectionActive = false;
            if (arrived) SetReport(report);
            else SetReport("El rover no alcanzó el almacén. Revisá si hay un muro bloqueando el paso.");
            if (stored) PlayWorldPing(CellPosition(dockCell), SpaceFarmPalette.Iron);
            RefreshWorld();
            RefreshHud();
        }

        private void SyncAstronauts()
        {
            if (state.Population == observedPopulation && astronauts.Count == state.Population) return;
            LoadAstronautSprites();
            while (astronauts.Count < state.Population)
            {
                int index = astronauts.Count;
                SpaceFarmRandom random = new SpaceFarmRandom((ulong)(uint)map.Seed * 131UL + (ulong)(index + 1) * 7919UL);
                Vector2 startOffset = astronautStops[index % astronautStops.Length] +
                    new Vector2(random.Range(-.12f, .12f), random.Range(-.1f, .1f));
                for (int attempt = 0; attempt < 24 && IsAstronautPositionBlocked(
                    CellPosition(BaseCell) + (Vector3)ProjectGroundOffset(startOffset), null); attempt++)
                    startOffset = new Vector2(random.Range(-1.9f, 2.5f), random.Range(-4.4f, -2.1f));
                GameObject root = new GameObject($"Astronauta {index + 1}");
                root.transform.SetParent(worldRoot.transform);
                root.transform.position = CellPosition(BaseCell) +
                    (Vector3)ProjectGroundOffset(startOffset);
                Sprite sprite = astronautPassASprites[4] != null ? astronautPassASprites[4] : astronautFacingSprites[4];
                if (sprite == null) sprite = circleSprite;
                float scale = .86f / sprite.bounds.size.y;
                SpriteRenderer body = CreateWorldSprite("Traje EVA", sprite, root.transform.position,
                    Vector3.one * scale, Color.white, 9, root.transform);
                float wait = random.Range(.25f, 1.5f);
                float speed = random.Range(.42f, .72f);
                AstronautView view = new AstronautView
                {
                    Root = root.transform,
                    Body = body,
                    BaseScale = body.transform.localScale,
                    Random = random,
                    Wait = wait,
                    Speed = speed,
                    WalkCycle = random.Range(0f, 1f)
                };
                astronauts.Add(view);
                ChooseAstronautTarget(view, startOffset);
            }
            while (astronauts.Count > state.Population)
            {
                int last = astronauts.Count - 1;
                Destroy(astronauts[last].Root.gameObject);
                astronauts.RemoveAt(last);
            }
            observedPopulation = state.Population;
        }

        private void AnimateAstronauts()
        {
            if (state == null) return;
            for (int i = 0; i < astronauts.Count; i++)
            {
                AstronautView view = astronauts[i];
                // The sprite atlas includes its own ground shadow: bobbing the entire image makes the feet jump.
                view.Body.transform.localPosition = Vector3.zero;
                view.Body.transform.localScale = view.BaseScale;
                if (state.GameOver)
                {
                    SetAstronautIdle(view);
                    continue;
                }
                SpaceFarmState.CrewMember member = state.Crew[i];
                if (member.Resting || member.Job != CrewJob.None)
                {
                    Vector3 destination = member.Resting
                        ? AstronautRestPosition(i)
                        : CrewWorkPosition(member);
                    bool arrived = MoveAstronautAlongClearPath(view, destination, view.Speed * 1.6f,
                        approachDestination: member.Job != CrewJob.None);
                    view.Body.enabled = !(member.Resting && arrived);
                    if (arrived) SetAstronautIdle(view);
                    continue;
                }
                view.Body.enabled = true;
                if (view.Wait > 0f)
                {
                    view.Wait -= Time.deltaTime;
                    SetAstronautIdle(view);
                    continue;
                }
                Vector3 target = CellPosition(BaseCell) +
                    (Vector3)ProjectGroundOffset(view.TargetOffset);
                float speed = view.Speed * (state.LastSol.Storm || state.LastSol.RaidFoodLoss > 0 ? .55f : 1f);
                if (MoveAstronautAlongClearPath(view, target, speed))
                {
                    view.Wait = view.Random.Range(.4f, 1.75f);
                    view.Speed = view.Random.Range(.42f, .72f);
                    SetAstronautIdle(view);
                    ChooseAstronautTarget(view, view.TargetOffset);
                }
            }
        }

        private Vector3 AstronautRestPosition(int index)
        {
            Vector2Int[] slots =
            {
                new Vector2Int(-2, 2), new Vector2Int(0, 2), new Vector2Int(2, 2),
                new Vector2Int(-2, 0), new Vector2Int(2, 0), new Vector2Int(-2, -2),
                new Vector2Int(0, -2), new Vector2Int(2, -2), new Vector2Int(-3, 1), new Vector2Int(3, 1)
            };
            Vector2Int slot = slots[index % slots.Length];
            return dormWorld.transform.position + new Vector3(slot.x * TileSize,
                slot.y * TileSize * SpaceFarmMap.TerrainPerspectiveYScale, 0f);
        }

        private bool MoveAstronautAlongClearPath(AstronautView view, Vector3 destination, float speed,
            bool approachDestination = false)
        {
            Vector2Int requestedGoal = WorldToCell(destination);
            Vector2Int goal = requestedGoal;
            if (!map.Inside(goal)) return false;
            if (IsAstronautCellBlocked(goal, view))
            {
                if (!FindNearestAstronautCell(destination, view, out goal))
                {
                    view.PathDirty = true;
                    view.RetryPathAt = Time.time + .35f;
                    SetAstronautIdle(view);
                    return false;
                }
            }

            if (view.PathDirty || view.GoalCell != goal ||
                (view.PathIndex < view.Path.Count && IsAstronautCellBlocked(view.Path[view.PathIndex], view)))
            {
                if (Time.time < view.RetryPathAt) return false;
                view.GoalCell = goal;
                view.PathDirty = false;
                view.PathIndex = 0;
                if (!FindAstronautPath(WorldToCell(view.Root.position), goal, view, out view.Path))
                {
                    view.PathDirty = true;
                    view.RetryPathAt = Time.time + .35f;
                    return false;
                }
            }

            Vector3 target = view.PathIndex < view.Path.Count
                ? CellPosition(view.Path[view.PathIndex])
                : goal == requestedGoal ? destination : approachDestination
                    ? WorkApproachPosition(destination, goal, view) : CellPosition(goal);
            Vector3 direction = target - view.Root.position;
            Vector3 previous = view.Root.position;
            Vector3 next = Vector3.MoveTowards(previous, target, speed * Time.deltaTime);
            if (IsAstronautPositionBlocked(next, view))
            {
                view.PathDirty = true;
                view.RetryPathAt = Time.time + .18f;
                SetAstronautIdle(view);
                return false;
            }
            view.Root.position = next;
            float traveled = Vector3.Distance(previous, next);
            if (traveled > .0001f)
            {
                view.WalkCycle = Mathf.Repeat(view.WalkCycle + traveled / .48f, 1f);
                SetAstronautFacing(view, direction, Mathf.FloorToInt(view.WalkCycle * 4f));
            }
            if (Vector3.Distance(next, target) > .025f) return false;
            if (view.PathIndex < view.Path.Count)
            {
                view.PathIndex++;
                return false;
            }
            return goal == requestedGoal && Vector3.Distance(next, destination) <= .12f ||
                   goal != requestedGoal && (!approachDestination || Vector3.Distance(next, target) <= .04f);
        }

        private Vector3 WorkApproachPosition(Vector3 destination, Vector2Int safeCell, AstronautView moving)
        {
            Vector3 center = CellPosition(safeCell);
            Vector3 towardBuilding = destination - center;
            towardBuilding.z = 0f;
            if (towardBuilding.sqrMagnitude < .0001f) return center;
            towardBuilding.Normalize();

            // Stop close to the sprite edge while keeping the astronaut's feet in a clear cell.
            float clearDistance = 0f;
            float blockedDistance = .32f;
            for (int attempt = 0; attempt < 7; attempt++)
            {
                float distance = (clearDistance + blockedDistance) * .5f;
                Vector3 candidate = center + towardBuilding * distance;
                if (IsAstronautPositionBlocked(candidate, moving)) blockedDistance = distance;
                else clearDistance = distance;
            }
            return center + towardBuilding * clearDistance;
        }

        private bool FindNearestAstronautCell(Vector3 destination, AstronautView moving, out Vector2Int result)
        {
            result = default;
            Vector2Int center = WorldToCell(destination);
            float best = float.MaxValue;
            bool found = false;
            for (int radius = 1; radius <= 4; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                Vector2Int cell = center + new Vector2Int(x, y);
                if (IsAstronautCellBlocked(cell, moving)) continue;
                float distance = (CellPosition(cell) - destination).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                result = cell;
                found = true;
            }
            return found;
        }

        private bool IsAstronautCellBlocked(Vector2Int cell, AstronautView moving)
        {
            return !map.Inside(cell) || IsAstronautPositionBlocked(CellPosition(cell), moving);
        }

        private bool IsAstronautInsideStructure(Vector3 position)
        {
            if (InsideBuilding(habitatWorld, position) || InsideBuilding(oxygenPlantWorld, position) ||
                InsideBuilding(solarWorld, position) || InsideBuilding(dormWorld, position)) return true;
            for (int i = 0; i < greenhouseWorlds.Count; i++)
                if (InsideBuilding(greenhouseWorlds[i], position)) return true;
            for (int i = 0; i < oxygenWorlds.Count; i++)
                if (InsideBuilding(oxygenWorlds[i], position)) return true;
            for (int i = 0; i < warehouseWorlds.Count; i++)
                if (InsideBuilding(warehouseWorlds[i], position)) return true;
            if (depositViews != null)
                for (int i = 0; i < depositViews.Length; i++)
                    if (InsideBuilding(depositViews[i].Building, position)) return true;
            return false;
        }

        private static bool InsideBuilding(GameObject building, Vector3 position)
        {
            if (building == null) return false;
            SpriteRenderer sprite = building.GetComponent<SpriteRenderer>();
            if (sprite == null || !sprite.enabled) return false;
            Bounds bounds = sprite.bounds;
            // The actor's ground anchor has a small clearance so its body does not overlap the art.
            return position.x >= bounds.min.x - .12f && position.x <= bounds.max.x + .12f &&
                   position.y >= bounds.min.y - .08f && position.y <= bounds.max.y + .08f;
        }

        private bool FindAstronautPath(Vector2Int start, Vector2Int goal, AstronautView moving, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            if (!map.Inside(start) || !map.Inside(goal) || IsAstronautCellBlocked(goal, moving)) return false;
            if (start == goal) return true;
            bool[,] visited = new bool[Width, Height];
            Vector2Int[,] previous = new Vector2Int[Width, Height];
            Queue<Vector2Int> open = new Queue<Vector2Int>();
            Vector2Int[] directions =
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };
            open.Enqueue(start);
            visited[start.x, start.y] = true;
            while (open.Count > 0 && !visited[goal.x, goal.y])
            {
                Vector2Int current = open.Dequeue();
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;
                    if (!map.Inside(next) || visited[next.x, next.y] || IsAstronautCellBlocked(next, moving)) continue;
                    if (direction.x != 0 && direction.y != 0 &&
                        (IsAstronautCellBlocked(current + new Vector2Int(direction.x, 0), moving) ||
                         IsAstronautCellBlocked(current + new Vector2Int(0, direction.y), moving))) continue;
                    visited[next.x, next.y] = true;
                    previous[next.x, next.y] = current;
                    open.Enqueue(next);
                }
            }
            if (!visited[goal.x, goal.y]) return false;
            Vector2Int cursor = goal;
            while (cursor != start)
            {
                path.Add(cursor);
                cursor = previous[cursor.x, cursor.y];
            }
            path.Reverse();
            return true;
        }

        private Vector3 CrewWorkPosition(SpaceFarmState.CrewMember member)
        {
            Vector2Int cell = BaseCell;
            if ((member.Job == CrewJob.Mine || member.Job == CrewJob.Extractor) &&
                member.Target >= 0 && member.Target < map.Deposits.Count)
                cell = map.Deposits[member.Target].Cell;
            else if (member.Job == CrewJob.Greenhouse && member.Target >= 0 && member.Target < state.GreenhouseCells.Count)
                cell = state.GreenhouseCells[member.Target];
            else if (member.Job == CrewJob.Oxygen)
                cell = member.Target == 0 ? BaseCell + new Vector2Int(2, 0) :
                    member.Target - 1 < state.OxygenCells.Count ? state.OxygenCells[member.Target - 1] : BaseCell;
            else if (member.Job == CrewJob.Solar)
                cell = BaseCell + new Vector2Int(-2, -1);
            return CellPosition(cell) + new Vector3(.45f, -.28f, -.1f);
        }

        private void LoadAstronautSprites()
        {
            if (astronautFacingSprites != null && astronautPassASprites != null &&
                astronautWalkingSprites != null && astronautPassBSprites != null) return;
            astronautFacingSprites = new Sprite[AstronautDirectionNames.Length];
            astronautPassASprites = new Sprite[AstronautDirectionNames.Length];
            astronautWalkingSprites = new Sprite[AstronautDirectionNames.Length];
            astronautPassBSprites = new Sprite[AstronautDirectionNames.Length];
            Sprite[] sprites = Resources.LoadAll<Sprite>("Art/space-farm-colonist-directions");
            Sprite[] passA = Resources.LoadAll<Sprite>("Art/space-farm-colonist-pass-a");
            Sprite[] steps = Resources.LoadAll<Sprite>("Art/space-farm-colonist-walk");
            Sprite[] passB = Resources.LoadAll<Sprite>("Art/space-farm-colonist-pass-b");
            for (int direction = 0; direction < AstronautDirectionNames.Length; direction++)
            {
                string name = AstronautDirectionNames[direction];
                astronautFacingSprites[direction] = FindAstronautSprite(sprites, "colonist-facing-" + name);
                astronautPassASprites[direction] = FindAstronautSprite(passA, "colonist-pass-a-" + name);
                astronautWalkingSprites[direction] = FindAstronautSprite(steps, "colonist-step-" + name);
                astronautPassBSprites[direction] = FindAstronautSprite(passB, "colonist-pass-b-" + name);
            }
        }

        private static Sprite FindAstronautSprite(Sprite[] sprites, string name)
        {
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null && sprites[i].name == name) return sprites[i];
            return null;
        }

        private void SetAstronautIdle(AstronautView view)
        {
            Sprite sprite = astronautPassASprites[view.FacingIndex];
            if (sprite == null) sprite = astronautFacingSprites[view.FacingIndex];
            if (sprite != null) view.Body.sprite = sprite;
        }

        private void SetAstronautFacing(AstronautView view, Vector3 direction, int frame)
        {
            if (direction.sqrMagnitude < .0001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt((90f - angle) / 45f);
            index = ((index % AstronautDirectionNames.Length) + AstronautDirectionNames.Length) % AstronautDirectionNames.Length;
            view.FacingIndex = index;
            Sprite sprite = frame == 0 ? astronautFacingSprites[index] :
                frame == 1 ? astronautPassASprites[index] :
                frame == 2 ? astronautWalkingSprites[index] : astronautPassBSprites[index];
            if (sprite == null) sprite = astronautFacingSprites[index];
            if (sprite != null) view.Body.sprite = sprite;
        }

        private void ChooseAstronautTarget(AstronautView view, Vector2 currentOffset)
        {
            for (int attempt = 0; attempt < 28; attempt++)
            {
                Vector2 candidate = new Vector2(view.Random.Range(-1.9f, 2.5f), view.Random.Range(-4.4f, -2.1f));
                if ((candidate - currentOffset).sqrMagnitude < .4f) continue;
                Vector3 candidateWorld = CellPosition(BaseCell) + (Vector3)ProjectGroundOffset(candidate);
                if (IsAstronautPositionBlocked(candidateWorld, view)) continue;
                bool occupied = false;
                for (int i = 0; i < astronauts.Count; i++)
                    if (astronauts[i] != view && (astronauts[i].TargetOffset - candidate).sqrMagnitude < .3f)
                    {
                        occupied = true;
                        break;
                    }
                if (occupied) continue;
                view.TargetOffset = candidate;
                return;
            }
            view.TargetOffset = new Vector2(
                Mathf.Clamp(currentOffset.x + view.Random.Range(-.6f, .6f), -.2f, 2.5f),
                Mathf.Clamp(currentOffset.y - .15f, -3.2f, -2.1f));
        }

        private void AddMineDrill(GameObject mine)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("Art/space-farm-iron-mine-animation");
            Sprite drillSprite = null;
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null && sprites[i].name == "iron-mine-drill-3d")
                {
                    drillSprite = sprites[i];
                    break;
                }
            if (drillSprite == null || mine == null) return;

            GameObject drill = new GameObject("Taladro animado");
            drill.transform.SetParent(mine.transform, false);
            drill.transform.localPosition = new Vector3(0f, .04f, -.025f);
            drill.transform.localScale = new Vector3(.58f, .58f, 1f);
            SpriteRenderer renderer = drill.AddComponent<SpriteRenderer>();
            renderer.sprite = drillSprite;
            renderer.sortingOrder = 9;
            mineDrills.Add(new MineDrillVisual
            {
                Transform = drill.transform,
                RestPosition = drill.transform.localPosition,
                Phase = UnityEngine.Random.value * Mathf.PI * 2f
            });
        }

        private void AnimateWorldArt()
        {
            float time = Time.time;
            if (solarWorld != null)
            {
                solarWorld.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * .65f) * 1.2f);
                SpriteRenderer panels = solarWorld.GetComponent<SpriteRenderer>();
                if (panels != null)
                {
                    float brightness = .96f + Mathf.Sin(time * 1.7f) * .04f;
                    panels.color = new Color(brightness, brightness, brightness, 1f);
                }
            }
            for (int i = mineDrills.Count - 1; i >= 0; i--)
            {
                MineDrillVisual drill = mineDrills[i];
                if (drill.Transform == null)
                {
                    mineDrills.RemoveAt(i);
                    continue;
                }
                float cycle = (Mathf.Sin(time * 5.2f + drill.Phase) + 1f) * .5f;
                float stroke = Mathf.SmoothStep(-.13f, .02f, cycle);
                drill.Transform.localPosition = drill.RestPosition + Vector3.up * stroke;
                drill.Transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 5.2f + drill.Phase) * 1.5f);
            }
        }

        private static Vector2 ProjectGroundOffset(Vector2 offset) =>
            new Vector2(offset.x, offset.y * SpaceFarmMap.TerrainPerspectiveYScale);
    }
}

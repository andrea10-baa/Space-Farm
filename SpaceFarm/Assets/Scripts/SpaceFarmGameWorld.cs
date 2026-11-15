using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
            public RectTransform MiniMarker;
        }

        private readonly Dictionary<Vector2Int, GameObject> barrierSegments = new Dictionary<Vector2Int, GameObject>();
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
        private readonly List<GameObject> greenhouseWorlds = new List<GameObject>();
        private readonly List<GameObject> oxygenWorlds = new List<GameObject>();
        private bool barrierMode;
        private bool barrierVertical;
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

            CreateArtwork("Nave y hábitat", "Art/space-farm-world-modules#habitat-3d", CellPosition(BaseCell) + new Vector3(0, 0, -.2f),
                3.1f, 2.07f, 5, worldRoot.transform);
            CreateArtwork("Planta de oxígeno", "Art/space-farm-world-modules#oxygen-3d",
                CellPosition(BaseCell + new Vector2Int(2, 0)) + new Vector3(0, 0, -.2f),
                1.7f, 1.14f, 6, worldRoot.transform);
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

            Transform root = new GameObject("Rocas y grava marciana").transform;
            root.SetParent(worldRoot.transform);
            SpaceFarmRandom random = new SpaceFarmRandom((ulong)(uint)map.Seed * 47UL + 193UL);
            int desiredCount = SpaceFarmMap.Width * SpaceFarmMap.Height / 13;
            int placed = 0;
            int attempts = desiredCount * 5;
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
                CreateWorldSprite("Afloramiento marciano", sprite, position,
                    new Vector3(scale, scale * depth, 1f), Color.white, -1, root);
                placed++;
            }
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

        private void CreateBarrierWorld()
        {
            GameObject root = new GameObject("Muros colocados por el jugador");
            root.transform.SetParent(worldRoot.transform);
            barrierPreview = CreateBarrierVisual(map.BaseCell, false, true, root.transform);
            barrierPreview.name = "Vista previa del muro";
            barrierPreview.SetActive(false);
        }

        private GameObject CreateBarrierVisual(Vector2Int cell, bool vertical, bool preview, Transform parent)
        {
            GameObject root = new GameObject(preview ? "Vista previa" : $"Muro {cell.x + 1},{cell.y + 1}");
            root.transform.SetParent(parent);
            root.transform.position = CellPosition(cell) + new Vector3(0f, 0f, -.24f);
            root.transform.rotation = Quaternion.Euler(0f, 0f, vertical ? 90f : 0f);
            float length = .82f;
            Color body = preview ? new Color(.5f,.94f,.91f,.55f) : SpaceFarmPalette.Rover;
            Color trim = preview ? new Color(.75f,1f,1f,.55f) : SpaceFarmPalette.Iron;
            CreateWorldSprite("Viga", squareSprite, root.transform.position,
                new Vector3(length,.2f,1f),
                body, 7, root.transform);
            CreateWorldSprite("Refuerzo", squareSprite, root.transform.position + new Vector3(0,0,-.01f),
                new Vector3(length,.055f,1f),
                trim, 8, root.transform);
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 offset = new Vector3(end * length * .5f,0,-.02f);
                CreateWorldSprite("Unión", squareSprite, root.transform.position + offset,
                    new Vector3(.19f,.19f,1f), trim, 8, root.transform);
            }
            return root;
        }

        private void CreateSolarWorld()
        {
            solarWorld = CreateArtwork("Matriz solar", "Art/space-farm-world-modules#solar-3d",
                CellPosition(BaseCell) + new Vector3(-2.1f,-.8f,-.25f), 2.1f, 1.4f, 6, worldRoot.transform).gameObject;
            solarBaseScale = solarWorld.transform.localScale;
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
                    bool built = state.BuildBarrierSegment(cell, barrierVertical, revealed[cell.x, cell.y], out string message);
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
                if (barrierMode && keyboard.rKey.wasPressedThisFrame)
                {
                    barrierVertical = !barrierVertical;
                    if (barrierPreview != null)
                        barrierPreview.transform.rotation = Quaternion.Euler(0f, 0f, barrierVertical ? 90f : 0f);
                }
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
            if (barrierMode && mouse != null)
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
                    buildModeText.text = $"Muro {(barrierVertical ? "vertical" : "horizontal")} · 1 Fe (tenés {state.Iron})\n" +
                        (valid ? "Posición libre · clic para colocar" : reason);
                barrierPreview.SetActive(!overUi && inside);
                if (!overUi && inside)
                {
                    barrierPreview.transform.position = CellPosition(hoveredBarrierCell) + new Vector3(0f, 0f, -.24f);
                    foreach (SpriteRenderer sprite in barrierPreview.GetComponentsInChildren<SpriteRenderer>())
                        sprite.color = valid ? new Color(.48f,1f,.88f,.58f) : new Color(1f,.31f,.27f,.6f);
                }
            }
            AnimateAstronauts();
        }

        private bool IsActorOccupyingCell(Vector2Int cell)
        {
            return rover != null && WorldToCell(rover.transform.position) == cell;
        }

        private bool IsAstronautPositionBlocked(Vector3 position, AstronautView moving)
        {
            Vector2Int cell = WorldToCell(position);
            if (!map.Inside(cell)) return true;
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
                greenhouseWorlds.Add(built);
                if (greenhouse == null) greenhouse = built;
                AnimatePop(built.transform, built.transform.localScale);
            }
            while (oxygenWorlds.Count < state.OxygenCells.Count)
            {
                int index = oxygenWorlds.Count;
                GameObject built = CreateBuilding($"Planta de oxígeno {index + 2}", "Art/space-farm-world-modules#oxygen-3d",
                    state.OxygenCells[index], worldRoot.transform);
                oxygenWorlds.Add(built);
                if (oxygenPlantTwo == null) oxygenPlantTwo = built;
                AnimatePop(built.transform, built.transform.localScale);
            }
            if (state.ColonyLevel != observedHabitatLevel)
            {
                observedHabitatLevel = state.ColonyLevel;
                Vector3 size = dormBaseScale * (1f + .08f * (state.ColonyLevel - 1));
                if (state.ColonyLevel == 1) dormWorld.transform.localScale = size;
                else AnimatePop(dormWorld.transform, size);
            }
            if (state.SolarLevel != observedSolarLevel)
            {
                observedSolarLevel = state.SolarLevel;
                Vector3 size = solarBaseScale * (1f + .12f * (state.SolarLevel - 1));
                if (state.SolarLevel == 1) solarWorld.transform.localScale = size;
                else AnimatePop(solarWorld.transform, size);
            }
            previousGreenhouse = state.Greenhouse;
            previousRoverLevel = state.RoverLevel;
            previousOxygenPlants = state.OxygenPlants;
            RefreshSystems();
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
                if (iron) AddMineDrill(view.Building);
                view.BuildingScale = view.Building.transform.localScale;
                AnimatePop(view.Building.transform, view.BuildingScale);
                view.ShownLevel = deposit.MineLevel;
            }
            view.Marker.enabled = visible && !deposit.HasBuilding;
            view.Marker.color = deposit.Surveyed ? Color.white : new Color(1f, 1f, 1f, .8f);
            if (view.Building != null && iron)
            {
                Vector3 scale = view.BuildingScale * (deposit.MineLevel >= 2 ? 1.16f : 1f);
                if (deposit.MineLevel != view.ShownLevel) AnimatePop(view.Building.transform, scale);
                view.ShownLevel = deposit.MineLevel;
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
            foreach (SpaceFarmState.BarrierState barrier in state.Barriers)
            {
                if (!barrierSegments.TryGetValue(barrier.Cell, out GameObject visual) || visual == null)
                {
                    visual = CreateBarrierVisual(barrier.Cell, barrier.Vertical, false, worldRoot.transform);
                    barrierSegments[barrier.Cell] = visual;
                }
            }
            for (int i = 0; i < discoveryMarkers.Length; i++)
                if (discoveryMarkers[i] != null)
                    discoveryMarkers[i].enabled = revealed[map.Points[i].Cell.x, map.Points[i].Cell.y] && !state.HasDiscovery(i);
            SyncAstronauts();
            RefreshSystemsHud();
            MarkMinimapDirty();
        }

        private void StartMineCollection(int depositId)
        {
            if (paused) return;
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
            if (!FindReachableCellNear(BaseCell, out Vector2Int dockCell))
            {
                collectionActive = false;
                SetReport("El rover quedó cargado porque no hay un camino libre hacia la base. Abrí un paso entre los muros.");
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
            state.UnloadRover(out string unloaded);
            collectionActive = false;
            SetReport(unloaded);
            PlayWorldPing(CellPosition(dockCell), SpaceFarmPalette.Iron);
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
                        ? dormWorld.transform.position + new Vector3((i % 3 - 1) * .18f, -.18f, 0f)
                        : CrewWorkPosition(member);
                    Vector3 before = view.Root.position;
                    Vector3 delta = destination - before;
                    if (delta.magnitude > .12f)
                    {
                        view.Body.enabled = true;
                        view.Root.position = Vector3.MoveTowards(before, destination, view.Speed * Time.deltaTime * 1.6f);
                        view.WalkCycle = Mathf.Repeat(view.WalkCycle + Vector3.Distance(before, view.Root.position) / .48f, 1f);
                        SetAstronautFacing(view, delta, Mathf.FloorToInt(view.WalkCycle * 4f));
                    }
                    else
                    {
                        view.Body.enabled = !member.Resting;
                        SetAstronautIdle(view);
                    }
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
                Vector3 direction = target - view.Root.position;
                float speed = view.Speed * (state.LastSol.Storm || state.LastSol.RaidFoodLoss > 0 ? .55f : 1f);
                Vector3 previousPosition = view.Root.position;
                Vector3 nextPosition = Vector3.MoveTowards(view.Root.position, target, speed * Time.deltaTime);
                if (IsAstronautPositionBlocked(nextPosition, view))
                {
                    view.Wait = .25f;
                    ChooseAstronautTarget(view, view.TargetOffset);
                    SetAstronautIdle(view);
                    continue;
                }
                view.Root.position = nextPosition;
                float traveled = Vector3.Distance(previousPosition, view.Root.position);
                if (traveled > .0001f)
                {
                    // Four grounded poses per stride; the animation slows whenever the colonist slows.
                    view.WalkCycle = Mathf.Repeat(view.WalkCycle + traveled / .48f, 1f);
                    SetAstronautFacing(view, direction, Mathf.FloorToInt(view.WalkCycle * 4f));
                }
                if (Vector3.Distance(view.Root.position, target) < .025f)
                {
                    view.Wait = view.Random.Range(.4f, 1.75f);
                    view.Speed = view.Random.Range(.42f, .72f);
                    SetAstronautIdle(view);
                    ChooseAstronautTarget(view, view.TargetOffset);
                }
            }
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

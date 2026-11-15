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
            public bool ShownDamaged;
            public RectTransform MiniMarker;
        }

        private readonly GameObject[] barrierSegments = new GameObject[SpaceFarmState.BarrierSlotCount];
        private readonly GameObject[] barrierGuides = new GameObject[SpaceFarmState.BarrierSlotCount];
        private readonly List<AstronautView> astronauts = new List<AstronautView>();
        private readonly List<MineDrillVisual> mineDrills = new List<MineDrillVisual>();
        private readonly Vector2[] astronautStops =
        {
            new Vector2(-1.4f, -1.65f), new Vector2(-.6f, -1.95f),
            new Vector2(.45f, -1.95f), new Vector2(1.55f, -1.6f)
        };
        private DepositView[] depositViews;
        private SpriteRenderer[] discoveryMarkers;
        private Color32[] terrainCellColors;
        private GameObject barrierPreview;
        private GameObject barrierPreviewVertical;
        private GameObject solarWorld;
        private bool barrierMode;
        private bool collectionActive;
        private int hoveredBarrierSlot = -1;
        private int observedPopulation;
        private Sprite[] astronautFacingSprites;
        private Sprite[] astronautPassASprites;
        private Sprite[] astronautWalkingSprites;
        private Sprite[] astronautPassBSprites;
        private static readonly string[] AstronautDirectionNames =
        {
            "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"
        };

        private Vector2 BarrierOffset(int index)
        {
            if (index < 4) return new Vector2(-1.875f + index * 1.25f, 2.2f);
            if (index < 8) return new Vector2(-1.875f + (index - 4) * 1.25f, -2.2f);
            if (index < 10) return new Vector2(-2.5f, index == 8 ? -1.1f : 1.1f);
            return new Vector2(2.5f, index == 10 ? -1.1f : 1.1f);
        }

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
                CellPosition(roverCell) + new Vector3(-.65f, -.5f * SpaceFarmMap.TerrainPerspectiveYScale, -.5f),
                1.55f, 1.55f, 8, worldRoot.transform);
            rover = roverRenderer.gameObject;
            // Optional buildings are created only after a player confirms their chosen cell.
            CreateBarrierWorld();
            CreateSolarWorld();
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
            GameObject root = new GameObject("Perímetro modular");
            root.transform.SetParent(worldRoot.transform);
            for (int i = 0; i < barrierSegments.Length; i++)
            {
                barrierSegments[i] = CreateBarrierVisual(i, root.transform, false);
                barrierSegments[i].SetActive(false);
                barrierGuides[i] = CreateBarrierVisual(i, root.transform, true);
                barrierGuides[i].name = $"Zona {i + 1}";
                foreach (SpriteRenderer sprite in barrierGuides[i].GetComponentsInChildren<SpriteRenderer>())
                    sprite.color = new Color(.6f,1f,.94f,.24f);
                barrierGuides[i].SetActive(false);
            }
            barrierPreview = CreateBarrierVisual(0, root.transform, true);
            barrierPreview.name = "Vista previa de barrera";
            barrierPreview.SetActive(false);
            barrierPreviewVertical = CreateBarrierVisual(8, root.transform, true);
            barrierPreviewVertical.name = "Vista previa vertical";
            barrierPreviewVertical.SetActive(false);
        }

        private GameObject CreateBarrierVisual(int index, Transform parent, bool preview)
        {
            GameObject root = new GameObject(preview ? "Vista previa" : $"Barrera {index + 1}");
            root.transform.SetParent(parent);
            Vector2 barrierOffset = BarrierOffset(index);
            barrierOffset.y *= SpaceFarmMap.TerrainPerspectiveYScale;
            root.transform.position = CellPosition(BaseCell) + (Vector3)barrierOffset;
            bool horizontal = index < 8;
            float length = horizontal ? 1.25f : 2.2f * SpaceFarmMap.TerrainPerspectiveYScale;
            Color body = preview ? new Color(.5f,.94f,.91f,.55f) : SpaceFarmPalette.Rover;
            Color trim = preview ? new Color(.75f,1f,1f,.55f) : SpaceFarmPalette.Iron;
            CreateWorldSprite("Viga", squareSprite, root.transform.position,
                horizontal ? new Vector3(length,.15f,1f) : new Vector3(.15f,length,1f),
                body, 7, root.transform);
            CreateWorldSprite("Refuerzo", squareSprite, root.transform.position + new Vector3(0,0,-.01f),
                horizontal ? new Vector3(length,.045f,1f) : new Vector3(.045f,length,1f),
                trim, 8, root.transform);
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 offset = horizontal ? new Vector3(end * length * .5f,0,-.02f) :
                    new Vector3(0,end * length * .5f,-.02f);
                CreateWorldSprite("Unión", squareSprite, root.transform.position + offset,
                    new Vector3(.19f,.19f,1f), trim, 8, root.transform);
            }
            return root;
        }

        private void CreateSolarWorld()
        {
            solarWorld = CreateArtwork("Matriz solar", "Art/space-farm-world-modules#solar-3d",
                CellPosition(BaseCell) + new Vector3(-2.1f,-.8f,-.25f), 2.1f, 1.4f, 6, worldRoot.transform).gameObject;
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

        private int FindBarrierSlot(Vector3 world)
        {
            Vector2 local = (Vector2)(world - CellPosition(BaseCell));
            for (int i = 0; i < barrierSegments.Length; i++)
            {
                Vector2 d = local - BarrierOffset(i);
                if (i < 8 ? Mathf.Abs(d.x) <= .62f && Mathf.Abs(d.y) <= .35f :
                    Mathf.Abs(d.x) <= .35f && Mathf.Abs(d.y) <= 1.08f) return i;
            }
            return -1;
        }

        private bool TryHandleWorldClick(Vector3 world)
        {
            if (barrierMode)
            {
                int slot = FindBarrierSlot(world);
                Vector2Int cell = WorldToCell(world);
                if (slot < 0 || !map.Inside(cell) || !revealed[cell.x, cell.y])
                    SetReport("Solo podés colocar segmentos en las 12 posiciones marcadas alrededor de la base.");
                else
                {
                    bool built = state.BuildBarrierSegment(slot, out string message);
                    SetReport(message);
                    if (built)
                    {
                        PlayWorldPing(barrierSegments[slot].transform.position, SpaceFarmPalette.Iron);
                        RefreshWorld();
                        RefreshHud();
                        if (state.BarrierCount == SpaceFarmState.BarrierSlotCount) EndBarrierMode();
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
            if (state.Greenhouse && greenhouse != null && Vector2.Distance(world, greenhouse.transform.position) < .85f)
                return SelectWorldBuilding("greenhouse");
            if (state.OxygenPlants > 1 && oxygenPlantTwo != null && Vector2.Distance(world, oxygenPlantTwo.transform.position) < .8f)
                return SelectWorldBuilding("oxygen2");
            if (Vector2.Distance(world, CellPosition(BaseCell + new Vector2Int(2,0))) < .78f)
                return SelectWorldBuilding("oxygen1");
            if (Vector2.Distance(world, solarWorld.transform.position + new Vector3(.33f,0,0)) < .9f)
                return SelectWorldBuilding("solar");
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
            if (barrierMode && mouse != null)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                Vector3 world = ScreenToWorld(mouse.position.ReadValue());
                hoveredBarrierSlot = overUi ? -1 : FindBarrierSlot(world);
                Vector2Int cell = WorldToCell(world);
                bool validCell = map.Inside(cell) && revealed[cell.x, cell.y];
                string reason = overUi ? "Apuntá al mapa" : hoveredBarrierSlot < 0 ? "Elegí una marca del perímetro" :
                    !validCell ? "Sector oculto" : state.HasBarrierSegment(hoveredBarrierSlot) ? "Segmento ya construido" :
                    state.Iron < 1 ? "Falta 1 Fe" : "VÁLIDO · clic para instalar";
                if (buildModeText != null)
                    buildModeText.text = $"Barrera · 1 Fe (tenés {state.Iron})\n{reason}";
                bool vertical = hoveredBarrierSlot >= 8;
                barrierPreview.SetActive(!overUi && !vertical);
                barrierPreviewVertical.SetActive(!overUi && vertical);
                if (!overUi)
                {
                    int slot = hoveredBarrierSlot;
                    GameObject preview = vertical ? barrierPreviewVertical : barrierPreview;
                    preview.transform.position = slot >= 0 ?
                        barrierSegments[slot].transform.position : new Vector3(world.x,world.y,-.25f);
                    bool valid = slot >= 0 && validCell && !state.HasBarrierSegment(slot) && state.Iron > 0;
                    foreach (SpriteRenderer sprite in preview.GetComponentsInChildren<SpriteRenderer>())
                        sprite.color = valid ? new Color(.48f,1f,.88f,.58f) : new Color(1f,.31f,.27f,.6f);
                }
            }
            AnimateAstronauts();
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
            if (state.GreenhouseCell.HasValue && greenhouse == null)
            {
                greenhouse = CreateBuilding("Invernadero", "Art/space-farm-world-modules#greenhouse-3d",
                    state.GreenhouseCell.Value, worldRoot.transform);
                AnimatePop(greenhouse.transform, greenhouse.transform.localScale);
            }
            if (state.OxygenPlantTwoCell.HasValue && oxygenPlantTwo == null)
            {
                oxygenPlantTwo = CreateBuilding("Planta de oxígeno 2", "Art/space-farm-world-modules#oxygen-3d",
                    state.OxygenPlantTwoCell.Value, worldRoot.transform);
                AnimatePop(oxygenPlantTwo.transform, oxygenPlantTwo.transform.localScale);
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
                view.Building.GetComponent<SpriteRenderer>().color = deposit.MineDamaged ? SpaceFarmPalette.DamagedTint : Color.white;
                if (deposit.MineDamaged && !view.ShownDamaged) FlashScreen(SpaceFarmPalette.Danger, .13f, .28f);
                view.ShownLevel = deposit.MineLevel;
                view.ShownDamaged = deposit.MineDamaged;
            }

            string text;
            if (!deposit.Surveyed) text = $"SEÑAL {deposit.Name}";
            else if (deposit.MineLevel > 0 && deposit.MineDamaged) text = $"{deposit.Name} · AVERIADA";
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
            for (int i = 0; i < barrierSegments.Length; i++)
            {
                if (barrierSegments[i] != null) barrierSegments[i].SetActive(state.HasBarrierSegment(i));
                if (barrierGuides[i] != null) barrierGuides[i].SetActive(barrierMode && !state.HasBarrierSegment(i));
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
            if (state.GameOver) { SetReport("La misión terminó. Abrí PAUSA (Esc) para reiniciar."); return; }
            if (travel != null || collectionActive) { SetReport("Esperá a que el rover termine su recorrido."); return; }
            if (deposit.MineStored == 0) { SetReport($"La mina de {deposit.Name} todavía no tiene hierro almacenado."); return; }
            collectionActive = true;
            StartCoroutine(MineCollectionRoutine(depositId));
            RefreshHud();
        }

        private IEnumerator MineCollectionRoutine(int depositId)
        {
            Vector2Int mineCell = map.Deposits[depositId].Cell;
            roverDestination = mineCell;
            routeMarker.transform.position = CellPosition(mineCell);
            routeMarker.enabled = true;
            SetReport($"Rover en camino a la mina de {map.Deposits[depositId].Name}.");
            travel = StartCoroutine(TravelTo());
            yield return travel;
            state.LoadMineToRover(depositId, out string loaded);
            SetReport(loaded);
            RefreshWorld();
            RefreshHud();
            roverDestination = BaseCell;
            routeMarker.transform.position = CellPosition(BaseCell);
            routeMarker.enabled = true;
            travel = StartCoroutine(TravelTo());
            yield return travel;
            // Cargo moves in two atomic steps (mine to rover, rover to habitat), so a pause in between cannot lose or duplicate iron.
            state.UnloadRover(out string unloaded);
            collectionActive = false;
            SetReport(unloaded);
            PlayWorldPing(CellPosition(BaseCell), SpaceFarmPalette.Iron);
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
                view.Root.position = Vector3.MoveTowards(view.Root.position, target, speed * Time.deltaTime);
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
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 candidate = new Vector2(view.Random.Range(-2.8f, 2.8f), view.Random.Range(-2.8f, -1.7f));
                if ((candidate - currentOffset).sqrMagnitude < .4f) continue;
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
                Mathf.Clamp(currentOffset.x + view.Random.Range(-.6f, .6f), -2.8f, 2.8f),
                Mathf.Clamp(currentOffset.y - .15f, -2.8f, -1.7f));
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

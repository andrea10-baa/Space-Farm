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
        private readonly List<Transform> astronauts = new List<Transform>();
        private readonly List<float> astronautWait = new List<float>();
        private readonly List<int> astronautGoal = new List<int>();
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
        private float astronautClock;

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
            CreateFog();
            CreateDepositViews();

            CreateArtwork("Nave y hábitat", "Art/lander", CellPosition(BaseCell) + new Vector3(0, 0, -.2f),
                3.1f, 2.07f, 5, worldRoot.transform);
            CreateArtwork("Planta de oxígeno", "Art/oxygen-plant",
                CellPosition(BaseCell + new Vector2Int(2, 0)) + new Vector3(0, 0, -.2f),
                1.7f, 1.14f, 6, worldRoot.transform);
            rover = CreateArtwork("Rover 1", "Art/rover", CellPosition(roverCell) + new Vector3(-.65f, -.5f, -.5f),
                1.55f, 1.04f, 8, worldRoot.transform).gameObject;
            greenhouse = CreateBuilding("Invernadero", "Art/greenhouse", BaseCell + new Vector2Int(-2, 1), worldRoot.transform);
            oxygenPlantTwo = CreateBuilding("Planta de oxígeno 2", "Art/oxygen-plant", BaseCell + new Vector2Int(2, 2), worldRoot.transform);
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
            greenhouse.SetActive(false);
            oxygenPlantTwo.SetActive(false);
        }

        /// <summary>The whole 80×50 region is one painted texture plus one tiled grain layer: two renderers in total.</summary>
        private void CreateTerrain()
        {
            SpaceFarmTerrainPainter.Result painted = SpaceFarmTerrainPainter.Paint(map);
            terrainCellColors = painted.CellColors;
            Texture2D texture = new Texture2D(painted.Width, painted.Height, TextureFormat.RGB24, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(painted.Pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, painted.Width, painted.Height), new Vector2(.5f, .5f),
                SpaceFarmTerrainPainter.PixelsPerCell / TileSize, 0, SpriteMeshType.FullRect);
            CreateWorldSprite("Terreno generado", sprite, Vector3.zero, Vector3.one, Color.white, -11, worldRoot.transform);

            const int grainSize = 128;
            Texture2D grain = new Texture2D(grainSize, grainSize, TextureFormat.RGBA32, false);
            grain.filterMode = FilterMode.Bilinear;
            grain.wrapMode = TextureWrapMode.Repeat;
            grain.SetPixels32(SpaceFarmTerrainPainter.PaintGrain(grainSize, seed));
            grain.Apply(false, true);
            Sprite grainSprite = Sprite.Create(grain, new Rect(0, 0, grainSize, grainSize), new Vector2(.5f, .5f),
                grainSize / (TileSize * 3f), 0, SpriteMeshType.FullRect);
            SpriteRenderer grainRenderer = CreateWorldSprite("Grano del suelo", grainSprite, new Vector3(0f, 0f, -.01f),
                Vector3.one, Color.white, -10, worldRoot.transform);
            grainRenderer.drawMode = SpriteDrawMode.Tiled;
            grainRenderer.size = new Vector2(Width * TileSize, Height * TileSize);
            runtimeAssets.AddRange(new UnityEngine.Object[] { sprite, texture, grainSprite, grain });
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
            root.transform.position = CellPosition(BaseCell) + (Vector3)BarrierOffset(index);
            bool horizontal = index < 8;
            float length = horizontal ? 1.25f : 2.2f;
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
            solarWorld = new GameObject("Generación solar");
            solarWorld.transform.SetParent(worldRoot.transform);
            solarWorld.transform.position = CellPosition(BaseCell) + new Vector3(-2.1f,-.8f,-.25f);
            for (int panel = 0; panel < 2; panel++)
            {
                Vector3 p = solarWorld.transform.position + new Vector3(panel * .66f,0,0);
                CreateWorldSprite("Panel solar", squareSprite, p, new Vector3(.58f,.5f,1f),
                    new Color(.12f,.23f,.34f,1f), 6, solarWorld.transform);
                for (int line = -1; line <= 1; line++)
                    CreateWorldSprite("Celdas", squareSprite, p + new Vector3(line * .15f,0,-.02f),
                        new Vector3(.025f,.43f,1f), SpaceFarmPalette.Ice, 7, solarWorld.transform);
            }
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
            if (state.Greenhouse && Vector2.Distance(world, greenhouse.transform.position) < .85f)
                return SelectWorldBuilding("greenhouse");
            if (state.OxygenPlants > 1 && Vector2.Distance(world, oxygenPlantTwo.transform.position) < .8f)
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
                bool vertical = hoveredBarrierSlot >= 8;
                barrierPreview.SetActive(!overUi && !vertical);
                barrierPreviewVertical.SetActive(!overUi && vertical);
                if (!overUi)
                {
                    int slot = hoveredBarrierSlot;
                    GameObject preview = vertical ? barrierPreviewVertical : barrierPreview;
                    preview.transform.position = slot >= 0 ?
                        barrierSegments[slot].transform.position : new Vector3(world.x,world.y,-.25f);
                    bool valid = slot >= 0 && !state.HasBarrierSegment(slot) && state.Iron > 0;
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
            if (state.Greenhouse && !previousGreenhouse) AnimatePop(greenhouse.transform, greenhouse.transform.localScale);
            if (state.OxygenPlants > previousOxygenPlants && previousOxygenPlants > 0)
                AnimatePop(oxygenPlantTwo.transform, oxygenPlantTwo.transform.localScale);
            greenhouse.SetActive(state.Greenhouse);
            oxygenPlantTwo.SetActive(state.OxygenPlants > 1);
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
                    iron ? "Art/iron-mine" : "Art/water-extractor", view.Info.Cell, view.Marker.transform.parent);
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
            if (state.Population == observedPopulation) return;
            while (astronauts.Count < state.Population)
            {
                int index = astronauts.Count;
                GameObject root = new GameObject($"Astronauta {index + 1}");
                root.transform.SetParent(worldRoot.transform);
                root.transform.position = CellPosition(BaseCell) +
                    (Vector3)(astronautStops[index % astronautStops.Length] + AstronautOffset(index));
                CreateWorldSprite("Traje", circleSprite, root.transform.position,
                    new Vector3(.22f,.28f,1f), new Color(.94f,.91f,.78f), 9, root.transform);
                CreateWorldSprite("Visor", circleSprite, root.transform.position + new Vector3(0,.08f,-.02f),
                    new Vector3(.14f,.12f,1f), SpaceFarmPalette.Ice, 10, root.transform);
                astronauts.Add(root.transform);
                astronautWait.Add(.35f * index);
                astronautGoal.Add((index + 1) % astronautStops.Length);
            }
            while (astronauts.Count > state.Population)
            {
                int last = astronauts.Count - 1;
                Destroy(astronauts[last].gameObject);
                astronauts.RemoveAt(last);
                astronautWait.RemoveAt(last);
                astronautGoal.RemoveAt(last);
            }
            observedPopulation = state.Population;
        }

        private void AnimateAstronauts()
        {
            if (state == null) return;
            astronautClock += Time.deltaTime;
            for (int i = 0; i < astronauts.Count; i++)
            {
                Transform person = astronauts[i];
                if (state.GameOver) continue;
                if (astronautWait[i] > 0f) { astronautWait[i] -= Time.deltaTime; continue; }
                Vector3 target = CellPosition(BaseCell) + (Vector3)(astronautStops[astronautGoal[i]] + AstronautOffset(i));
                float speed = state.LastSol.Storm || state.LastSol.RaidFoodLoss > 0 ? .27f : .52f;
                person.position = Vector3.MoveTowards(person.position, target, speed * Time.deltaTime);
                SpriteRenderer suit = person.GetComponentInChildren<SpriteRenderer>();
                if (suit != null) suit.transform.localScale = new Vector3(.22f, .28f + Mathf.Sin(astronautClock * 13f + i) * .018f, 1f);
                if (Vector3.Distance(person.position, target) < .02f)
                {
                    astronautWait[i] = .5f + i * .18f;
                    astronautGoal[i] = (astronautGoal[i] + 1) % astronautStops.Length;
                }
            }
        }

        private Vector2 AstronautOffset(int index) => new Vector2((index - 1) * .27f, -index * .08f);
    }
}

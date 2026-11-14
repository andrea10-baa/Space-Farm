using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SpaceFarm
{
    public sealed partial class SpaceFarmGame
    {
        private static readonly Vector2Int[] DiscoveryCells =
        {
            new Vector2Int(5, 5), new Vector2Int(25, 15), new Vector2Int(24, 4)
        };
        private readonly GameObject[] barrierSegments = new GameObject[SpaceFarmState.BarrierSlotCount];
        private readonly GameObject[] barrierGuides = new GameObject[SpaceFarmState.BarrierSlotCount];
        private readonly SpriteRenderer[] discoveryMarkers = new SpriteRenderer[3];
        private readonly List<Transform> astronauts = new List<Transform>();
        private readonly List<float> astronautWait = new List<float>();
        private readonly List<int> astronautGoal = new List<int>();
        private readonly Vector2[] astronautStops =
        {
            new Vector2(-1.4f, -1.65f), new Vector2(-.6f, -1.95f),
            new Vector2(.45f, -1.95f), new Vector2(1.55f, -1.6f)
        };
        private GameObject barrierPreview;
        private GameObject barrierPreviewVertical;
        private GameObject solarWorld;
        private TextMeshPro mineWorldLabel;
        private SpriteRenderer mineLabelBacking;
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

        private void CreateTerrainTiles()
        {
            CreateArtwork("Terreno continuo", "Art/mars-terrain", Vector3.zero,
                Width * TileSize, Height * TileSize, -11, worldRoot.transform);
            Color[] tints =
            {
                new Color(1f,1f,1f,.05f), new Color(.91f,.91f,.9f,.05f), new Color(.96f,.88f,.82f,.05f),
                new Color(.89f,.84f,.81f,.05f), new Color(.95f,.93f,.88f,.05f), new Color(.86f,.83f,.83f,.05f)
            };
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
            {
                SpriteRenderer tile = CreateArtwork($"Terreno {x},{y}", "Art/mars-terrain",
                    new Vector3((x - 1) * 9f, (y - .5f) * 9f, 0f), 9.05f, 9.05f, -10, worldRoot.transform);
                tile.color = tints[y * 3 + x];
                if ((x + y) % 2 == 1) tile.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            }
            var random = new System.Random(114);
            for (int i = 0; i < 65; i++)
            {
                float x = (float)(random.NextDouble() * 25.8 - 12.9);
                float y = (float)(random.NextDouble() * 16.8 - 8.4);
                if (Mathf.Abs(x) < 3.7f && Mathf.Abs(y) < 3f) continue;
                float size = (float)(.07 + random.NextDouble() * .13);
                CreateWorldSprite("Roca marciana", circleSprite, new Vector3(x,y,-.02f),
                    new Vector3(size, size * .55f, 1f), new Color(.28f,.16f,.14f,.48f), -9, worldRoot.transform);
            }
            for (int i = 0; i < 45; i++)
            {
                float x = (float)(random.NextDouble() * 25.8 - 12.9);
                float y = (float)(random.NextDouble() * 16.8 - 8.4);
                if (Mathf.Abs(x) < 3f && Mathf.Abs(y) < 2.8f) continue;
                float size = (float)(.3 + random.NextDouble() * .7);
                CreateWorldSprite("Cráter", ringSprite, new Vector3(x,y,-.03f),
                    new Vector3(size,size * .7f,1f), new Color(.32f,.16f,.12f,.22f), -9, worldRoot.transform);
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

        private void CreatePointsOfInterest()
        {
            for (int i = 0; i < DiscoveryCells.Length; i++)
            {
                discoveryMarkers[i] = CreateWorldSprite("Punto de interés", ringSprite,
                    CellPosition(DiscoveryCells[i]) + new Vector3(0,0,-.12f),
                    new Vector3(.7f,.7f,1f), i == 1 ? SpaceFarmPalette.Ice : SpaceFarmPalette.Iron,
                    4, worldRoot.transform);
                discoveryMarkers[i].enabled = false;
            }
        }

        private string DiscoverPointOfInterest(Vector2Int cell)
        {
            for (int i = 0; i < DiscoveryCells.Length; i++)
                if (DiscoveryCells[i] == cell && !state.HasDiscovery(i))
                    return state.ClaimDiscovery(i);
            return null;
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
                if (slot < 0 || !Inside(cell) || !revealed[cell.x, cell.y])
                    SetReport("Solo podés colocar segmentos en las 12 posiciones marcadas y reveladas.");
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
            Vector3 basePosition = CellPosition(BaseCell);
            if (state.MineLevel > 0 && Vector2.Distance(world, mine.transform.position) < .85f)
                return SelectWorldBuilding("mine");
            if (state.WaterExtractor && Vector2.Distance(world, waterExtractor.transform.position) < .85f)
                return SelectWorldBuilding("water");
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

        private void UpdateSystems(Mouse mouse)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (barrierMode && keyboard.escapeKey.wasPressedThisFrame) EndBarrierMode();
                Vector2 direction = Vector2.zero;
                if (keyboard.aKey.isPressed) direction.x--;
                if (keyboard.dKey.isPressed) direction.x++;
                if (keyboard.sKey.isPressed) direction.y--;
                if (keyboard.wKey.isPressed) direction.y++;
                if (direction.sqrMagnitude > 0f)
                {
                    gameCamera.transform.position += (Vector3)(direction.normalized * (6f * Time.deltaTime));
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
                    discoveryMarkers[i].enabled = revealed[DiscoveryCells[i].x, DiscoveryCells[i].y] && !state.HasDiscovery(i);
            if (mineWorldLabel == null) CreateMineWorldLabel();
            mineWorldLabel.gameObject.SetActive(state.MineLevel > 0 && state.MineStored > 0);
            mineLabelBacking.gameObject.SetActive(state.MineLevel > 0 && state.MineStored > 0);
            mineWorldLabel.text = state.MineStored == state.MineCapacity ?
                $"LLENO {state.MineStored}/{state.MineCapacity} Fe" : $"RECOGER {state.MineStored} Fe";
            SyncAstronauts();
            RefreshSystemsHud();
        }

        private void CreateMineWorldLabel()
        {
            Vector3 position = CellPosition(IronCell) + new Vector3(0f,-1.15f,-.4f);
            mineLabelBacking = CreateWorldSprite("Fondo de almacén", squareSprite,
                position + new Vector3(0,0,.03f), new Vector3(2.4f,.48f,1f),
                new Color(.1f,.16f,.2f,.88f), 11, worldRoot.transform);
            GameObject label = new GameObject("Hierro almacenado", typeof(TextMeshPro));
            label.transform.SetParent(worldRoot.transform);
            label.transform.position = position;
            label.transform.localScale = Vector3.one * .32f;
            mineWorldLabel = label.GetComponent<TextMeshPro>();
            mineWorldLabel.font = TMP_Settings.defaultFontAsset;
            mineWorldLabel.fontSize = 4f;
            mineWorldLabel.alignment = TextAlignmentOptions.Center;
            mineWorldLabel.color = SpaceFarmPalette.Iron;
            mineWorldLabel.sortingOrder = 12;
        }

        private void StartMineCollection()
        {
            if (state.GameOver) { SetReport("La misión terminó. Reiniciá para volver a intentarlo."); return; }
            if (travel != null || collectionActive) { SetReport("Esperá a que el rover termine su recorrido."); return; }
            if (state.MineStored == 0) { SetReport("La mina todavía no tiene hierro almacenado."); return; }
            collectionActive = true;
            StartCoroutine(MineCollectionRoutine());
            RefreshHud();
        }

        private IEnumerator MineCollectionRoutine()
        {
            roverDestination = IronCell;
            routeMarker.transform.position = CellPosition(IronCell);
            routeMarker.enabled = true;
            travel = StartCoroutine(TravelTo());
            yield return travel;
            state.LoadMineToRover(out string loaded);
            SetReport(loaded);
            RefreshSystems();
            RefreshHud();
            roverDestination = BaseCell;
            routeMarker.transform.position = CellPosition(BaseCell);
            routeMarker.enabled = true;
            travel = StartCoroutine(TravelTo());
            yield return travel;
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
                SpriteRenderer body = CreateWorldSprite("Traje", circleSprite, root.transform.position,
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

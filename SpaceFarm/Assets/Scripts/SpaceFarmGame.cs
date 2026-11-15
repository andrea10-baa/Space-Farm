using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>
    /// Space Farm session: one generated Mars, one colony. This file holds the lifecycle, camera,
    /// pointer input and rover travel. World objects, fog, HUD, minimap and pause live in the
    /// other partial files; map generation and rules are plain classes with no scene access.
    /// </summary>
    public sealed partial class SpaceFarmGame : MonoBehaviour
    {
        private const int Width = SpaceFarmMap.Width;
        private const int Height = SpaceFarmMap.Height;
        private const float TileSize = 0.9f;
        private float RoverSpeed => state != null && state.RoverLevel >= 2 ? 5.2f : 2.7f;
        private const float MinCameraSize = 2.6f;
        private const float MaxCameraSize = 28f;
        private const float DefaultCameraSize = 5.2f;
        private const float DragThreshold = 9f;
        private const float HeaderHeight = 112f;
        private const float FooterHeight = 44f;

        /// <summary>Seed the next session must use. Null means "pick a new one".</summary>
        private static int? pendingSeed;

        private SpaceFarmMap map;
        private SpaceFarmState state;
        private int seed;
        private Camera gameCamera;
        private GameObject rover;
        private SpriteRenderer roverRenderer;
        private Sprite[] roverDirectionalSprites;
        private Sprite[] roverWheelSprites;
        private int roverFacingIndex = 4;
        private Vector2 roverHeading = Vector2.down;
        private GameObject greenhouse;
        private GameObject oxygenPlantTwo;
        private Coroutine travel;
        private Coroutine worldPing;
        private Coroutine screenFlashRoutine;
        private Vector2Int roverDestination;
        private Vector2Int roverCell;
        private bool roverRouteDirty;
        private MapDeposit signalDuringTrip;
        private bool isPanning;
        private bool leftPressValid;
        private bool leftDragging;
        private Vector2 leftPressScreen;
        private bool followRover;
        private Vector3 panWorldPosition;
        private float lastCameraAspect;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float roverIdleClock;
        private Vector2Int lastRevealCenter = new Vector2Int(-1, -1);
        private Vector3 roverBaseScale;
        private Canvas hudCanvas;
        private TextMeshProUGUI statsText;
        private TextMeshProUGUI objectiveText;
        private TextMeshProUGUI reportText;
        private TextMeshProUGUI roverText;
        private TextMeshProUGUI actionDetailText;
        private RectTransform actionCardsRoot;
        private readonly List<ActionCard> actionCards = new List<ActionCard>();
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
        private int selectedCategory;
        private Image screenFlash;
        private Sprite squareSprite;
        private Sprite circleSprite;
        private Sprite ringSprite;
        private SpriteRenderer routeMarker;
        private SpriteRenderer scanPulse;
        private SpriteRenderer signalPing;
        private ParticleSystem scanTrail;
        private GameObject worldRoot;
        private bool previousGreenhouse;
        private int previousRoverLevel = 1;
        private int previousOxygenPlants;
        private float upgradeHudClock;

        // ---------- Hooks for checks and tools; the game itself never depends on them ----------

        public int Seed => seed;
        public SpaceFarmMap Map => map;
        public SpaceFarmState State => state;
        public bool IsPaused => paused;
        public bool RoverBusy => travel != null || collectionActive;
        public Vector2Int RoverCell => roverCell;
        public bool IsRevealed(Vector2Int cell) => map != null && map.Inside(cell) && revealed[cell.x, cell.y];
        /// <summary>Makes the next session (first load or restart) use this seed.</summary>
        public static void RequestSeed(int value) => pendingSeed = value;
        public void OrderRover(Vector2Int cell) { if (!arrivalSequenceActive && map.Inside(cell)) SendRoverTo(cell); }
        public void OrderCollection(int depositId) { if (!arrivalSequenceActive) StartMineCollection(depositId); }
        public void SelectDepositForTools(int depositId) => SelectDeposit(depositId);
        public void SelectWorldObjectForTools(string key, int index = 0) => SelectWorldBuilding(key, index);
        public bool ClickWorldForTools(Vector3 position) => TryHandleWorldClick(position);
        public void CloseWorldCardForTools() => HideWorldSelection();
        public bool IsWorldCardOpenForTools => infoPanel != null && infoPanel.activeSelf;
        public bool WorldCardSecondaryVisibleForTools => infoSecondary != null && infoSecondary.gameObject.activeSelf;
        public bool WorldCardSecondaryEnabledForTools => infoSecondary != null && infoSecondary.interactable;
        public string WorldCardTitleForTools => infoTitle != null ? infoTitle.text : string.Empty;
        public void FocusCell(Vector2Int cell) => FocusWorld(CellPosition(cell));
        public void RestartSameMap() => Restart(true);
        public void RestartNewMap() => Restart(false);
        public void RestartWithSeed(int value) { Restart(true); pendingSeed = value; }
        public void PressDepositPrimary(int depositId) { SelectDeposit(depositId); ContextPrimaryAction(); }
        public void PressAdvanceSol() => AdvanceSol();
        public Vector3 RoverWorldPosition => rover.transform.position;
        public Vector3 CameraWorldPosition => gameCamera.transform.position;
        public void RevealForTools(Vector2Int cell, int radius) { if (Reveal(cell, radius)) RefreshWorld(); }
        /// <summary>Same path as a real press on the minimap picture at that cell.</summary>
        public void PressMinimapCell(Vector2Int cell)
        {
            Vector3[] corners = new Vector3[4];
            minimapView.GetWorldCorners(corners);
            Vector3 point = corners[0] + (corners[2] - corners[0]).x * ((cell.x + .5f) / Width) * Vector3.right
                + (corners[2] - corners[0]).y * ((cell.y + .5f) / Height) * Vector3.up;
            NavigateFromMinimap(RectTransformUtility.WorldToScreenPoint(null, point), null);
        }

        private Vector2Int BaseCell => map.BaseCell;
        private Vector2Int RoverDockCell => BaseCell + new Vector2Int(0, -2);

        private void Start()
        {
            Time.timeScale = 1f;
            SpaceFarmSaveData loadedSave = TakePendingSessionLoad();
            seed = loadedSave != null ? loadedSave.Seed : pendingSeed ?? NewSeed(0);
            pendingSeed = null;
            loadedSavedSession = loadedSave != null;
            activeSessionId = loadedSave != null ? loadedSave.Id : Guid.NewGuid().ToString("N");
            activeSessionName = loadedSave != null ? loadedSave.Name : pendingSessionName;
            pendingSessionName = null;
            float started = Time.realtimeSinceStartup;
            map = SpaceFarmMap.Generate(seed);
            state = new SpaceFarmState(map);
            if (loadedSave != null && !state.RestoreSave(loadedSave.State, out string restoreError))
            {
                Debug.LogError("No se pudo restaurar el guardado: " + restoreError);
                loadedSave = null;
                loadedSavedSession = false;
                activeSessionId = Guid.NewGuid().ToString("N");
                activeSessionName = "Colonia nueva";
            }
            Vector2Int savedRover = loadedSave == null ? RoverDockCell : new Vector2Int(loadedSave.RoverX, loadedSave.RoverY);
            roverCell = roverDestination = map.Inside(savedRover) ? savedRover : RoverDockCell;
            ConfigureCamera();
            CreateSprites();
            CreateWorld();
            CreateHud();
            CreateCrewMenu();
            InitializeSystems();
            CreateMinimap();
            CreatePauseMenu();
            // Restore the revealed chart exactly; a fresh landing receives its initial orbital survey.
            if (loadedSave != null) RestoreRevealMask(loadedSave.Revealed);
            else Reveal(BaseCell, 5);
            roverBaseScale = rover.transform.localScale;
            RefreshWorld();
            PrepareForInitialLanding();
            RefreshHud();
            SetReport($"Preparando el aterrizaje en una región nueva de Marte (semilla {seed}).");
            CreateTitleScreen();
            if (loadedSavedSession) BeginFromTitle();
            Debug.Log($"SPACE_FARM_MAP_READY: {map.Describe()} en {(Time.realtimeSinceStartup - started) * 1000f:0} ms");
        }

        private static int NewSeed(int different)
        {
            // Wall clock, not UnityEngine.Random: the generator must never depend on global random state.
            long ticks = DateTime.UtcNow.Ticks;
            int value = (int)(Math.Abs(ticks ^ (ticks >> 21) ^ Environment.TickCount) % 999999) + 1;
            return value == different ? value % 999999 + 1 : value;
        }

        /// <summary>
        /// Restarts from zero on a fresh component: every building, barrier, deposit view, astronaut,
        /// fog texture and report belongs to this object and is destroyed with it, so nothing is duplicated.
        /// </summary>
        private void Restart(bool sameSeed)
        {
            if (!enabled) return;
            pendingSeed = sameSeed ? seed : NewSeed(seed);
            Time.timeScale = 1f;
            StopAllCoroutines();
            enabled = false;
            GameObject next = new GameObject(gameObject.name);
            next.AddComponent<SpaceFarmGame>();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            foreach (UnityEngine.Object asset in runtimeAssets)
                if (asset != null) Destroy(asset);
            runtimeAssets.Clear();
        }

        private void Update()
        {
            if (state == null || titleScreenOpen || arrivalSequenceActive) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (paused) SetPaused(false);
                else if (crewMenuOpen) CloseCrewMenu();
                else if (editMoveActive) CancelEditMove();
                else if (activeStructure != StructureKind.None) CancelPlacement();
                else if (barrierMode) EndBarrierMode();
                else if (editMode) SetEditMode(false);
                else if (infoPanel != null && infoPanel.activeSelf) HideWorldSelection();
                else if (constructionMenuOpen) CloseConstructionMenu();
                else SetPaused(true);
            }
            // Paused: nothing below runs, so no order, build or sol can happen behind the menu.
            if (paused) return;
            if (state.Tick(Time.deltaTime))
            {
                SetReport(state.LastReport);
                RefreshWorld();
                RefreshHud();
            }
            if (state.Upgrades.Count > 0 || selectedBuilding != null || crewMenuOpen)
            {
                upgradeHudClock += Time.deltaTime;
                if (upgradeHudClock >= .5f) { upgradeHudClock = 0f; RefreshHud(); }
            }
            UpdateFogReveal();
            AnimateRover();
            AnimateWorldArt();
            RefreshUpgradeProgressVisuals();
            UpdateSystems(mouse, keyboard);
            if (mouse != null)
            {
                if (editMoveActive && mouse.rightButton.wasPressedThisFrame)
                {
                    CancelEditMove(); suppressRightPan = true;
                }
                else if (activeStructure != StructureKind.None && mouse.rightButton.wasPressedThisFrame)
                {
                    CancelPlacement();
                    suppressRightPan = true;
                }
                if (mouse.rightButton.wasReleasedThisFrame) suppressRightPan = false;
                HandleCameraInput(mouse);
                HandleWorldPointer(mouse);
                UpdatePlacementPreview(mouse);
                UpdateEditMovePreview(mouse);
            }
            UpdateFollow();
            UpdateMinimap();
            TickAutomaticLogistics();
        }

        private void HandleWorldPointer(Mouse mouse)
        {
            if (constructionMenuOpen || crewMenuOpen || interactionMode == InteractionMode.DraggingStructure) return;
            if (infoPanel != null && infoPanel.activeSelf) return;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                leftPressValid = !overUi;
                leftDragging = false;
                leftPressScreen = pointer;
                panWorldPosition = ScreenToWorld(pointer);
            }
            if (leftPressValid && mouse.leftButton.isPressed)
            {
                if (!leftDragging && (pointer - leftPressScreen).sqrMagnitude > DragThreshold * DragThreshold)
                {
                    leftDragging = true;
                    interactionMode = InteractionMode.PanningCamera;
                    followRover = false;
                    RefreshFollowButton();
                }
                if (leftDragging)
                {
                    gameCamera.transform.position += panWorldPosition - ScreenToWorld(pointer);
                    ClampCamera();
                }
            }
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            // A press that turned into a drag moved the camera; it is never an order or a barrier.
            bool click = leftPressValid && !leftDragging;
            leftPressValid = false;
            leftDragging = false;
            if (interactionMode == InteractionMode.PanningCamera && !isPanning)
                interactionMode = activeStructure != StructureKind.None ? InteractionMode.PlacingStructure : InteractionMode.Explore;
            if (!click || overUi || state.GameOver) return;
            Vector3 world = ScreenToWorld(pointer);
            if (editMoveActive)
            {
                ConfirmEditMove(WorldToCell(world));
                return;
            }
            if (activeStructure != StructureKind.None)
            {
                ConfirmPlacement(WorldToCell(world));
                return;
            }
            if (TryHandleWorldClick(world)) return;
            if (editMode) { SetReport("MODO EDITAR · Seleccioná un muro o una estructura construida."); return; }
            Vector2Int target = WorldToCell(world);
            if (map.Inside(target)) SendRoverTo(target);
        }

        private void ConfigureCamera()
        {
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                gameCamera = cameraObject.AddComponent<Camera>();
            }
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = DefaultCameraSize;
            Vector3 home = CellPosition(BaseCell);
            gameCamera.transform.position = new Vector3(home.x, home.y, -10f);
            gameCamera.backgroundColor = SpaceFarmPalette.Background;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            lastCameraAspect = gameCamera.aspect;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            ClampCamera();
        }

        private void CreateSprites()
        {
            Texture2D square = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] squarePixels = new Color[16];
            for (int i = 0; i < squarePixels.Length; i++) squarePixels[i] = Color.white;
            square.SetPixels(squarePixels);
            square.filterMode = FilterMode.Point;
            square.Apply();
            squareSprite = Sprite.Create(square, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);

            Texture2D circle = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color[] circlePixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 15.5f;
                float dy = y - 15.5f;
                circlePixels[y * 32 + x] = dx * dx + dy * dy <= 225 ? Color.white : Color.clear;
            }
            circle.SetPixels(circlePixels);
            circle.filterMode = FilterMode.Bilinear;
            circle.Apply();
            circleSprite = Sprite.Create(circle, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);

            Texture2D ring = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] ringPixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance - 25f) / 3.5f);
                ringPixels[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
            }
            ring.SetPixels(ringPixels);
            ring.filterMode = FilterMode.Bilinear;
            ring.Apply();
            ringSprite = Sprite.Create(ring, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
            runtimeAssets.AddRange(new UnityEngine.Object[] { squareSprite, circleSprite, ringSprite, square, circle, ring });
        }

        private GameObject CreateBuilding(string name, string artPath, Vector2Int cell, Transform parent)
        {
            return CreateArtwork(name, artPath, CellPosition(cell) + new Vector3(0, 0, -.2f),
                1.75f, 1.17f, 6, parent).gameObject;
        }

        private SpriteRenderer CreateArtwork(string name, string path, Vector3 position, float width, float height,
            int order, Transform parent)
        {
            Sprite sprite = null;
            int separator = path.IndexOf('#');
            if (separator >= 0)
            {
                string atlasPath = path.Substring(0, separator);
                string spriteName = path.Substring(separator + 1);
                Sprite[] sprites = Resources.LoadAll<Sprite>(atlasPath);
                for (int i = 0; i < sprites.Length; i++)
                    if (sprites[i] != null && sprites[i].name == spriteName)
                    {
                        sprite = sprites[i];
                        break;
                    }
            }
            else sprite = Resources.Load<Sprite>(path);
            if (sprite == null) sprite = squareSprite;
            Vector3 scale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1f);
            return CreateWorldSprite(name, sprite, position, scale, Color.white, order, parent);
        }

        private SpriteRenderer CreateWorldSprite(string name, Sprite sprite, Vector3 position, Vector3 scale,
            Color tint, int order, Transform parent)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent);
            item.transform.position = position;
            item.transform.localScale = scale;
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = order;
            return renderer;
        }

        private Vector3 CellPosition(Vector2Int cell)
        {
            return new Vector3((cell.x - (Width - 1) / 2f) * TileSize,
                (cell.y - (Height - 1) / 2f) * TileSize * SpaceFarmMap.TerrainPerspectiveYScale, 0f);
        }

        private Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(Mathf.RoundToInt(world.x / TileSize + (Width - 1) / 2f),
                Mathf.RoundToInt(world.y / (TileSize * SpaceFarmMap.TerrainPerspectiveYScale) + (Height - 1) / 2f));
        }

        private static string Sector(Vector2Int cell) => $"{cell.x + 1},{cell.y + 1}";

        // ---------- Rover ----------

        private void SendRoverTo(Vector2Int target)
        {
            if (paused) return;
            if (state.IsDamaged(DamagedStructureKind.Rover))
            {
                SetReport($"El rover está averiado. Reparalo por {state.RepairIronCost} Fe y {state.RepairEnergyCost} energía antes de moverlo.");
                return;
            }
            if (!map.Inside(target)) { SetReport("Ese sector queda fuera del mapa."); return; }
            if (!state.CanRoverReach(target))
            {
                SetReport($"Zona bloqueada: la colonia está en nivel {state.ColonyLevel}. Alcance actual: {state.RoverRange} sectores desde la base. Ganá XP para avanzar.");
                return;
            }
            if (target != roverCell && state.IsCellBlocked(target))
            {
                SetReport("El rover no puede entrar en un sector ocupado por un muro o una estructura.");
                return;
            }
            if (collectionActive)
            {
                SetReport("El rover está recogiendo hierro. Esperá a que descargue en el hábitat.");
                return;
            }
            signalDuringTrip = null;
            roverDestination = target;
            roverRouteDirty = true;
            routeMarker.transform.position = CellPosition(target) + new Vector3(0f, 0f, -.35f);
            routeMarker.enabled = target != roverCell;
            SetReport(target == roverCell
                ? "El rover ya está en este sector. Elegí otra casilla, explorada u oculta, para continuar."
                : $"Rover en marcha al sector {Sector(target)}, a {SpaceFarmMap.CellDistance(roverCell, target)} casillas. El anillo cian marca el destino.");
            if (travel == null && target != roverCell) travel = StartCoroutine(TravelTo());
            RefreshHud();
        }

        private IEnumerator TravelTo()
        {
            if (scanTrail != null) scanTrail.Play();
            scanPulse.enabled = true;
            Vector2Int routeTarget = new Vector2Int(int.MinValue, int.MinValue);
            List<Vector2Int> path = new List<Vector2Int>();
            while (roverCell != roverDestination)
            {
                if (roverRouteDirty || routeTarget != roverDestination || path.Count == 0 || state.IsCellBlocked(path[0]))
                {
                    routeTarget = roverDestination;
                    roverRouteDirty = false;
                    if (!FindRoverPath(roverCell, routeTarget, out path))
                    {
                        // A colonist can temporarily stand in a narrow passage. Wait until it moves,
                        // but stop permanently when walls or structures remove every route.
                        if (FindRoverPath(roverCell, routeTarget, out _, false))
                        {
                            yield return null;
                            continue;
                        }
                        SetReport("No hay un camino libre hasta ese sector. Retirá un muro o una estructura que bloquee el paso.");
                        break;
                    }
                }
                if (path.Count == 0) break;
                Vector2Int nextCell = path[0];
                path.RemoveAt(0);
                Vector3 start = rover.transform.position;
                Vector3 end = CellPosition(nextCell) + new Vector3(0f, 0f, -.5f);
                Vector3 direction = (end - start).normalized;
                SetRoverFacing(direction);
                float duration = Vector3.Distance(start, end) / RoverSpeed;
                float elapsed = 0f;
                bool colonistBlocked = false;
                while (elapsed < duration)
                {
                    // Time.deltaTime is zero while paused: the trip freezes here and resumes untouched.
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    Vector3 nextPosition = Vector3.LerpUnclamped(start, end, progress);
                    if (IsRoverPositionBlocked(nextPosition))
                    {
                        colonistBlocked = true;
                        break;
                    }
                    rover.transform.position = nextPosition;
                    Vector2Int revealCenter = WorldToCell(rover.transform.position);
                    if (map.Inside(revealCenter) && revealCenter != lastRevealCenter)
                    {
                        lastRevealCenter = revealCenter;
                        if (Reveal(revealCenter, state.ScanRadius)) RefreshWorld();
                    }
                    yield return null;
                }
                if (colonistBlocked)
                {
                    // Return to the last safe cell before routing around the moving colonist.
                    while (Vector3.Distance(rover.transform.position, start) > .025f)
                    {
                        rover.transform.position = Vector3.MoveTowards(rover.transform.position, start,
                            RoverSpeed * Time.deltaTime);
                        yield return null;
                    }
                    rover.transform.position = start;
                    roverRouteDirty = true;
                    path.Clear();
                    continue;
                }
                rover.transform.position = end;
                roverCell = nextCell;
                if (Reveal(roverCell, state.ScanRadius)) RefreshWorld();
            }
            if (roverCell != roverDestination)
            {
                if (scanTrail != null) scanTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                routeMarker.enabled = false;
                travel = null;
                RefreshHud();
                yield break;
            }
            MapDeposit here = map.DepositAt(roverCell);
            int point = map.PointAt(roverCell);
            if (here != null && !state.Deposit(here.Id).Surveyed)
            {
                SetReport(state.SurveyDeposit(here.Id));
                if (!collectionActive && !barrierMode) SelectDeposit(here.Id);
            }
            else if (point >= 0 && !state.HasDiscovery(point))
                SetReport(state.ClaimDiscovery(point));
            else if (signalDuringTrip != null)
                SetReport($"El rover llegó al sector {Sector(roverCell)}. La señal {signalDuringTrip.Name} quedó visible al {SpaceFarmMap.Compass(roverCell, signalDuringTrip.Cell)}: enviá el rover hasta el marcador para estudiarla.");
            else SetReport($"El rover llegó al sector {Sector(roverCell)} y reveló terreno nuevo.");
            signalDuringTrip = null;
            if (scanTrail != null) scanTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            routeMarker.enabled = false;
            PlayWorldPing(rover.transform.position, SpaceFarmPalette.Ice);
            travel = null;
            RefreshWorld();
            RefreshHud();
        }

        private bool FindRoverPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path, bool avoidColonists = true)
        {
            path = new List<Vector2Int>();
            if (!map.Inside(start) || !map.Inside(goal) || (goal != start && state.IsCellBlocked(goal))) return false;
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
                    if (!map.Inside(next) || !state.CanRoverReach(next) || visited[next.x, next.y] || IsRoverPathCellBlocked(next, goal, avoidColonists)) continue;
                    if (direction.x != 0 && direction.y != 0 &&
                        (IsRoverPathCellBlocked(current + new Vector2Int(direction.x, 0), goal, avoidColonists) ||
                         IsRoverPathCellBlocked(current + new Vector2Int(0, direction.y), goal, avoidColonists))) continue;
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
            return path.Count > 0;
        }

        private bool IsRoverPathCellBlocked(Vector2Int cell, Vector2Int goal, bool avoidColonists)
        {
            if (state.IsCellBlocked(cell)) return true;
            if (!avoidColonists || cell == goal) return false;
            Vector3 center = CellPosition(cell);
            for (int i = 0; i < astronauts.Count; i++)
                if (astronauts[i].Root != null && Vector2.Distance(center, astronauts[i].Root.position) < .72f) return true;
            return false;
        }

        private bool IsRoverPositionBlocked(Vector3 position)
        {
            Vector2Int cell = WorldToCell(position);
            if (!map.Inside(cell) || state.IsCellBlocked(cell)) return true;
            for (int i = 0; i < astronauts.Count; i++)
                if (astronauts[i].Root != null && Vector2.Distance(position, astronauts[i].Root.position) < .72f) return true;
            return false;
        }

        private bool FindReachableCellNear(Vector2Int target, out Vector2Int result)
        {
            for (int radius = 1; radius <= 5; radius++)
            {
                int bestLength = int.MaxValue;
                Vector2Int best = default;
                for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                    Vector2Int candidate = target + new Vector2Int(x, y);
                    if (!map.Inside(candidate) || state.IsCellBlocked(candidate)) continue;
                    if (!FindRoverPath(roverCell, candidate, out List<Vector2Int> path, false) || path.Count >= bestLength) continue;
                    best = candidate;
                    bestLength = path.Count;
                }
                if (bestLength != int.MaxValue) { result = best; return true; }
            }
            result = default;
            return false;
        }

        private bool FindNearestWarehouseDockCell(out Vector2Int result)
        {
            int bestLength = int.MaxValue;
            Vector2Int best = default;
            for (int i = 0; i < state.WarehouseCells.Count; i++)
            {
                if (!FindReachableCellNear(state.WarehouseCells[i], out Vector2Int dock)) continue;
                if (!FindRoverPath(roverCell, dock, out List<Vector2Int> path, false) || path.Count >= bestLength) continue;
                best = dock;
                bestLength = path.Count;
            }
            result = best;
            return bestLength != int.MaxValue;
        }

        private void CreateScanTrail()
        {
            GameObject trailObject = new GameObject("Rover · polvo de exploración");
            trailObject.transform.SetParent(rover.transform, false);
            trailObject.transform.localPosition = new Vector3(0f, -.34f, .2f);
            scanTrail = trailObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = scanTrail.main;
            main.loop = true;
            main.startLifetime = .45f;
            main.startSpeed = .08f;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .09f);
            main.startColor = SpaceFarmPalette.Ice;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            ParticleSystem.EmissionModule emission = scanTrail.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = scanTrail.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .18f;
            ParticleSystem.ColorOverLifetimeModule color = scanTrail.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(SpaceFarmPalette.Ice, 0f), new GradientColorKey(SpaceFarmPalette.Iron, 1f) },
                new[] { new GradientAlphaKey(.55f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystemRenderer renderer = scanTrail.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 7;
            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = Color.white;
            renderer.sharedMaterial = material;
            runtimeAssets.Add(material);
            scanTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void AnimateRover()
        {
            roverIdleClock += Time.deltaTime;
            if (rover == null) return;
            UpdateRoverSprite();
            float movement = travel != null ? .018f : .008f;
            float pulse = 1f + Mathf.Sin(roverIdleClock * (travel != null ? 15f : 3f)) * movement;
            rover.transform.localScale = roverBaseScale * pulse;
            if (scanTrail != null)
                scanTrail.transform.localPosition = new Vector3(-roverHeading.x * .34f, -roverHeading.y * .34f, .2f);
            if (scanPulse != null)
            {
                scanPulse.transform.position = rover.transform.position + new Vector3(0f, 0f, -.12f);
                float size = travel != null ? .9f + Mathf.PingPong(roverIdleClock * 1.3f, .4f) : .74f;
                scanPulse.transform.localScale = new Vector3(size, size, 1f);
                scanPulse.enabled = travel != null;
            }
            if (routeMarker != null && routeMarker.enabled)
            {
                float size = .66f + Mathf.PingPong(roverIdleClock * .55f, .18f);
                routeMarker.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        private void SetRoverFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < .001f) return;
            roverHeading = direction.normalized;
            float angle = Mathf.Atan2(roverHeading.y, roverHeading.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt((90f - angle) / 45f);
            roverFacingIndex = ((index % 8) + 8) % 8;
            UpdateRoverSprite();
        }

        private void UpdateRoverSprite()
        {
            if (roverRenderer == null) return;
            if (roverDirectionalSprites == null || roverWheelSprites == null)
            {
                string[] facings = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
                roverDirectionalSprites = new Sprite[facings.Length];
                roverWheelSprites = new Sprite[facings.Length];
                Sprite[] bodies = Resources.LoadAll<Sprite>("Art/space-farm-rover-directions");
                Sprite[] wheels = Resources.LoadAll<Sprite>("Art/space-farm-rover-wheels");
                for (int facing = 0; facing < facings.Length; facing++)
                {
                    for (int i = 0; i < bodies.Length; i++)
                        if (bodies[i] != null && bodies[i].name == "rover-facing-" + facings[facing])
                            roverDirectionalSprites[facing] = bodies[i];
                    for (int i = 0; i < wheels.Length; i++)
                        if (wheels[i] != null && wheels[i].name == "rover-wheel-" + facings[facing])
                            roverWheelSprites[facing] = wheels[i];
                }
            }

            bool wheelTurned = travel != null && (Mathf.FloorToInt(roverIdleClock * 9f) & 1) == 1;
            Sprite target = wheelTurned ? roverWheelSprites[roverFacingIndex] : null;
            if (target == null) target = roverDirectionalSprites[roverFacingIndex];
            if (target != null && roverRenderer.sprite != target) roverRenderer.sprite = target;
        }

        // ---------- Camera ----------

        private void HandleCameraInput(Mouse mouse)
        {
            if (infoPanel != null && infoPanel.activeSelf)
            {
                if (!mouse.middleButton.isPressed && !mouse.rightButton.isPressed) isPanning = false;
                return;
            }
            bool screenChanged = Screen.width != lastScreenWidth || Screen.height != lastScreenHeight
                || Mathf.Abs(gameCamera.aspect - lastCameraAspect) > .01f;
            if (screenChanged)
            {
                lastCameraAspect = gameCamera.aspect;
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                ClampCamera();
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 pointer = mouse.position.ReadValue();
            bool panPressed = mouse.middleButton.wasPressedThisFrame ||
                (!suppressRightPan && mouse.rightButton.wasPressedThisFrame);
            bool panHeld = mouse.middleButton.isPressed || (!suppressRightPan && mouse.rightButton.isPressed);
            if (panPressed && !overUi)
            {
                isPanning = true;
                interactionMode = InteractionMode.PanningCamera;
                followRover = false;
                RefreshFollowButton();
                panWorldPosition = ScreenToWorld(pointer);
            }
            if (!panHeld)
            {
                isPanning = false;
                if (interactionMode == InteractionMode.PanningCamera && !leftDragging)
                    interactionMode = activeStructure != StructureKind.None ? InteractionMode.PlacingStructure : InteractionMode.Explore;
            }
            if (isPanning)
            {
                gameCamera.transform.position += panWorldPosition - ScreenToWorld(pointer);
                ClampCamera();
            }
            Vector2 scroll = mouse.scroll.ReadValue();
            if (!overUi && Mathf.Abs(scroll.y) > .01f)
            {
                Vector3 before = ScreenToWorld(pointer);
                // One wheel notch reports 120 on desktop and about 1 on some browsers.
                float notches = Mathf.Clamp(Mathf.Abs(scroll.y) > 20f ? scroll.y / 120f : scroll.y, -3f, 3f);
                gameCamera.orthographicSize = Mathf.Clamp(gameCamera.orthographicSize - notches * .45f,
                    MinCameraSize, MaximumCameraSize());
                Vector3 after = ScreenToWorld(pointer);
                gameCamera.transform.position += before - after;
                ClampCamera();
            }
        }

        private void UpdateFollow()
        {
            if (!followRover || rover == null || isPanning || leftDragging) return;
            Vector3 target = new Vector3(rover.transform.position.x, rover.transform.position.y, -10f);
            gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, target,
                1f - Mathf.Exp(-6f * Time.deltaTime));
            ClampCamera();
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            return gameCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -gameCamera.transform.position.z));
        }

        /// <summary>
        /// Keeps the view on the terrain. The camera may travel past the top and bottom edges only by the
        /// height the opaque HUD bars cover, so edge sectors are reachable and no empty space is visible.
        /// </summary>
        private void ClampCamera()
        {
            gameCamera.orthographicSize = Mathf.Clamp(gameCamera.orthographicSize,
                MinCameraSize, MaximumCameraSize());
            float halfHeight = gameCamera.orthographicSize;
            float halfWidth = halfHeight * gameCamera.aspect;
            float worldHalfWidth = Width * TileSize * .5f;
            float worldHalfHeight = Height * TileSize * SpaceFarmMap.TerrainPerspectiveYScale * .5f;
            float unitsPerPixel = 2f * halfHeight / Mathf.Max(1, Screen.height);
            float hudScale = Screen.width / 1280f;
            float top = HeaderHeight * hudScale * unitsPerPixel;
            float bottom = FooterHeight * hudScale * unitsPerPixel;
            Vector3 position = gameCamera.transform.position;
            position.x = worldHalfWidth <= halfWidth ? 0f : Mathf.Clamp(position.x, -worldHalfWidth + halfWidth, worldHalfWidth - halfWidth);
            position.y = Mathf.Clamp(position.y, -worldHalfHeight + halfHeight - bottom, worldHalfHeight - halfHeight + top);
            position.z = -10f;
            gameCamera.transform.position = position;
        }

        private float MaximumCameraSize() =>
            Mathf.Min(MaxCameraSize, Height * TileSize * SpaceFarmMap.TerrainPerspectiveYScale * .5f,
                Width * TileSize / (2f * Mathf.Max(.1f, gameCamera.aspect)));

        private void FocusWorld(Vector3 position)
        {
            gameCamera.transform.position = new Vector3(position.x, position.y, -10f);
            ClampCamera();
        }

        // ---------- Feedback ----------

        private void PlayWorldPing(Vector3 position, Color color)
        {
            if (signalPing == null) return;
            if (worldPing != null) StopCoroutine(worldPing);
            worldPing = StartCoroutine(WorldPing(position, color));
        }

        private IEnumerator WorldPing(Vector3 position, Color color)
        {
            signalPing.transform.position = position + new Vector3(0f, 0f, -.4f);
            signalPing.color = color;
            signalPing.enabled = true;
            const float duration = .62f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                float size = Mathf.Lerp(.55f, 2.05f, eased);
                signalPing.transform.localScale = new Vector3(size, size, 1f);
                Color tint = color;
                tint.a = 1f - t;
                signalPing.color = tint;
                yield return null;
            }
            signalPing.enabled = false;
            worldPing = null;
        }

        private IEnumerator AnimateButton(Transform target)
        {
            if (target == null) yield break;
            Vector3 baseline = Vector3.one;
            float elapsed = 0f;
            while (elapsed < .18f)
            {
                if (target == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / .18f);
                float bump = 1f + Mathf.Sin(t * Mathf.PI) * .055f;
                target.localScale = baseline * bump;
                yield return null;
            }
            if (target != null) target.localScale = baseline;
        }

        private void AnimatePop(Transform target, Vector3 finalScale)
        {
            if (target == null) return;
            StartCoroutine(Pop(target, finalScale));
        }

        private IEnumerator Pop(Transform target, Vector3 finalScale)
        {
            Vector3 overshoot = finalScale * 1.12f;
            target.localScale = finalScale * .82f;
            float elapsed = 0f;
            while (elapsed < .11f)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(finalScale * .82f, overshoot, Mathf.Clamp01(elapsed / .11f));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < .13f)
            {
                elapsed += Time.deltaTime;
                target.localScale = Vector3.Lerp(overshoot, finalScale, Mathf.Clamp01(elapsed / .13f));
                yield return null;
            }
            target.localScale = finalScale;
        }

        private void FlashScreen(Color color, float alpha, float duration)
        {
            if (screenFlash == null) return;
            if (screenFlashRoutine != null) StopCoroutine(screenFlashRoutine);
            screenFlashRoutine = StartCoroutine(ScreenFlash(color, alpha, duration));
        }

        private IEnumerator ScreenFlash(Color color, float alpha, float duration)
        {
            float half = Mathf.Max(.05f, duration * .5f);
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                color.a = alpha * (t / half);
                screenFlash.color = color;
                yield return null;
            }
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                color.a = alpha * (1f - t / half);
                screenFlash.color = color;
                yield return null;
            }
            color.a = 0f;
            screenFlash.color = color;
            screenFlashRoutine = null;
        }

        // ---------- Colony actions ----------

        private delegate bool GameAction(out string message);

        private bool Act(GameAction action)
        {
            if (state.GameOver)
            {
                SetReport("La misión terminó. Abrí PAUSA (Esc) para reiniciar este mapa o empezar uno nuevo.");
                return false;
            }
            bool success = action(out string message);
            SetReport(message);
            if (success) FlashScreen(SpaceFarmPalette.Oxygen, .10f, .22f);
            RefreshWorld();
            RefreshHud();
            return success;
        }

        private void AdvanceSol()
        {
            if (paused || arrivalSequenceActive) return;
            if (travel != null || collectionActive)
            {
                SetReport("Esperá a que el rover termine su recorrido antes de avanzar el sol.");
                return;
            }
            if (state.GameOver)
            {
                SetReport(state.AdvanceSol());
                return;
            }
            state.AdvanceSol();
            SetReport(SummarizeSol());
            if (state.LastSol.ShipmentDelivered) QueueShipmentArrival();
            if (state.LastSol.Storm) FlashScreen(SpaceFarmPalette.Iron, .18f, .32f);
            if (state.LastSol.RaidFoodLoss > 0) FlashScreen(SpaceFarmPalette.Danger, .22f, .38f);
            RefreshWorld();
            RefreshHud();
        }

        private void SetReport(string value)
        {
            if (reportText != null) reportText.text = value;
        }

        private string NextObjective()
        {
            if (state == null || map == null) return "Preparando la carta orbital…";
            if (state.DamagedStructure != DamagedStructureKind.None)
                return $"Repará {state.DamagedStructureName(state.DamagedStructure, state.DamagedTarget)} ({state.RepairIronCost} Fe y {state.RepairEnergyCost} energía).";
            if (state.RoverCargo > 0 && state.Iron >= state.WarehouseCapacity)
                return "El almacén está lleno. Construí otro y enviá el rover a descargar.";
            MapDeposit iron = map.Nearest(DepositKind.Iron);
            MapDeposit ice = map.Nearest(DepositKind.Ice);
            if (iron == null || ice == null) return "Explorá la región para localizar hierro y hielo.";
            if (!state.AnySurveyed(DepositKind.Iron))
                return $"La carta orbital indica hierro al {SpaceFarmMap.Compass(BaseCell, iron.Cell)}, a {iron.Distance} sectores. Enviá el rover y estudialo.";
            if (state.MineCount == 0) return $"Seleccioná el yacimiento estudiado y construí la mina ({state.MineCost} de hierro).";
            if (!state.AnySurveyed(DepositKind.Ice))
                return $"Hay hielo al {SpaceFarmMap.Compass(BaseCell, ice.Cell)}, a {ice.Distance} sectores. Enviá el rover a localizarlo.";
            if (state.ExtractorCount == 0) return $"Construí un extractor para asegurar agua ({state.ExtractorCost} de hierro).";
            if (!state.Greenhouse) return "Construí un invernadero para iniciar la cosecha (6 de hierro).";
            if (state.ColonyLevel < 3) return $"Explorá y construí para ganar XP y alcanzar el nivel 3 ({state.Xp}/{state.XpToNext}).";
            if (state.Sol < 7) return $"Mantené la colonia hasta el sol 7 (ahora {state.Sol}).";
            return "Revisá recursos y explorá yacimientos lejanos para crecer.";
        }
    }
}

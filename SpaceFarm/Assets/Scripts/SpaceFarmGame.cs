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
    /// <summary>First playable Space Farm loop: discover, build and survive.</summary>
    public sealed partial class SpaceFarmGame : MonoBehaviour
    {
        private const int Width = 30;
        private const int Height = 20;
        private const float TileSize = 0.9f;
        private const int FogPixelsPerCell = 8;
        private const float RoverSpeed = 5.2f;
        private const float MinCameraSize = 2.6f;
        private const float MaxCameraSize = 8.2f;
        private static readonly Vector2Int BaseCell = new Vector2Int(15, 10);
        private static readonly Vector2Int IronCell = new Vector2Int(19, 12);
        private static readonly Vector2Int IceCell = new Vector2Int(9, 13);

        private readonly bool[,] revealed = new bool[Width, Height];
        private readonly float[,] revealAmount = new float[Width, Height];
        private readonly SpriteRenderer[,] markers = new SpriteRenderer[Width, Height];
        private Texture2D fogTexture;
        private bool fogDirty = true;
        private bool fogAnimating;
        private SpaceFarmState state;
        private Camera gameCamera;
        private GameObject rover;
        private GameObject greenhouse;
        private GameObject waterExtractor;
        private GameObject oxygenPlantTwo;
        private GameObject mine;
        private Vector3 mineBaseScale;
        private Coroutine travel;
        private Coroutine worldPing;
        private Coroutine screenFlashRoutine;
        private Vector2Int roverDestination = BaseCell;
        private Vector2Int roverCell = BaseCell;
        private Vector2Int? lastRevealedDeposit;
        private bool isPanning;
        private bool cameraWasManuallyZoomed;
        private Vector3 panWorldPosition;
        private float lastCameraAspect;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private float roverIdleClock;
        private Vector2Int lastRevealCenter = new Vector2Int(-1, -1);
        private Vector3 roverBaseScale;
        private float fogRefreshTimer;
        private TextMeshProUGUI statsText;
        private TextMeshProUGUI objectiveText;
        private TextMeshProUGUI reportText;
        private TextMeshProUGUI roverText;
        private TextMeshProUGUI actionDetailText;
        private RectTransform actionCardsRoot;
        private readonly System.Collections.Generic.List<ActionCard> actionCards = new System.Collections.Generic.List<ActionCard>();
        private readonly System.Collections.Generic.List<UnityEngine.UI.Button> categoryButtons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
        private int selectedCategory;
        private Image screenFlash;
        private Sprite squareSprite;
        private Sprite circleSprite;
        private Sprite ringSprite;
        private SpriteRenderer routeMarker;
        private SpriteRenderer scanPulse;
        private SpriteRenderer signalPing;
        private ParticleSystem scanTrail;
        private Material scanParticleMaterial;
        private GameObject worldRoot;
        private bool previousMineDamaged;
        private bool previousGreenhouse;
        private bool previousWaterExtractor;
        private int previousMineLevel;
        private int previousRoverLevel = 1;
        private int previousOxygenPlants;

        private void Start()
        {
            state = new SpaceFarmState();
            ConfigureCamera();
            CreateSprites();
            CreateWorld();
            CreateHud();
            InitializeSystems();
            // The lander arrives with a partial orbital chart; the rover reveals the rest.
            Reveal(BaseCell, 5);
            RefreshWorld();
            RefreshHud();
            SetReport(state.LastReport);
            roverBaseScale = rover.transform.localScale;
            RefreshSystems();
        }

        private void OnDestroy()
        {
            if (scanParticleMaterial != null) Destroy(scanParticleMaterial);
            if (fogTexture != null) Destroy(fogTexture);
            if (squareSprite != null) Destroy(squareSprite);
            if (circleSprite != null) Destroy(circleSprite);
            if (ringSprite != null) Destroy(ringSprite);
            DisposeSystems();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            UpdateFogReveal();
            AnimateRover();
            UpdateSystems(mouse);
            if (mouse == null) return;
            HandleCameraInput(mouse);

            if (!mouse.leftButton.wasPressedThisFrame || state.GameOver) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Vector3 world = ScreenToWorld(mouse.position.ReadValue());
            if (TryHandleWorldClick(world)) return;
            Vector2Int target = new Vector2Int(
                Mathf.RoundToInt(world.x / TileSize + (Width - 1) / 2f),
                Mathf.RoundToInt(world.y / TileSize + (Height - 1) / 2f));
            if (!Inside(target)) return;
            SendRoverTo(target);
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
            float aspect = Mathf.Max(.1f, gameCamera.aspect);
            gameCamera.orthographicSize = Mathf.Clamp((Width * TileSize) / (3f * aspect), 4.3f, 5.6f);
            gameCamera.transform.position = new Vector3(0f, 0f, -10f);
            gameCamera.backgroundColor = SpaceFarmPalette.Background;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            lastCameraAspect = gameCamera.aspect;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
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
        }

        private void CreateWorld()
        {
            worldRoot = new GameObject("Mars Grid");
            worldRoot.transform.SetParent(transform);
            CreateTerrainTiles();
            fogTexture = new Texture2D(Width * FogPixelsPerCell, Height * FogPixelsPerCell,
                TextureFormat.RGBA32, false);
            fogTexture.filterMode = FilterMode.Bilinear;
            Sprite fogSprite = Sprite.Create(fogTexture,
                new Rect(0, 0, fogTexture.width, fogTexture.height), new Vector2(.5f, .5f),
                FogPixelsPerCell / TileSize);
            CreateWorldSprite("Niebla de guerra", fogSprite, Vector3.zero, Vector3.one, Color.white, 0,
                worldRoot.transform);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == IronCell || cell == IceCell)
                {
                    markers[x, y] = CreateArtwork(cell == IronCell ? "Yacimiento de hierro" : "Depósito de hielo",
                        cell == IronCell ? "Art/iron-deposit" : "Art/ice-deposit",
                        CellPosition(cell) + new Vector3(0, 0, -.1f), 1.55f, 1.03f, 3, worldRoot.transform);
                    markers[x, y].enabled = false;
                }
            }

            CreateArtwork("Nave y hábitat", "Art/lander", CellPosition(BaseCell) + new Vector3(0, 0, -.2f),
                3.1f, 2.07f, 5, worldRoot.transform);
            CreateArtwork("Planta de oxígeno", "Art/oxygen-plant",
                CellPosition(BaseCell + new Vector2Int(2, 0)) + new Vector3(0, 0, -.2f),
                1.7f, 1.14f, 6, worldRoot.transform);
            rover = CreateArtwork("Rover 1", "Art/rover", CellPosition(roverCell) + new Vector3(-.65f, -.5f, -.5f),
                1.55f, 1.04f, 8, worldRoot.transform).gameObject;
            greenhouse = CreateBuilding("Invernadero", "Art/greenhouse", BaseCell + new Vector2Int(-2, 1), worldRoot.transform);
            waterExtractor = CreateBuilding("Extractor de hielo", "Art/water-extractor", IceCell, worldRoot.transform);
            mine = CreateBuilding("Mina de hierro", "Art/iron-mine", IronCell, worldRoot.transform);
            mineBaseScale = mine.transform.localScale;
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
            waterExtractor.SetActive(false);
            mine.SetActive(false);
            oxygenPlantTwo.SetActive(false);
        }

        private GameObject CreateBuilding(string name, string artPath, Vector2Int cell, Transform parent)
        {
            return CreateArtwork(name, artPath, CellPosition(cell) + new Vector3(0, 0, -.2f),
                1.75f, 1.17f, 6, parent).gameObject;
        }

        private SpriteRenderer CreateArtwork(string name, string path, Vector3 position, float width, float height,
            int order, Transform parent)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
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
                (cell.y - (Height - 1) / 2f) * TileSize, 0f);
        }

        private void Reveal(Vector2Int center, int radius)
        {
            bool ironWasVisible = revealed[IronCell.x, IronCell.y];
            bool iceWasVisible = revealed[IceCell.x, IceCell.y];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int dx = x - center.x;
                int dy = y - center.y;
                if (dx * dx + dy * dy <= radius * radius && !revealed[x, y])
                {
                    revealed[x, y] = true;
                    revealAmount[x, y] = 0f;
                    fogAnimating = true;
                    fogDirty = true;
                }
            }
            if (!ironWasVisible && revealed[IronCell.x, IronCell.y])
            {
                lastRevealedDeposit = IronCell;
                SetReport("Señal de hierro detectada. Enviá el rover al noreste para estudiar el yacimiento.");
                PlayWorldPing(CellPosition(IronCell), SpaceFarmPalette.Iron);
            }
            if (!iceWasVisible && revealed[IceCell.x, IceCell.y])
            {
                lastRevealedDeposit = IceCell;
                SetReport("Señal de hielo detectada. Enviá el rover al noroeste para estudiar el depósito.");
                PlayWorldPing(CellPosition(IceCell), SpaceFarmPalette.Ice);
            }
        }

        private void UpdateFogReveal()
        {
            if (!fogAnimating) return;
            bool changed = false;
            bool stillAnimating = false;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!revealed[x, y] || revealAmount[x, y] >= 1f) continue;
                revealAmount[x, y] = Mathf.MoveTowards(revealAmount[x, y], 1f, Time.deltaTime * 3.4f);
                changed = true;
                stillAnimating |= revealAmount[x, y] < 1f;
            }
            fogAnimating = stillAnimating;
            if (changed) fogDirty = true;
            fogRefreshTimer += Time.deltaTime;
            if (fogDirty && fogRefreshTimer >= .045f)
            {
                fogRefreshTimer = 0f;
                UpdateFogTexture();
            }
        }

        private void UpdateFogTexture()
        {
            int pixelWidth = fogTexture.width;
            int pixelHeight = fogTexture.height;
            Color32[] pixels = new Color32[pixelWidth * pixelHeight];
            Color32 fogColor = SpaceFarmPalette.FogOverlay;
            for (int py = 0; py < pixelHeight; py++)
            for (int px = 0; px < pixelWidth; px++)
            {
                float gridX = (px + .5f) / FogPixelsPerCell - .5f;
                float gridY = (py + .5f) / FogPixelsPerCell - .5f;
                int cellX = Mathf.Clamp(px / FogPixelsPerCell, 0, Width - 1);
                int cellY = Mathf.Clamp(py / FogPixelsPerCell, 0, Height - 1);
                float visibility = 0f;
                for (int y = Mathf.Max(0, cellY - 2); y <= Mathf.Min(Height - 1, cellY + 2); y++)
                for (int x = Mathf.Max(0, cellX - 2); x <= Mathf.Min(Width - 1, cellX + 2); x++)
                {
                    if (!revealed[x, y]) continue;
                    float dx = Mathf.Max(0f, Mathf.Abs(gridX - x) - .5f);
                    float dy = Mathf.Max(0f, Mathf.Abs(gridY - y) - .5f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float softEdge = 1f - Mathf.SmoothStep(0f, .92f, distance);
                    visibility = Mathf.Max(visibility, revealAmount[x, y] * softEdge);
                }
                float opacity = 1f - visibility;
                fogColor.a = (byte)(SpaceFarmPalette.FogOverlay.a * 255f * opacity);
                pixels[py * pixelWidth + px] = fogColor;
            }
            fogTexture.SetPixels32(pixels);
            fogTexture.Apply(false);
            fogDirty = false;
        }

        private void SendRoverTo(Vector2Int target)
        {
            if (collectionActive)
            {
                SetReport("El rover está recogiendo hierro. Esperá a que descargue en el hábitat.");
                return;
            }
            if (target != IronCell && target != IceCell) lastRevealedDeposit = null;
            roverDestination = target;
            routeMarker.transform.position = CellPosition(target) + new Vector3(0f, 0f, -.35f);
            routeMarker.enabled = target != roverCell;
            roverText.text = target == roverCell
                ? $"Rover listo · sector {target.x + 1},{target.y + 1}"
                : $"Rover en ruta · sector {target.x + 1},{target.y + 1}";
            SetReport(target == roverCell
                ? "El rover ya está en este sector. Elegí una casilla explorada u oculta para continuar."
                : "Rover en marcha. El anillo cian marca el destino; los sectores nuevos se revelan al avanzar.");
            if (travel == null && target != roverCell) travel = StartCoroutine(TravelTo());
        }

        private IEnumerator TravelTo()
        {
            if (scanTrail != null) scanTrail.Play();
            scanPulse.enabled = true;
            while (roverCell != roverDestination)
            {
                int x = roverCell.x;
                int y = roverCell.y;
                Vector2Int target = roverDestination;
                if (x != target.x) x += Math.Sign(target.x - x);
                else y += Math.Sign(target.y - y);
                Vector2Int nextCell = new Vector2Int(x, y);
                Vector3 start = rover.transform.position;
                Vector3 end = CellPosition(nextCell) + new Vector3(0f, 0f, -.5f);
                Vector3 direction = (end - start).normalized;
                float desiredAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                float duration = Vector3.Distance(start, end) / RoverSpeed;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    // Constant interpolation keeps the rover moving continuously across tile boundaries.
                    rover.transform.position = Vector3.LerpUnclamped(start, end, progress);
                    Quaternion rotation = Quaternion.Euler(0f, 0f, desiredAngle);
                    rover.transform.rotation = Quaternion.RotateTowards(rover.transform.rotation, rotation, 720f * Time.deltaTime);
                    Vector2Int revealCenter = WorldToCell(rover.transform.position);
                    if (Inside(revealCenter) && revealCenter != lastRevealCenter)
                    {
                        lastRevealCenter = revealCenter;
                        Reveal(revealCenter, state.ScanRadius);
                        RefreshWorld();
                    }
                    yield return null;
                }
                rover.transform.position = end;
                roverCell = nextCell;
                Reveal(roverCell, state.ScanRadius);
                RefreshWorld();
            }
            if (roverCell == IronCell)
            {
                SetReport(state.SurveyIron());
                lastRevealedDeposit = null;
            }
            else if (roverCell == IceCell)
            {
                SetReport(state.SurveyIce());
                lastRevealedDeposit = null;
            }
            else if (lastRevealedDeposit.HasValue)
            {
                string depositName = lastRevealedDeposit.Value == IronCell ? "Hierro" : "Hielo";
                SetReport($"El rover llegó al sector {roverCell.x + 1},{roverCell.y + 1}. {depositName} quedó visible: enviá el rover hasta el marcador para estudiarlo.");
            }
            else SetReport(DiscoverPointOfInterest(roverCell) ??
                $"El rover llegó al sector {roverCell.x + 1},{roverCell.y + 1} y reveló terreno nuevo.");
            if (scanTrail != null) scanTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            routeMarker.enabled = false;
            roverText.text = $"Rover nivel {state.RoverLevel} · alcance {state.ScanRadius} sectores · listo";
            PlayWorldPing(rover.transform.position, SpaceFarmPalette.Ice);
            RefreshWorld();
            RefreshHud();
            travel = null;
        }

        private Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(Mathf.RoundToInt(world.x / TileSize + (Width - 1) / 2f),
                Mathf.RoundToInt(world.y / TileSize + (Height - 1) / 2f));
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
            scanParticleMaterial = new Material(Shader.Find("Sprites/Default"));
            scanParticleMaterial.color = Color.white;
            renderer.sharedMaterial = scanParticleMaterial;
            scanTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private bool Inside(Vector2Int cell) => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        private void RefreshWorld()
        {
            if (fogDirty) UpdateFogTexture();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                bool visible = revealed[x, y];
                if (markers[x, y] != null)
                    markers[x, y].enabled = visible && (x != IronCell.x || y != IronCell.y || state.MineLevel == 0)
                        && (x != IceCell.x || y != IceCell.y || !state.WaterExtractor);
            }
            bool mineActive = state.MineLevel > 0;
            bool mineLevelChanged = state.MineLevel != previousMineLevel;
            mine.SetActive(mineActive);
            mine.transform.localScale = mineBaseScale * (state.MineLevel >= 2 ? 1.16f : 1f);
            mine.GetComponent<SpriteRenderer>().color = state.MineDamaged ? SpaceFarmPalette.DamagedTint : Color.white;
            if (mineActive && mineLevelChanged) AnimatePop(mine.transform, mine.transform.localScale);
            if (state.MineDamaged && !previousMineDamaged) FlashScreen(SpaceFarmPalette.Danger, .13f, .28f);

            bool roverUpgraded = state.RoverLevel != previousRoverLevel;
            if (roverUpgraded)
            {
                roverBaseScale *= 1.12f;
                PlayWorldPing(rover.transform.position, SpaceFarmPalette.Ice);
            }

            if (state.Greenhouse && !previousGreenhouse) AnimatePop(greenhouse.transform, greenhouse.transform.localScale);
            if (state.WaterExtractor && !previousWaterExtractor) AnimatePop(waterExtractor.transform, waterExtractor.transform.localScale);
            if (state.OxygenPlants > previousOxygenPlants) AnimatePop(oxygenPlantTwo.transform, oxygenPlantTwo.transform.localScale);

            greenhouse.SetActive(state.Greenhouse);
            waterExtractor.SetActive(state.WaterExtractor);
            oxygenPlantTwo.SetActive(state.OxygenPlants > 1);
            previousGreenhouse = state.Greenhouse;
            previousWaterExtractor = state.WaterExtractor;
            previousMineLevel = state.MineLevel;
            previousMineDamaged = state.MineDamaged;
            previousRoverLevel = state.RoverLevel;
            previousOxygenPlants = state.OxygenPlants;
            RefreshSystems();
        }

        private void CreateHud()
        {
            GameObject canvasObject = new GameObject("Space Farm HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0f;

            if (EventSystem.current == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(canvasObject.transform);
            }

            GameObject header = Panel("Header", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 96));
            Label("SPACE FARM", header.transform, 26, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -12), new Vector2(205, 35));
            Label("MARTE · SUPERVIVENCIA", header.transform, 12, FontStyles.Bold, SpaceFarmPalette.MutedText,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -45), new Vector2(205, 24));
            objectiveText = Label("", header.transform, 16, FontStyles.Bold, SpaceFarmPalette.Oxygen,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(230, -10), new Vector2(470, 48));
            objectiveText.enableAutoSizing = true;
            objectiveText.fontSizeMin = 12;
            objectiveText.fontSizeMax = 16;
            roverText = Label("", header.transform, 13, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-176, -12), new Vector2(160, 48));
            statsText = Label("", header.transform, 15, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                Vector2.zero, new Vector2(78, 28));
            statsText.enableAutoSizing = true;
            statsText.fontSizeMin = 11;
            statsText.fontSizeMax = 15;
            CreateResourceHud(header.transform);
            Panel("Header Accent", header.transform, SpaceFarmPalette.Iron,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, 3));

            AddButton("AVANZAR SOL", header.transform, AdvanceSol, 15, SpaceFarmPalette.Iron,
                SpaceFarmPalette.Background, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-14, -17), new Vector2(148, 48));

            GameObject footer = Panel("Command Deck", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, 164));
            reportText = Label("", footer.transform, 14, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                Vector2.zero, new Vector2(-36, 28));
            reportText.enableAutoSizing = true;
            reportText.fontSizeMin = 11;
            reportText.fontSizeMax = 14;
            CreateCategoryTabs(footer.transform);
            actionDetailText = Label("Elegí una acción. Las tarjetas indican costo y requisito.", footer.transform,
                12, FontStyles.Normal, SpaceFarmPalette.MutedText,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(354, -40), new Vector2(500, 28));
            GameObject cards = new GameObject("Action Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cards.transform.SetParent(footer.transform, false);
            actionCardsRoot = cards.GetComponent<RectTransform>();
            actionCardsRoot.anchorMin = new Vector2(0, 0);
            actionCardsRoot.anchorMax = new Vector2(1, 0);
            actionCardsRoot.pivot = new Vector2(.5f, 0);
            actionCardsRoot.anchoredPosition = new Vector2(0, 12);
            actionCardsRoot.sizeDelta = new Vector2(-32, 66);
            HorizontalLayoutGroup cardLayout = cards.GetComponent<HorizontalLayoutGroup>();
            cardLayout.padding = new RectOffset(16, 16, 0, 0);
            cardLayout.spacing = 8;
            cardLayout.childAlignment = TextAnchor.MiddleCenter;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = true;

            AddAction("Mina de hierro", "8 hierro", 0, () => state.BuildMine(out _),
                () => !state.IronSurveyed ? "Estudiá el yacimiento de hierro" : state.MineLevel > 0 ? "La mina ya está construida" : MissingIron(8));
            AddAction("Extractor de agua", "6 hierro", 0, () => state.BuildWaterExtractor(out _),
                () => !state.IceSurveyed ? "Estudiá el depósito de hielo" : state.WaterExtractor ? "Ya hay un extractor" : MissingIron(6));
            AddAction("Invernadero", "6 hierro", 0, () => state.BuildGreenhouse(out _),
                () => state.Greenhouse ? "El invernadero ya está construido" : MissingIron(6));
            AddAction("Planta de O2", "9 hierro", 0, () => state.BuildOxygenPlant(out _),
                () => state.OxygenPlants >= 2 ? "La demo admite 2 plantas de oxígeno" : MissingIron(9));
            AddAction("Construir barrera", "1 Fe/segmento", 0, () => false,
                () => state.BarrierCount == SpaceFarmState.BarrierSlotCount ? "Perímetro completo" : MissingIron(1));
            AddAction("Mejorar mina", "6 hierro", 1, () => state.UpgradeMine(out _),
                () => state.MineLevel == 0 ? "Construí primero la mina" : state.MineDamaged ? "Repará la mina primero" : state.MineLevel >= 2 ? "Nivel máximo de la demo" : MissingIron(6));
            AddAction("Reparar mina", "3 hierro", 1, () => state.RepairMine(out _),
                () => !state.MineDamaged ? "La mina no necesita reparación" : MissingIron(3));
            AddAction("Mejorar rover", "5 hierro", 1, () => state.UpgradeRover(out _),
                () => state.RoverLevel >= 2 ? "Nivel máximo de la demo" : MissingIron(5));
            AddAction("Recibir colono", "reservas vitales", 2, () => state.AddColonist(out _),
                () => state.Population >= 4 ? "El hábitat está completo" : state.OxygenPerSol < (state.Population + 1) * 1.25f ? "Aumentá O2 y conservá un 25 % de reserva" : state.Oxygen < 8 || state.Water < 8 || state.Food < 8 ? "Necesitás al menos 8 de O2, agua y comida" : string.Empty);
            AddAction("Pedir a la Tierra", "4 hierro", 2, () => state.OrderFromEarth(out _),
                () => state.ShipmentArrivalSol != 0 ? "Ya hay un pedido en camino" : MissingIron(4));

            GameObject flashObject = new GameObject("Event Flash", typeof(RectTransform), typeof(Image));
            flashObject.transform.SetParent(canvasObject.transform, false);
            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            screenFlash = flashObject.GetComponent<Image>();
            screenFlash.color = new Color(1f, 1f, 1f, 0f);
            screenFlash.raycastTarget = false;
            flashObject.transform.SetAsLastSibling();
            RefreshActionCards();
            SelectCategory(0);
        }

        private GameObject Panel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private TextMeshProUGUI Label(string value, Transform parent, float size, FontStyles style, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 dimensions)
        {
            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(parent, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private void CreateCategoryTabs(Transform parent)
        {
            string[] captions = { "CONSTRUIR", "MEJORAR", "COLONIA" };
            for (int i = 0; i < captions.Length; i++)
            {
                int category = i;
                UnityEngine.UI.Button button = AddButton(captions[i], parent, () => SelectCategory(category),
                    13, SpaceFarmPalette.Button, SpaceFarmPalette.Text,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(18 + i * 108, -40), new Vector2(100, 30));
                button.transition = Selectable.Transition.None;
                categoryButtons.Add(button);
            }
        }

        private void AddAction(string title, string cost, int category, Func<bool> action, Func<string> lockReason)
        {
            GameObject item = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            item.transform.SetParent(actionCardsRoot, false);
            item.GetComponent<LayoutElement>().flexibleWidth = 1f;
            item.GetComponent<LayoutElement>().minWidth = 112f;
            Image image = item.GetComponent<Image>();
            image.color = SpaceFarmPalette.Button;
            Button button = item.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            ColorBlock colors = button.colors;
            colors.normalColor = SpaceFarmPalette.Button;
            colors.highlightedColor = SpaceFarmPalette.ButtonHover;
            colors.pressedColor = SpaceFarmPalette.Ice;
            colors.selectedColor = SpaceFarmPalette.ButtonHover;
            colors.disabledColor = SpaceFarmPalette.PanelRaised;
            button.colors = colors;

            TextMeshProUGUI titleText = Label(title, item.transform, 15, FontStyles.Bold,
                SpaceFarmPalette.Text, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1),
                new Vector2(0, -7), new Vector2(-12, 26));
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 12;
            titleText.fontSizeMax = 15;
            TextMeshProUGUI detailText = Label(cost, item.transform, 11, FontStyles.Normal,
                SpaceFarmPalette.MutedText, Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                new Vector2(0, -13), new Vector2(-10, -26));
            detailText.alignment = TextAlignmentOptions.Center;
            detailText.textWrappingMode = TextWrappingModes.Normal;
            detailText.enableAutoSizing = true;
            detailText.fontSizeMin = 9;
            detailText.fontSizeMax = 11;

            ActionCard card = new ActionCard(category, item.transform, image, button, titleText, detailText,
                cost, action, lockReason);
            actionCards.Add(card);
            button.onClick.AddListener(() =>
            {
                StartCoroutine(AnimateButton(card.Root));
                if (state.GameOver)
                {
                    SetReport("La misión terminó. Reiniciá la escena para volver a intentarlo.");
                    return;
                }
                string reason = card.LockReason();
                if (!string.IsNullOrEmpty(reason))
                {
                    SetReport(reason + ". Costo: " + card.Cost + ".");
                    return;
                }
                if (card.Title.text == "Construir barrera")
                {
                    BeginBarrierMode();
                    return;
                }
                bool success = card.Action();
                if (!success) SetReport(state.LastReport);
                else
                {
                    SetReport(state.LastReport);
                    FlashScreen(SpaceFarmPalette.Oxygen, .10f, .22f);
                }
                RefreshWorld();
                RefreshHud();
            });
        }

        private Button AddButton(string caption, Transform parent, Action callback, float fontSize,
            Color normal, Color textColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 dimensions)
        {
            GameObject item = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Image image = item.GetComponent<Image>();
            image.color = normal;
            Button button = item.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = SpaceFarmPalette.ButtonHover;
            colors.pressedColor = SpaceFarmPalette.Ice;
            colors.selectedColor = SpaceFarmPalette.ButtonHover;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                StartCoroutine(AnimateButton(item.transform));
                callback();
            });
            TextMeshProUGUI text = Label(caption, item.transform, fontSize, FontStyles.Bold, textColor,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-12, -4));
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        private void SelectCategory(int category)
        {
            selectedCategory = category;
            string[] hints =
            {
                "Clic en el mapa: enviar rover · rueda: zoom · botón central: desplazar.",
                "Mejorá la mina y el rover; repará instalaciones dañadas.",
                "Admití colonos solo con reservas, o pedí suministros a la Tierra."
            };
            actionDetailText.text = hints[Mathf.Clamp(category, 0, hints.Length - 1)];
            for (int i = 0; i < categoryButtons.Count; i++)
            {
                Image image = categoryButtons[i].GetComponent<Image>();
                image.color = i == category ? SpaceFarmPalette.Iron : SpaceFarmPalette.PanelRaised;
                TextMeshProUGUI label = categoryButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.color = i == category ? SpaceFarmPalette.Background : SpaceFarmPalette.Text;
            }
            RefreshActionCards();
        }

        private void RefreshActionCards()
        {
            foreach (ActionCard card in actionCards)
            {
                bool visible = card.Category == selectedCategory;
                card.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                string reason = card.LockReason();
                bool available = string.IsNullOrEmpty(reason);
                card.Background.color = available ? SpaceFarmPalette.Button : SpaceFarmPalette.PanelRaised;
                card.Title.color = available ? SpaceFarmPalette.Text : SpaceFarmPalette.MutedText;
                card.Detail.color = available ? SpaceFarmPalette.Ice : SpaceFarmPalette.MutedText;
                card.Detail.text = available ? card.Cost : card.Cost + " · " + reason;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionCardsRoot);
        }

        private string MissingIron(int amount) => state.Iron < amount ? $"Faltan {amount - state.Iron} de hierro" : string.Empty;

        private void HandleCameraInput(Mouse mouse)
        {
            bool screenChanged = Screen.width != lastScreenWidth || Screen.height != lastScreenHeight
                || Mathf.Abs(gameCamera.aspect - lastCameraAspect) > .01f;
            if (screenChanged)
            {
                if (!cameraWasManuallyZoomed)
                    gameCamera.orthographicSize = Mathf.Clamp((Width * TileSize) / (3f * Mathf.Max(.1f, gameCamera.aspect)),
                        4.3f, 5.6f);
                lastCameraAspect = gameCamera.aspect;
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                ClampCamera();
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.middleButton.wasPressedThisFrame && !overUi)
            {
                isPanning = true;
                panWorldPosition = ScreenToWorld(pointer);
            }
            if (mouse.middleButton.wasReleasedThisFrame) isPanning = false;
            if (isPanning && mouse.middleButton.isPressed)
            {
                Vector3 current = ScreenToWorld(pointer);
                gameCamera.transform.position += panWorldPosition - current;
                ClampCamera();
            }
            Vector2 scroll = mouse.scroll.ReadValue();
            if (!overUi && Mathf.Abs(scroll.y) > .01f)
            {
                Vector3 before = ScreenToWorld(pointer);
                gameCamera.orthographicSize = Mathf.Clamp(gameCamera.orthographicSize - scroll.y * .0035f,
                    MinCameraSize, MaximumCameraSize());
                cameraWasManuallyZoomed = true;
                Vector3 after = ScreenToWorld(pointer);
                gameCamera.transform.position += before - after;
                ClampCamera();
            }
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            return gameCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -gameCamera.transform.position.z));
        }

        private void ClampCamera()
        {
            gameCamera.orthographicSize = Mathf.Clamp(gameCamera.orthographicSize,
                MinCameraSize, MaximumCameraSize());
            float halfHeight = gameCamera.orthographicSize;
            float halfWidth = halfHeight * gameCamera.aspect;
            float worldHalfWidth = Width * TileSize * .5f;
            float worldHalfHeight = Height * TileSize * .5f;
            Vector3 position = gameCamera.transform.position;
            position.x = worldHalfWidth <= halfWidth ? 0f : Mathf.Clamp(position.x, -worldHalfWidth + halfWidth, worldHalfWidth - halfWidth);
            position.y = worldHalfHeight <= halfHeight ? 0f : Mathf.Clamp(position.y, -worldHalfHeight + halfHeight, worldHalfHeight - halfHeight);
            position.z = -10f;
            gameCamera.transform.position = position;
        }

        private float MaximumCameraSize() =>
            Mathf.Min(MaxCameraSize, Height * TileSize * .5f,
                Width * TileSize / (2f * Mathf.Max(.1f, gameCamera.aspect)));

        private void AnimateRover()
        {
            roverIdleClock += Time.deltaTime;
            if (rover == null) return;
            float movement = travel != null ? .018f : .008f;
            float pulse = 1f + Mathf.Sin(roverIdleClock * (travel != null ? 15f : 3f)) * movement;
            rover.transform.localScale = roverBaseScale * pulse;
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
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / .18f);
                float bump = 1f + Mathf.Sin(t * Mathf.PI) * .055f;
                target.localScale = baseline * bump;
                yield return null;
            }
            target.localScale = baseline;
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

        private delegate bool GameAction(out string message);

        private sealed class ActionCard
        {
            public readonly int Category;
            public readonly Transform Root;
            public readonly Image Background;
            public readonly Button Button;
            public readonly TextMeshProUGUI Title;
            public readonly TextMeshProUGUI Detail;
            public readonly string Cost;
            public readonly Func<bool> Action;
            public readonly Func<string> LockReason;

            public ActionCard(int category, Transform root, Image background, Button button,
                TextMeshProUGUI title, TextMeshProUGUI detail, string cost, Func<bool> action, Func<string> lockReason)
            {
                Category = category;
                Root = root;
                Background = background;
                Button = button;
                Title = title;
                Detail = detail;
                Cost = cost;
                Action = action;
                LockReason = lockReason;
            }
        }

        private void Act(GameAction action)
        {
            if (state.GameOver)
            {
                SetReport("La misión terminó. Reiniciá la escena para volver a intentarlo.");
                return;
            }
            bool success = action(out string message);
            SetReport(message);
            if (success) FlashScreen(SpaceFarmPalette.Oxygen, .10f, .22f);
            RefreshWorld();
            RefreshHud();
        }

        private void AdvanceSol()
        {
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
            if (state.LastSol.Storm) FlashScreen(SpaceFarmPalette.Iron, .18f, .32f);
            if (state.LastSol.RaidFoodLoss > 0) FlashScreen(SpaceFarmPalette.Danger, .22f, .38f);
            RefreshWorld();
            RefreshHud();
            RefreshSystems();
        }

        private void SetReport(string value)
        {
            if (reportText != null) reportText.text = value;
        }

        private void RefreshHud()
        {
            statsText.text = $"SOL <b>{state.Sol:00}</b>";
            roverText.text = collectionActive
                ? $"ROVER {state.RoverLevel} · RECOGIDA\nCarga {state.RoverCargo}/{state.RoverCapacity} Fe"
                : travel != null
                    ? $"ROVER {state.RoverLevel}\nEXPLORANDO · {state.ScanRadius} casillas"
                    : $"ROVER {state.RoverLevel}\nListo · alcance {state.ScanRadius}";
            if (state.GameOver)
                objectiveText.text = "MISIÓN TERMINADA · Recursos vitales agotados. Reiniciá la escena para intentarlo de nuevo.";
            else if (state.MissionComplete)
                objectiveText.text = "MISIÓN COMPLETA · Primera cosecha y colonia protegida tras la incursión.";
            else objectiveText.text = "SIGUIENTE PASO  ·  " + NextObjective();
            RefreshActionCards();
            RefreshResourceHud();
            RefreshSystemsHud();
        }

        private string NextObjective()
        {
            if (!state.IronSurveyed) return "Explorá al noreste y estudiá el yacimiento de hierro.";
            if (state.MineLevel == 0) return "Construí la mina de hierro (8 de hierro).";
            if (!state.IceSurveyed) return "Explorá al noroeste para localizar hielo.";
            if (!state.WaterExtractor) return "Construí un extractor para asegurar agua (6 de hierro).";
            if (!state.Greenhouse) return "Construí un invernadero para iniciar la cosecha (6 de hierro).";
            if (state.Population < 3) return "Prepará reservas de O2, agua y comida; luego recibí un colono.";
            if (state.BarrierCount < 8) return $"Protegé la mina: construí barreras ({state.BarrierCount}/8).";
            if (state.Sol < 7) return $"Mantené la colonia hasta el sol 7 (ahora {state.Sol}).";
            return "Revisá recursos y preparate para el próximo evento.";
        }
    }
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>First playable Space Farm loop: discover, build and survive.</summary>
    public sealed class SpaceFarmGame : MonoBehaviour
    {
        private const int Width = 16;
        private const int Height = 11;
        private const float TileSize = 0.9f;
        private const int FogPixelsPerCell = 16;
        private static readonly Vector2Int BaseCell = new Vector2Int(8, 5);
        private static readonly Vector2Int IronCell = new Vector2Int(12, 7);
        private static readonly Vector2Int IceCell = new Vector2Int(3, 8);

        private readonly bool[,] revealed = new bool[Width, Height];
        private readonly SpriteRenderer[,] markers = new SpriteRenderer[Width, Height];
        private Texture2D fogTexture;
        private bool fogDirty = true;
        private SpaceFarmState state;
        private Camera gameCamera;
        private GameObject rover;
        private GameObject greenhouse;
        private GameObject waterExtractor;
        private GameObject barrier;
        private GameObject oxygenPlantTwo;
        private GameObject mine;
        private Vector3 mineBaseScale;
        private Coroutine travel;
        private Vector2Int roverCell = BaseCell;
        private TextMeshProUGUI statsText;
        private TextMeshProUGUI objectiveText;
        private TextMeshProUGUI reportText;
        private TextMeshProUGUI roverText;
        private Sprite squareSprite;
        private Sprite circleSprite;

        private void Start()
        {
            state = new SpaceFarmState();
            ConfigureCamera();
            CreateSprites();
            CreateWorld();
            CreateHud();
            // The lander arrives with a partial orbital chart; the rover reveals the rest.
            Reveal(BaseCell, 5);
            RefreshWorld();
            RefreshHud();
            SetReport(state.LastReport);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || state.GameOver) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Vector3 world = gameCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2Int target = new Vector2Int(
                Mathf.RoundToInt(world.x / TileSize + (Width - 1) / 2f),
                Mathf.RoundToInt(world.y / TileSize + (Height - 1) / 2f));
            if (!Inside(target)) return;
            if (travel != null) StopCoroutine(travel);
            travel = StartCoroutine(TravelTo(target));
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
            gameCamera.orthographicSize = 6.4f;
            gameCamera.transform.position = new Vector3(2.7f, 0f, -10f);
            gameCamera.backgroundColor = SpaceFarmPalette.Background;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
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
        }

        private void CreateWorld()
        {
            GameObject world = new GameObject("Mars Grid");
            world.transform.SetParent(transform);
            SpriteRenderer terrain = CreateArtwork("Superficie de Marte", "Art/mars-terrain", Vector3.zero,
                Width * TileSize, Height * TileSize, -10, world.transform);
            terrain.color = Color.white;
            fogTexture = new Texture2D(Width * FogPixelsPerCell, Height * FogPixelsPerCell,
                TextureFormat.RGBA32, false);
            fogTexture.filterMode = FilterMode.Bilinear;
            Sprite fogSprite = Sprite.Create(fogTexture,
                new Rect(0, 0, fogTexture.width, fogTexture.height), new Vector2(.5f, .5f),
                FogPixelsPerCell / TileSize);
            CreateWorldSprite("Niebla de guerra", fogSprite, Vector3.zero, Vector3.one, Color.white, 0,
                world.transform);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == IronCell || cell == IceCell)
                {
                    markers[x, y] = CreateArtwork(cell == IronCell ? "Yacimiento de hierro" : "Depósito de hielo",
                        cell == IronCell ? "Art/iron-deposit" : "Art/ice-deposit",
                        CellPosition(cell) + new Vector3(0, 0, -.1f), 1.55f, 1.03f, 3, world.transform);
                    markers[x, y].enabled = false;
                }
            }

            CreateArtwork("Nave y hábitat", "Art/lander", CellPosition(BaseCell) + new Vector3(0, 0, -.2f),
                3.1f, 2.07f, 5, world.transform);
            CreateArtwork("Planta de oxígeno", "Art/oxygen-plant",
                CellPosition(BaseCell + new Vector2Int(2, 0)) + new Vector3(0, 0, -.2f),
                1.7f, 1.14f, 6, world.transform);
            rover = CreateArtwork("Rover 1", "Art/rover", CellPosition(roverCell) + new Vector3(-.65f, -.5f, -.5f),
                1.55f, 1.04f, 8, world.transform).gameObject;
            greenhouse = CreateBuilding("Invernadero", "Art/greenhouse", BaseCell + new Vector2Int(-2, 1), world.transform);
            waterExtractor = CreateBuilding("Extractor de hielo", "Art/water-extractor", IceCell, world.transform);
            mine = CreateBuilding("Mina de hierro", "Art/iron-mine", IronCell, world.transform);
            mineBaseScale = mine.transform.localScale;
            oxygenPlantTwo = CreateBuilding("Planta de oxígeno 2", "Art/oxygen-plant", BaseCell + new Vector2Int(2, 2), world.transform);
            barrier = CreateBarriers(world.transform);
            greenhouse.SetActive(false);
            waterExtractor.SetActive(false);
            mine.SetActive(false);
            barrier.SetActive(false);
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

        private GameObject CreateBarriers(Transform parent)
        {
            GameObject root = new GameObject("Barreras del hábitat");
            root.transform.SetParent(parent);
            Vector3 center = CellPosition(BaseCell);
            for (int i = -1; i <= 1; i++)
            {
                CreateWorldSprite("Barrera norte", squareSprite, center + new Vector3(i * .85f, 1.27f, -.3f),
                    new Vector3(.76f, .12f, 1), SpaceFarmPalette.Rover, 7, root.transform);
                CreateWorldSprite("Barrera sur", squareSprite, center + new Vector3(i * .85f, -1.27f, -.3f),
                    new Vector3(.76f, .12f, 1), SpaceFarmPalette.Rover, 7, root.transform);
            }
            for (int i = -1; i <= 1; i++)
            {
                CreateWorldSprite("Barrera oeste", squareSprite, center + new Vector3(-1.53f, i * .72f, -.3f),
                    new Vector3(.12f, .61f, 1), SpaceFarmPalette.Rover, 7, root.transform);
                CreateWorldSprite("Barrera este", squareSprite, center + new Vector3(1.53f, i * .72f, -.3f),
                    new Vector3(.12f, .61f, 1), SpaceFarmPalette.Rover, 7, root.transform);
            }
            return root;
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
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int dx = x - center.x;
                int dy = y - center.y;
                if (dx * dx + dy * dy <= radius * radius && !revealed[x, y])
                {
                    revealed[x, y] = true;
                    fogDirty = true;
                }
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
                float nearest = 2f;
                for (int y = Mathf.Max(0, cellY - 2); y <= Mathf.Min(Height - 1, cellY + 2); y++)
                for (int x = Mathf.Max(0, cellX - 2); x <= Mathf.Min(Width - 1, cellX + 2); x++)
                {
                    if (!revealed[x, y]) continue;
                    float dx = Mathf.Max(0f, Mathf.Abs(gridX - x) - .5f);
                    float dy = Mathf.Max(0f, Mathf.Abs(gridY - y) - .5f);
                    nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy));
                }
                float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(nearest / .72f));
                fogColor.a = (byte)(SpaceFarmPalette.FogOverlay.a * 255f * opacity);
                pixels[py * pixelWidth + px] = fogColor;
            }
            fogTexture.SetPixels32(pixels);
            fogTexture.Apply(false);
            fogDirty = false;
        }

        private IEnumerator TravelTo(Vector2Int target)
        {
            while (roverCell != target)
            {
                int x = roverCell.x;
                int y = roverCell.y;
                if (x != target.x) x += Math.Sign(target.x - x);
                else y += Math.Sign(target.y - y);
                roverCell = new Vector2Int(x, y);
                rover.transform.position = CellPosition(roverCell) + new Vector3(0, 0, -.5f);
                Reveal(roverCell, state.ScanRadius);
                RefreshWorld();
                roverText.text = $"ROVER {state.RoverLevel} · RADIO {state.ScanRadius}";
                yield return new WaitForSeconds(.13f);
            }
            if (roverCell == IronCell) SetReport(state.SurveyIron());
            else if (roverCell == IceCell) SetReport(state.SurveyIce());
            else SetReport($"Rover en el sector {target.x},{target.y}. Elegí otro punto para seguir explorando.");
            RefreshWorld();
            RefreshHud();
            travel = null;
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
            mine.SetActive(state.MineLevel > 0);
            mine.transform.localScale = mineBaseScale * (state.MineLevel >= 2 ? 1.16f : 1f);
            mine.GetComponent<SpriteRenderer>().color = state.MineDamaged ? SpaceFarmPalette.DamagedTint : Color.white;
            greenhouse.SetActive(state.Greenhouse);
            waterExtractor.SetActive(state.WaterExtractor);
            barrier.SetActive(state.Barrier);
            oxygenPlantTwo.SetActive(state.OxygenPlants > 1);
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
            scaler.matchWidthOrHeight = .5f;

            if (EventSystem.current == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(canvasObject.transform);
            }

            GameObject header = Panel("Header", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 76));
            Label("SPACE FARM", header.transform, 28, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(22, -12), new Vector2(230, 52));
            statsText = Label("Recursos", header.transform, 18, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(270, -11), new Vector2(-620, 56));
            Panel("Header Accent", header.transform, SpaceFarmPalette.Iron,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                Vector2.zero, new Vector2(0, 3));

            GameObject side = Panel("Mission Panel", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-352, 82), new Vector2(336, -156));
            RectTransform sideRect = side.GetComponent<RectTransform>();
            sideRect.offsetMin = new Vector2(-352, 74);
            sideRect.offsetMax = new Vector2(-16, -82);
            VerticalLayoutGroup layout = side.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LabelWithLayout("MISIÓN 01", side.transform, 20, FontStyles.Bold,
                SpaceFarmPalette.Oxygen, 30);
            objectiveText = LabelWithLayout("", side.transform, 17, FontStyles.Normal,
                SpaceFarmPalette.Text, 88);
            roverText = LabelWithLayout("", side.transform, 17, FontStyles.Bold,
                SpaceFarmPalette.Ice, 26);
            LabelWithLayout("OPERACIONES", side.transform, 15, FontStyles.Bold,
                SpaceFarmPalette.MutedText, 20);
            AddButton("Pasar un sol", side.transform, AdvanceSol);
            AddActionPair(side.transform,
                "Mina · 8 Fe", () => Act(state.BuildMine),
                "Mejorar · 6 Fe", () => Act(state.UpgradeMine));
            AddActionPair(side.transform,
                "Reparar · 3 Fe", () => Act(state.RepairMine),
                "Rover · 5 Fe", () => Act(state.UpgradeRover));
            AddActionPair(side.transform,
                "Agua · 6 Fe", () => Act(state.BuildWaterExtractor),
                "Cultivo · 6 Fe", () => Act(state.BuildGreenhouse));
            AddActionPair(side.transform,
                "Oxígeno · 9 Fe", () => Act(state.BuildOxygenPlant),
                "Barreras · 5 Fe", () => Act(state.BuildBarrier));
            AddActionPair(side.transform,
                "Nuevo colono", () => Act(state.AddColonist),
                "Tierra · 4 Fe", () => Act(state.OrderFromEarth));

            GameObject footer = Panel("Event Log", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(-368, 70));
            reportText = Label("", footer.transform, 17, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0),
                new Vector2(18, 8), new Vector2(-36, -16));
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

        private TextMeshProUGUI LabelWithLayout(string value, Transform parent, float size, FontStyles style,
            Color color, float height)
        {
            TextMeshProUGUI text = Label(value, parent, size, style, color, Vector2.zero, Vector2.one,
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private void AddActionPair(Transform parent, string leftCaption, Action leftAction,
            string rightCaption, Action rightAction)
        {
            GameObject row = new GameObject("Acciones", typeof(RectTransform), typeof(LayoutElement),
                typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 48;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            AddButton(leftCaption, row.transform, leftAction);
            AddButton(rightCaption, row.transform, rightAction);
        }

        private void AddButton(string caption, Transform parent, Action callback)
        {
            GameObject item = new GameObject(caption, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(LayoutElement));
            item.transform.SetParent(parent, false);
            LayoutElement element = item.GetComponent<LayoutElement>();
            element.preferredHeight = 48;
            element.flexibleWidth = 1;
            UnityEngine.UI.Image image = item.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            UnityEngine.UI.Button button = item.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = caption == "Pasar un sol" ? SpaceFarmPalette.Iron : SpaceFarmPalette.Button;
            colors.highlightedColor = SpaceFarmPalette.ButtonHover;
            colors.pressedColor = SpaceFarmPalette.Ice;
            colors.selectedColor = SpaceFarmPalette.ButtonHover;
            button.colors = colors;
            button.onClick.AddListener(() => callback());
            TextMeshProUGUI text = Label(caption, item.transform, caption == "Pasar un sol" ? 18 : 16,
                FontStyles.Bold, caption == "Pasar un sol" ? SpaceFarmPalette.Background : SpaceFarmPalette.Text,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-12, -4));
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private delegate bool GameAction(out string message);

        private void Act(GameAction action)
        {
            if (state.GameOver)
            {
                SetReport("La misión terminó. Reiniciá la escena para volver a intentarlo.");
                return;
            }
            action(out string message);
            SetReport(message);
            RefreshWorld();
            RefreshHud();
        }

        private void AdvanceSol()
        {
            if (travel != null) return;
            SetReport(state.AdvanceSol());
            RefreshWorld();
            RefreshHud();
        }

        private void SetReport(string value)
        {
            if (reportText != null) reportText.text = value;
        }

        private void RefreshHud()
        {
            statsText.text = $"SOL {state.Sol}    HIERRO {state.Iron}    ENERGÍA {state.Energy}    O2 {state.Oxygen}\nAGUA {state.Water}    COMIDA {state.Food}    COLONOS {state.Population}    SALUD {state.Health}/3";
            roverText.text = $"ROVER {state.RoverLevel} · RADIO {state.ScanRadius}";
            if (state.GameOver) objectiveText.text = "MISIÓN PERDIDA\nSe agotó un recurso vital. Reiniciá la escena para volver a intentarlo.";
            else if (state.MissionComplete) objectiveText.text = "¡MISIÓN COMPLETADA!\nLa colonia logró su primera cosecha y resistió la incursión.";
            else
            {
                string iron = state.IronSurveyed ? "OK" : "--";
                string ice = state.IceSurveyed ? "OK" : "--";
                string mine = state.MineLevel > 0 ? "OK" : "--";
                string crop = state.Greenhouse ? "OK" : "--";
                string people = state.Population >= 3 ? "OK" : "--";
                string wall = state.Barrier ? "OK" : "--";
                objectiveText.text = $"Fe {iron} · Hielo {ice}\nMina {mine} · Cultivo {crop}\nColono {people} · Muro {wall}\nClic en mapa para explorar";
            }
        }
    }
}

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
        private static readonly Vector2Int BaseCell = new Vector2Int(8, 5);
        private static readonly Vector2Int IronCell = new Vector2Int(12, 7);
        private static readonly Vector2Int IceCell = new Vector2Int(3, 8);

        private readonly bool[,] revealed = new bool[Width, Height];
        private readonly SpriteRenderer[,] tiles = new SpriteRenderer[Width, Height];
        private readonly SpriteRenderer[,] markers = new SpriteRenderer[Width, Height];
        private SpaceFarmState state;
        private Camera gameCamera;
        private GameObject rover;
        private GameObject greenhouse;
        private GameObject waterExtractor;
        private GameObject barrier;
        private GameObject oxygenPlantTwo;
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
            Reveal(BaseCell, state.ScanRadius);
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
            gameCamera.transform.position = new Vector3(0f, 0f, -10f);
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
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                tiles[x, y] = CreateWorldSprite($"Sector {x},{y}", squareSprite, CellPosition(cell),
                    new Vector3(.85f, .85f, 1f), SpaceFarmPalette.Fog, 0, world.transform);
                if (cell == IronCell || cell == IceCell)
                {
                    markers[x, y] = CreateWorldSprite(cell == IronCell ? "Hierro" : "Hielo", squareSprite,
                        CellPosition(cell) + new Vector3(0, 0, -.1f),
                        new Vector3(.34f, .34f, 1f), cell == IronCell ? SpaceFarmPalette.Iron : SpaceFarmPalette.Ice,
                        2, world.transform);
                    markers[x, y].transform.rotation = Quaternion.Euler(0, 0, 45);
                    markers[x, y].enabled = false;
                }
            }

            CreateWorldSprite("Nave y hábitat", squareSprite, CellPosition(BaseCell) + new Vector3(0, 0, -.2f),
                new Vector3(1.45f, 1.2f, 1f), SpaceFarmPalette.Rover, 4, world.transform);
            CreateWorldSprite("Planta de oxígeno", circleSprite, CellPosition(BaseCell + Vector2Int.right) + new Vector3(0, 0, -.25f),
                new Vector3(.65f, .65f, 1f), SpaceFarmPalette.Oxygen, 5, world.transform);
            rover = CreateWorldSprite("Rover 1", circleSprite, CellPosition(roverCell) + new Vector3(0, 0, -.5f),
                new Vector3(.32f, .32f, 1f), SpaceFarmPalette.Button, 8, world.transform).gameObject;
            CreateWorldSprite("Antena del rover", squareSprite, rover.transform.position + new Vector3(0, .13f, -.1f),
                new Vector3(.05f, .22f, 1f), SpaceFarmPalette.Iron, 9, rover.transform);
            greenhouse = CreateBuilding("Invernadero", BaseCell + new Vector2Int(-1, 1), SpaceFarmPalette.Greenhouse, world.transform);
            waterExtractor = CreateBuilding("Extractor de hielo", IceCell, SpaceFarmPalette.Ice, world.transform);
            barrier = CreateBuilding("Barreras", BaseCell + new Vector2Int(0, -2), SpaceFarmPalette.Iron, world.transform);
            oxygenPlantTwo = CreateBuilding("Planta de oxígeno 2", BaseCell + new Vector2Int(2, 0), SpaceFarmPalette.Oxygen, world.transform);
            greenhouse.SetActive(false);
            waterExtractor.SetActive(false);
            barrier.SetActive(false);
            oxygenPlantTwo.SetActive(false);
        }

        private GameObject CreateBuilding(string name, Vector2Int cell, Color tint, Transform parent)
        {
            return CreateWorldSprite(name, squareSprite, CellPosition(cell) + new Vector3(0, 0, -.2f),
                new Vector3(.6f, .6f, 1f), tint, 5, parent).gameObject;
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
                if (Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y) <= radius)
                    revealed[x, y] = true;
            }
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
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                bool visible = revealed[x, y];
                tiles[x, y].color = visible ? ((x + y) % 2 == 0 ? SpaceFarmPalette.Soil : SpaceFarmPalette.SoilLight)
                    : SpaceFarmPalette.Fog;
                if (markers[x, y] != null) markers[x, y].enabled = visible;
            }
            if (state.MineLevel > 0)
            {
                markers[IronCell.x, IronCell.y].color = state.MineDamaged ? SpaceFarmPalette.Danger : SpaceFarmPalette.Iron;
                markers[IronCell.x, IronCell.y].transform.localScale = state.MineLevel == 1
                    ? new Vector3(.62f, .62f, 1) : new Vector3(.78f, .78f, 1);
            }
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
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            if (EventSystem.current == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(canvasObject.transform);
            }

            GameObject header = Panel("Header", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(0, 104));
            Label("SPACE FARM  /  MARTE", header.transform, 29, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(28, -18), new Vector2(390, 68));
            statsText = Label("Recursos", header.transform, 20, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(420, -16), new Vector2(-760, 75));

            GameObject side = Panel("Mission Panel", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-344, 116), new Vector2(330, -242));
            RectTransform sideRect = side.GetComponent<RectTransform>();
            sideRect.offsetMin = new Vector2(-344, 118);
            sideRect.offsetMax = new Vector2(-14, -118);
            VerticalLayoutGroup layout = side.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 18, 18);
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LabelWithLayout("MISIÓN 01", side.transform, 20, FontStyles.Bold,
                SpaceFarmPalette.Oxygen, 36);
            objectiveText = LabelWithLayout("", side.transform, 17, FontStyles.Normal,
                SpaceFarmPalette.Text, 100);
            roverText = LabelWithLayout("", side.transform, 17, FontStyles.Normal,
                SpaceFarmPalette.Ice, 30);
            AddButton("Pasar un sol", side.transform, AdvanceSol);
            AddButton("Mina · 8 Fe", side.transform, () => Act(state.BuildMine));
            AddButton("Mina nivel 2 · 6 Fe", side.transform, () => Act(state.UpgradeMine));
            AddButton("Reparar mina · 3 Fe", side.transform, () => Act(state.RepairMine));
            AddButton("Rover nivel 2 · 5 Fe", side.transform, () => Act(state.UpgradeRover));
            AddButton("Extraer agua · 6 Fe", side.transform, () => Act(state.BuildWaterExtractor));
            AddButton("Invernadero · 6 Fe", side.transform, () => Act(state.BuildGreenhouse));
            AddButton("Planta de O₂ · 9 Fe", side.transform, () => Act(state.BuildOxygenPlant));
            AddButton("Barreras · 5 Fe", side.transform, () => Act(state.BuildBarrier));
            AddButton("Nuevo colono", side.transform, () => Act(state.AddColonist));
            AddButton("Pedido Tierra · 4 Fe", side.transform, () => Act(state.OrderFromEarth));

            GameObject footer = Panel("Event Log", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(-358, 104));
            reportText = Label("", footer.transform, 19, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0),
                new Vector2(24, 12), new Vector2(-48, -24));
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

        private void AddButton(string caption, Transform parent, Action callback)
        {
            GameObject item = new GameObject(caption, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(LayoutElement));
            item.transform.SetParent(parent, false);
            item.GetComponent<LayoutElement>().preferredHeight = 39;
            UnityEngine.UI.Image image = item.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            UnityEngine.UI.Button button = item.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = SpaceFarmPalette.Button;
            colors.highlightedColor = SpaceFarmPalette.ButtonHover;
            colors.pressedColor = SpaceFarmPalette.Ice;
            colors.selectedColor = SpaceFarmPalette.ButtonHover;
            button.colors = colors;
            button.onClick.AddListener(() => callback());
            TextMeshProUGUI text = Label(caption, item.transform, 18, FontStyles.Normal, SpaceFarmPalette.Text,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-16, -4));
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
            statsText.text = $"SOL {state.Sol}    Fe {state.Iron}    ENERGÍA {state.Energy}    O₂ {state.Oxygen}    AGUA {state.Water}    COMIDA {state.Food}\nCOLONOS {state.Population}    SALUD {state.Health}/3";
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

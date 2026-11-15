using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>
    /// Corner minimap: terrain colour of explored cells, fog over the rest, base, rover, discovered
    /// deposits and points of interest, and the frame of the main camera. The picture is rebuilt only
    /// when exploration or the colony changes; per frame it only moves two markers when they changed.
    /// </summary>
    public sealed partial class SpaceFarmGame
    {
        private const string MinimapSideKey = "SpaceFarm.MinimapRight";
        private const string MinimapSizeKey = "SpaceFarm.MinimapExpanded";
        private static bool minimapPreferencesLoaded;
        private static bool minimapRight;
        private static bool minimapExpanded;

        private RectTransform minimapRoot;
        private RectTransform minimapView;
        private RectTransform minimapRover;
        private RectTransform minimapFrame;
        private RectTransform minimapGreenhouse;
        private RectTransform minimapOxygenTwo;
        private RectTransform[] minimapPoints;
        private Texture2D minimapTexture;
        private Color32[] minimapPixels;
        private Button minimapSizeButton;
        private Button minimapSideButton;
        private bool minimapDirty = true;
        private float minimapTimer;
        private Vector3 minimapLastCamera = new Vector3(float.MaxValue, 0f, 0f);
        private float minimapLastSize;
        private float minimapLastAspect;
        private Vector3 minimapLastRover = new Vector3(float.MaxValue, 0f, 0f);

        public bool MinimapOnRight => minimapRight;
        public bool MinimapExpanded => minimapExpanded;
        public void SetMinimapSide(bool right) { minimapRight = right; SaveMinimapPreferences(); ApplyMinimapLayout(); }
        public void SetMinimapExpanded(bool expanded) { minimapExpanded = expanded; SaveMinimapPreferences(); ApplyMinimapLayout(); }

        private void MarkMinimapDirty() => minimapDirty = true;

        private void CreateMinimap()
        {
            if (!minimapPreferencesLoaded)
            {
                // Bottom left by default: the installation card and AVANZAR SOL live on the right.
                minimapRight = PlayerPrefs.GetInt(MinimapSideKey, 0) == 1;
                minimapExpanded = PlayerPrefs.GetInt(MinimapSizeKey, 0) == 1;
                minimapPreferencesLoaded = true;
            }
            GameObject root = Panel("Minimapa", hudCanvas.transform, SpaceFarmPalette.Panel,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(204, 150));
            root.GetComponent<Image>().raycastTarget = true;
            minimapRoot = root.GetComponent<RectTransform>();
            Panel("Borde", root.transform, SpaceFarmPalette.Iron, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 1), Vector2.zero, new Vector2(0, 2));
            TextMeshProUGUI title = Label($"MAPA · semilla {seed}", root.transform, 11, FontStyles.Bold, SpaceFarmPalette.MutedText,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(7, -3), new Vector2(-62, 20));
            title.enableAutoSizing = true;
            title.fontSizeMin = 8;
            title.fontSizeMax = 11;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            minimapSideButton = AddButton("<", root.transform, () => SetMinimapSide(!minimapRight), 12,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-29, -4), new Vector2(22, 18));
            minimapSizeButton = AddButton("+", root.transform, () => SetMinimapExpanded(!minimapExpanded), 13,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(22, 18));

            minimapTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            minimapTexture.filterMode = FilterMode.Bilinear;
            minimapTexture.wrapMode = TextureWrapMode.Clamp;
            minimapPixels = new Color32[Width * Height];
            runtimeAssets.Add(minimapTexture);
            GameObject view = new GameObject("Vista", typeof(RectTransform), typeof(RawImage), typeof(SpaceFarmMinimapInput));
            view.transform.SetParent(root.transform, false);
            minimapView = view.GetComponent<RectTransform>();
            minimapView.anchorMin = Vector2.zero;
            minimapView.anchorMax = Vector2.one;
            minimapView.offsetMin = new Vector2(6, 6);
            minimapView.offsetMax = new Vector2(-6, -24);
            RawImage image = view.GetComponent<RawImage>();
            image.texture = minimapTexture;
            image.raycastTarget = true;
            view.GetComponent<SpaceFarmMinimapInput>().Navigate = NavigateFromMinimap;

            foreach (DepositView deposit in depositViews)
            {
                bool iron = deposit.Info.Kind == DepositKind.Iron;
                deposit.MiniMarker = MinimapMarker(deposit.Info.Name, squareSprite, deposit.Info.Cell,
                    iron ? SpaceFarmPalette.Iron : SpaceFarmPalette.Ice, 6f, iron ? 45f : 0f);
            }
            minimapPoints = new RectTransform[map.Points.Count];
            for (int i = 0; i < minimapPoints.Length; i++)
                minimapPoints[i] = MinimapMarker("Punto de interés", ringSprite, map.Points[i].Cell, SpaceFarmPalette.Text, 8f, 0f);
            MinimapMarker("Base borde", squareSprite, BaseCell, SpaceFarmPalette.Background, 11f, 0f).gameObject.SetActive(true);
            MinimapMarker("Base", squareSprite, BaseCell, SpaceFarmPalette.Text, 8f, 0f).gameObject.SetActive(true);
            minimapGreenhouse = MinimapMarker("Invernadero", circleSprite, BaseCell, SpaceFarmPalette.Greenhouse, 8f, 0f);
            minimapOxygenTwo = MinimapMarker("Planta de O2 adicional", circleSprite, BaseCell, SpaceFarmPalette.Oxygen, 8f, 0f);

            GameObject frame = new GameObject("Cámara", typeof(RectTransform));
            frame.transform.SetParent(minimapView, false);
            minimapFrame = frame.GetComponent<RectTransform>();
            Panel("Arriba", frame.transform, SpaceFarmPalette.Text, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 1.5f));
            Panel("Abajo", frame.transform, SpaceFarmPalette.Text, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 1.5f));
            Panel("Izquierda", frame.transform, SpaceFarmPalette.Text, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, new Vector2(1.5f, 0));
            Panel("Derecha", frame.transform, SpaceFarmPalette.Text, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, .5f), Vector2.zero, new Vector2(1.5f, 0));

            // Rover: mint dot on a dark rim so it reads over any terrain colour.
            minimapRover = MinimapMarker("Rover", circleSprite, roverCell, SpaceFarmPalette.Background, 11f, 0f);
            minimapRover.gameObject.SetActive(true);
            Image dot = new GameObject("Punto", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dot.transform.SetParent(minimapRover, false);
            dot.sprite = circleSprite;
            dot.color = SpaceFarmPalette.Oxygen;
            dot.raycastTarget = false;
            dot.rectTransform.sizeDelta = new Vector2(7f, 7f);

            ApplyMinimapLayout();
            RefreshMinimap();
        }

        private RectTransform MinimapMarker(string name, Sprite sprite, Vector2Int cell, Color color, float size, float rotation)
        {
            GameObject marker = new GameObject(name, typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(minimapView, false);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = CellToMinimap(cell);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = marker.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            marker.SetActive(false);
            return rect;
        }

        private static Vector2 CellToMinimap(Vector2Int cell) =>
            new Vector2((cell.x + .5f) / Width, (cell.y + .5f) / Height);

        private Vector2 WorldToMinimap(Vector3 world) =>
            new Vector2(world.x / (Width * TileSize) + .5f,
                world.y / (Height * TileSize * SpaceFarmMap.TerrainPerspectiveYScale) + .5f);

        /// <summary>Sizes and docks the minimap, and moves the installation card to the opposite side so they never overlap.</summary>
        private void ApplyMinimapLayout()
        {
            if (minimapRoot == null) return;
            Vector2 view = minimapExpanded ? new Vector2(304, 148) : new Vector2(192, 94);
            Vector2 corner = new Vector2(minimapRight ? 1 : 0, 0);
            minimapRoot.anchorMin = minimapRoot.anchorMax = minimapRoot.pivot = corner;
            minimapRoot.anchoredPosition = new Vector2(minimapRight ? -12 : 12, FooterHeight + 10);
            minimapRoot.sizeDelta = view + new Vector2(12, 30);
            minimapSizeButton.GetComponentInChildren<TextMeshProUGUI>().text = minimapExpanded ? "-" : "+";
            minimapSideButton.GetComponentInChildren<TextMeshProUGUI>().text = minimapRight ? "<" : ">";
            if (infoPanel != null)
            {
                RectTransform card = infoPanel.GetComponent<RectTransform>();
                Vector2 top = new Vector2(minimapRight ? 0 : 1, 1);
                card.anchorMin = card.anchorMax = card.pivot = top;
                card.anchoredPosition = new Vector2(minimapRight ? 200 : -15, -108);
            }
            ApplyConstructionMenuLayout();
            if (pauseMinimapLabel != null)
                pauseMinimapLabel.text = minimapRight ? "MINIMAPA: DERECHA" : "MINIMAPA: IZQUIERDA";
            minimapLastCamera = new Vector3(float.MaxValue, 0f, 0f);
        }

        private static void SaveMinimapPreferences()
        {
            PlayerPrefs.SetInt(MinimapSideKey, minimapRight ? 1 : 0);
            PlayerPrefs.SetInt(MinimapSizeKey, minimapExpanded ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void UpdateMinimap()
        {
            if (minimapRoot == null) return;
            minimapTimer += Time.unscaledDeltaTime;
            if (minimapDirty && minimapTimer >= .12f) RefreshMinimap();

            Vector3 roverPosition = rover.transform.position;
            if (roverPosition != minimapLastRover)
            {
                minimapLastRover = roverPosition;
                minimapRover.anchorMin = minimapRover.anchorMax = WorldToMinimap(roverPosition);
            }
            Vector3 cameraPosition = gameCamera.transform.position;
            float size = gameCamera.orthographicSize;
            float aspect = gameCamera.aspect;
            if (cameraPosition == minimapLastCamera && Mathf.Approximately(size, minimapLastSize) &&
                Mathf.Approximately(aspect, minimapLastAspect)) return;
            minimapLastCamera = cameraPosition;
            minimapLastSize = size;
            minimapLastAspect = aspect;
            // The frame shows the part of the view the HUD bars do not cover.
            float unitsPerPixel = 2f * size / Mathf.Max(1, Screen.height);
            float hudScale = Screen.width / 1280f;
            Vector3 min = cameraPosition + new Vector3(-size * aspect, -size + FooterHeight * hudScale * unitsPerPixel, 0f);
            Vector3 max = cameraPosition + new Vector3(size * aspect, size - HeaderHeight * hudScale * unitsPerPixel, 0f);
            Vector2 low = WorldToMinimap(min);
            Vector2 high = WorldToMinimap(max);
            minimapFrame.anchorMin = new Vector2(Mathf.Clamp01(low.x), Mathf.Clamp01(low.y));
            minimapFrame.anchorMax = new Vector2(Mathf.Clamp01(high.x), Mathf.Clamp01(high.y));
            minimapFrame.offsetMin = minimapFrame.offsetMax = Vector2.zero;
        }

        /// <summary>Rebuilds the 128×80 picture and the marker visibility. Hidden cells show only fog, so nothing leaks.</summary>
        private void RefreshMinimap()
        {
            minimapDirty = false;
            minimapTimer = 0f;
            Color32 fog = SpaceFarmPalette.Fog;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int index = y * Width + x;
                minimapPixels[index] = revealed[x, y] ? terrainCellColors[index] : fog;
            }
            minimapTexture.SetPixels32(minimapPixels);
            minimapTexture.Apply(false);
            foreach (DepositView deposit in depositViews)
            {
                SpaceFarmState.DepositState info = state.Deposit(deposit.Info.Id);
                bool visible = revealed[deposit.Info.Cell.x, deposit.Info.Cell.y];
                deposit.MiniMarker.gameObject.SetActive(visible);
                if (!visible) continue;
                float size = info.HasBuilding ? 9f : 6f;
                deposit.MiniMarker.sizeDelta = new Vector2(size, size);
            }
            for (int i = 0; i < minimapPoints.Length; i++)
                minimapPoints[i].gameObject.SetActive(revealed[map.Points[i].Cell.x, map.Points[i].Cell.y] && !state.HasDiscovery(i));
            minimapGreenhouse.gameObject.SetActive(state.GreenhouseCell.HasValue);
            if (state.GreenhouseCell.HasValue) minimapGreenhouse.anchorMin = minimapGreenhouse.anchorMax = CellToMinimap(state.GreenhouseCell.Value);
            minimapOxygenTwo.gameObject.SetActive(state.OxygenPlantTwoCell.HasValue);
            if (state.OxygenPlantTwoCell.HasValue) minimapOxygenTwo.anchorMin = minimapOxygenTwo.anchorMax = CellToMinimap(state.OxygenPlantTwoCell.Value);
        }

        private void NavigateFromMinimap(Vector2 screen, Camera eventCamera)
        {
            if (paused) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(minimapView, screen, eventCamera, out Vector2 local)) return;
            Rect rect = minimapView.rect;
            float u = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            float v = Mathf.Clamp01((local.y - rect.yMin) / rect.height);
            Vector2Int cell = new Vector2Int(Mathf.Min(Width - 1, (int)(u * Width)), Mathf.Min(Height - 1, (int)(v * Height)));
            if (!revealed[cell.x, cell.y])
            {
                SetReport("Esa zona del minimapa sigue sin explorar. Enviá el rover para revelarla.");
                return;
            }
            if (followRover) SetFollow(false);
            FocusWorld(new Vector3((u - .5f) * Width * TileSize,
                (v - .5f) * Height * TileSize * SpaceFarmMap.TerrainPerspectiveYScale, 0f));
        }
    }
}

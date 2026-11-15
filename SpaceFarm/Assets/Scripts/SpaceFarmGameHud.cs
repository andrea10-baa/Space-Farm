using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>HUD: resources, a compact report bar, a collapsible catalog and contextual installation cards.</summary>
    public sealed partial class SpaceFarmGame
    {
        private readonly List<TextMeshProUGUI> resourceLabels = new List<TextMeshProUGUI>();
        private readonly List<RectTransform> resourceChips = new List<RectTransform>();
        private readonly List<TextMeshProUGUI> resourceAmounts = new List<TextMeshProUGUI>();
        private readonly List<RectTransform> resourceBarRects = new List<RectTransform>();
        private Sprite[] resourceIconSprites;
        private int[] previousResources;
        private GameObject buildModePanel;
        private TextMeshProUGUI buildModeText;
        private TextMeshProUGUI defenseText;
        private RectTransform xpBarFill;
        private const float XpBarWidth = 132f;
        private GameObject infoPanel;
        private TextMeshProUGUI infoTitle;
        private TextMeshProUGUI infoBody;
        private Button infoPrimary;
        private Button infoSecondary;
        private Button followButton;
        private Button baseNavButton;
        private Button roverNavButton;
        private string selectedBuilding;
        private int selectedBuildingIndex;
        private int selectedDeposit = -1;
        private GameObject constructionMenuRoot;
        private bool constructionMenuOpen;
        private Button constructionMenuToggle;
        private Button upgradeMenuToggle;
        private Button editModeToggle;
        private Button exploreModeToggle;
        private string upgradeCatalogSignature = string.Empty;

        private sealed class ActionCard
        {
            public int Category;
            public Transform Root;
            public Image Background;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Detail;
            public Func<string> Cost;
            public Func<bool> Action;
            public Func<string> LockReason;
            public bool BarrierMode;
            public StructureKind Placement;
            public Button Button;
            public Image Icon;
            public string Description;
        }

        private void CreateHud()
        {
            GameObject canvasObject = new GameObject("Space Farm HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            hudCanvas = canvasObject.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, HeaderHeight));
            header.GetComponent<Image>().raycastTarget = true;
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
            objectiveText.fontSizeMin = 11;
            objectiveText.fontSizeMax = 16;
            defenseText = Label("", header.transform, 12, FontStyles.Bold, SpaceFarmPalette.Iron,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(712, -10), new Vector2(XpBarWidth, 32));
            defenseText.textWrappingMode = TextWrappingModes.NoWrap;
            GameObject xpTrack = new GameObject("Progreso de experiencia", typeof(RectTransform), typeof(Image));
            xpTrack.transform.SetParent(header.transform, false);
            RectTransform xpTrackRect = xpTrack.GetComponent<RectTransform>();
            xpTrackRect.anchorMin = xpTrackRect.anchorMax = xpTrackRect.pivot = new Vector2(0, 1);
            xpTrackRect.anchoredPosition = new Vector2(712, -47);
            xpTrackRect.sizeDelta = new Vector2(XpBarWidth, 6);
            xpTrack.GetComponent<Image>().color = SpaceFarmPalette.Background;
            GameObject xpFill = new GameObject("XP acumulada", typeof(RectTransform), typeof(Image));
            xpFill.transform.SetParent(xpTrack.transform, false);
            xpBarFill = xpFill.GetComponent<RectTransform>();
            xpBarFill.anchorMin = new Vector2(0, 0); xpBarFill.anchorMax = new Vector2(0, 1);
            xpBarFill.pivot = new Vector2(0, .5f); xpBarFill.anchoredPosition = Vector2.zero;
            xpBarFill.sizeDelta = new Vector2(0, 0);
            xpFill.GetComponent<Image>().color = SpaceFarmPalette.Oxygen;
            roverText = Label("", header.transform, 13, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-176, -12), new Vector2(160, 48));
            roverText.enableAutoSizing = true; roverText.fontSizeMin = 8; roverText.fontSizeMax = 12;
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
            AddButton("II  PAUSA", header.transform, () => SetPaused(true), 12, SpaceFarmPalette.PanelRaised,
                SpaceFarmPalette.Text, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-344, -17), new Vector2(86, 48));

            GameObject footer = Panel("Barra de mensajes", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, FooterHeight));
            footer.GetComponent<Image>().raycastTarget = true;
            reportText = Label("", footer.transform, 14, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(18, -5), new Vector2(-700, 34));
            reportText.enableAutoSizing = true;
            reportText.fontSizeMin = 10;
            reportText.fontSizeMax = 14;
            reportText.textWrappingMode = TextWrappingModes.Normal;
            reportText.overflowMode = TextOverflowModes.Ellipsis;
            constructionMenuToggle = AddButton("CONSTRUIR", footer.transform, ToggleConstructionMenu,
                14, SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-10, 0), new Vector2(128, 34));
            upgradeMenuToggle = AddButton("MEJORAS", footer.transform, OpenUpgradeMenu,
                13, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-146,0),new Vector2(128,34));
            editModeToggle = AddButton("EDITAR", footer.transform, () => SetEditMode(!editMode),
                13, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-282,0),new Vector2(128,34));
            exploreModeToggle = AddButton("EXPLORAR", footer.transform, EnterExploreMode,
                13, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-418,0),new Vector2(128,34));
            crewMenuToggle = AddButton("ASTRONAUTAS", footer.transform, ToggleCrewMenu,
                14, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-554, 0), new Vector2(128, 34));

            constructionMenuRoot = Panel("Menú de construcción", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, Vector2.zero);
            constructionMenuRoot.GetComponent<Image>().raycastTarget = true;
            ApplyConstructionMenuLayout();
            Label("PLANOS Y ACCIONES", constructionMenuRoot.transform, 17, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(13, -7), new Vector2(-64, 28));
            AddButton("X", constructionMenuRoot.transform, CloseConstructionMenu, 15,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-8, -6), new Vector2(32, 29));
            CreateCategoryTabs(constructionMenuRoot.transform);
            actionDetailText = Label("Elegí un plano o arrastralo al mapa.", constructionMenuRoot.transform,
                11, FontStyles.Normal, SpaceFarmPalette.MutedText,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(13, -76), new Vector2(-26, 18));
            GameObject viewport = new GameObject("Action Cards Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(constructionMenuRoot.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(10, 8); viewportRect.offsetMax = new Vector2(-10, -100);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            ScrollRect actionScroll = viewport.GetComponent<ScrollRect>();
            actionScroll.horizontal = false; actionScroll.vertical = true;
            actionScroll.movementType = ScrollRect.MovementType.Clamped; actionScroll.scrollSensitivity = 34f;
            GameObject cards = new GameObject("Action Cards", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            cards.transform.SetParent(viewport.transform, false);
            actionCardsRoot = cards.GetComponent<RectTransform>();
            actionCardsRoot.anchorMin = new Vector2(0, 1);
            actionCardsRoot.anchorMax = new Vector2(1, 1);
            actionCardsRoot.pivot = new Vector2(.5f, 1);
            actionCardsRoot.anchoredPosition = Vector2.zero;
            actionCardsRoot.sizeDelta = new Vector2(-6, 0);
            actionScroll.content = actionCardsRoot;
            actionScroll.viewport = viewportRect;
            VerticalLayoutGroup cardLayout = cards.GetComponent<VerticalLayoutGroup>();
            cardLayout.spacing = 4;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            ContentSizeFitter actionContentSize = cards.GetComponent<ContentSizeFitter>();
            actionContentSize.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            actionContentSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddAction("Mina de hierro", () => $"{state.MineCost} Fe · {state.MineCount}/{state.IronDepositCount}", 0,
                null, () => state.PlacementAvailability(StructureKind.Mine), StructureKind.Mine,
                "Art/space-farm-iron-mine-animation#iron-mine-base-3d", "Extrae hierro del yacimiento elegido");
            AddAction("Extractor de agua", () => $"{state.ExtractorCost} Fe · {state.ExtractorCount}/{SpaceFarmState.MaxExtractors}", 0,
                null, () => state.PlacementAvailability(StructureKind.Extractor), StructureKind.Extractor,
                "Art/space-farm-world-modules#water-3d", "Convierte hielo en agua");
            AddAction("Invernadero", () => $"6 Fe · {state.GreenhouseCells.Count}/5", 0, null,
                () => state.PlacementAvailability(StructureKind.Greenhouse), StructureKind.Greenhouse,
                "Art/space-farm-world-modules#greenhouse-3d", "Produce alimentos con agua y energía");
            AddAction("Planta de O2", () => $"9 Fe · {state.OxygenPlants}/5", 0, null,
                () => state.PlacementAvailability(StructureKind.OxygenPlant), StructureKind.OxygenPlant,
                "Art/space-farm-world-modules#oxygen-3d", "Produce oxígeno para más colonos");
            AddAction("Almacén de recursos", () => $"12 Fe · capacidad {state.WarehouseCapacity} Fe", 0, null,
                () => state.PlacementAvailability(StructureKind.Warehouse), StructureKind.Warehouse,
                "Art/space-farm-warehouse", "Amplía el espacio para hierro y minerales");
            AddAction("Construir muros", () => "1 Fe por muro", 0, null,
                () => state.PlacementAvailability(StructureKind.Barrier), StructureKind.Barrier,
                "Art/space-farm-world-modules#habitat-3d", "Colocá muros en cualquier sector libre").BarrierMode = true;
            AddAction("Pedir a la Tierra", () => "4 hierro", 2, () => state.OrderFromEarth(out _),
                () => state.ShipmentArrivalSol != 0 ? "Ya hay un pedido en camino" : MissingIron(4),
                StructureKind.None, "Art/space-farm-world-modules#habitat-3d", "Recibe agua y alimentos");
            AddAction("Enviar rover al almacén", () => $"Carga {state.RoverCargoTotal}/{state.RoverCapacity} unidades", 2,
                () => { OrderCargoDelivery(); return true; },
                () => state.RoverCargoTotal <= 0 ? "El rover no lleva carga" : state.IsDamaged(DamagedStructureKind.Rover) ? "Repará el rover primero" :
                    state.WarehouseFree <= 0 ? "Almacén lleno: la carga queda en el rover" : string.Empty,
                StructureKind.None, "Art/space-farm-warehouse", "Descarga materiales y actualiza el almacén");
            AddAction("Recolección automática", () => state.AutomaticCollection ? "ACTIVADA · prioriza reservas vitales" : "DESACTIVADA · órdenes manuales",
                2, () => { state.AutomaticCollection = !state.AutomaticCollection; SetReport(state.AutomaticCollection ? "Recolección automática activada." : "Recolección automática pausada."); return true; },
                () => string.Empty, StructureKind.None, "Art/space-farm-rover-directions#rover-facing-south",
                "El rover recoge recursos locales al terminar su tarea");

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
            RefreshActionCards();
            SelectCategory(0);
            constructionMenuRoot.SetActive(false);
        }

        /// <summary>Mine an upgrade or repair card acts on: the selected mine first, then the nearest that qualifies.</summary>
        private int MineTarget()
        {
            SpaceFarmState.DepositState chosen = state.Deposit(selectedDeposit);
            if (selectedBuilding == "deposit" && chosen != null && Qualifies(chosen)) return chosen.Id;
            foreach (SpaceFarmState.DepositState deposit in state.Deposits)
                if (Qualifies(deposit)) return deposit.Id;
            return -1;
        }

        private static bool Qualifies(SpaceFarmState.DepositState deposit) => deposit.MineLevel == 1;

        private GameObject Panel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = panel.GetComponent<Image>();
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
                Button button = AddButton(captions[i], parent, () => SelectCategory(category),
                    13, SpaceFarmPalette.Button, SpaceFarmPalette.Text,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(12 + i * 117, -39), new Vector2(110, 29));
                button.transition = Selectable.Transition.None;
                categoryButtons.Add(button);
            }
        }

        private ActionCard AddAction(string title, Func<string> cost, int category, Func<bool> action,
            Func<string> lockReason, StructureKind placement = StructureKind.None, string iconPath = "", string description = "")
        {
            GameObject item = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            item.transform.SetParent(actionCardsRoot, false);
            item.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 56);
            item.GetComponent<LayoutElement>().preferredHeight = 56f;
            Image image = item.GetComponent<Image>();
            image.color = SpaceFarmPalette.Button;
            Button button = item.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            GameObject thumbnail = new GameObject("Miniatura", typeof(RectTransform), typeof(Image));
            thumbnail.transform.SetParent(item.transform, false);
            RectTransform thumbRect = thumbnail.GetComponent<RectTransform>();
            thumbRect.anchorMin = thumbRect.anchorMax = thumbRect.pivot = new Vector2(0, .5f);
            thumbRect.anchoredPosition = new Vector2(5, 0);
            thumbRect.sizeDelta = new Vector2(52, 48);
            Image icon = thumbnail.GetComponent<Image>();
            icon.sprite = LoadCatalogSprite(iconPath);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI titleText = Label(title, item.transform, 15, FontStyles.Bold,
                SpaceFarmPalette.Text, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(63, -2), new Vector2(-71, 19));
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 11;
            titleText.fontSizeMax = 15;
            TextMeshProUGUI descriptionText = Label(description, item.transform, 10, FontStyles.Normal,
                SpaceFarmPalette.MutedText, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(63, -20), new Vector2(-71, 15));
            descriptionText.enableAutoSizing = true;
            descriptionText.fontSizeMin = 8;
            descriptionText.fontSizeMax = 10;
            TextMeshProUGUI detailText = Label(cost(), item.transform, 11, FontStyles.Bold,
                SpaceFarmPalette.Ice, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(63, 2), new Vector2(-71, 18));
            detailText.textWrappingMode = TextWrappingModes.Normal;
            detailText.enableAutoSizing = true;
            detailText.fontSizeMin = 8;
            detailText.fontSizeMax = 11;

            ActionCard card = new ActionCard
            {
                Category = category, Root = item.transform, Background = image, Title = titleText,
                Detail = detailText, Cost = cost, Action = action, LockReason = lockReason,
                Placement = placement, Button = button, Icon = icon, Description = description
            };
            actionCards.Add(card);
            if (placement != StructureKind.None && placement != StructureKind.Barrier)
            {
                SpaceFarmBuildCardDrag drag = item.AddComponent<SpaceFarmBuildCardDrag>();
                drag.Begin = () => { if (card.Button.interactable) BeginStructurePlacement(card.Placement, true); };
                drag.End = DropCardPlacement;
            }
            button.onClick.AddListener(() =>
            {
                if (paused) return;
                StartCoroutine(AnimateButton(card.Root));
                if (state.GameOver)
                {
                    SetReport("La misión terminó. Abrí PAUSA (Esc) para reiniciar este mapa o empezar uno nuevo.");
                    return;
                }
                string reason = card.LockReason();
                if (!string.IsNullOrEmpty(reason))
                {
                    SetReport(reason + ". Costo: " + card.Cost() + ".");
                    return;
                }
                if (card.BarrierMode)
                {
                    CloseConstructionMenu();
                    BeginBarrierMode();
                    return;
                }
                if (card.Placement != StructureKind.None)
                {
                    BeginStructurePlacement(card.Placement);
                    return;
                }
                if (card.Category == 1)
                {
                    card.Action?.Invoke();
                    RefreshHud();
                    return;
                }
                bool success = card.Action();
                if (card.Title.text == "Recolección automática")
                {
                    SetReport(state.AutomaticCollection ? "Recolección automática activada." : "Recolección automática pausada.");
                    RefreshHud();
                    return;
                }
                SetReport(state.LastReport);
                if (success) FlashScreen(SpaceFarmPalette.Oxygen, .10f, .22f);
                RefreshWorld();
                RefreshHud();
            });
            return card;
        }

        private static Sprite LoadCatalogSprite(string path)
        {
            int separator = path.IndexOf('#');
            if (separator < 0) return Resources.Load<Sprite>(path);
            string spriteName = path.Substring(separator + 1);
            Sprite[] sprites = Resources.LoadAll<Sprite>(path.Substring(0, separator));
            foreach (Sprite sprite in sprites) if (sprite != null && sprite.name == spriteName) return sprite;
            return null;
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
            colors.normalColor = new Color(.9f, .9f, .9f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.72f, .72f, .72f, 1f);
            colors.selectedColor = new Color(.9f, .9f, .9f, 1f);
            colors.disabledColor = new Color(.55f, .55f, .55f, .7f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                StartCoroutine(AnimateButton(item.transform));
                callback();
            });
            TextMeshProUGUI text = Label(caption, item.transform, fontSize, FontStyles.Bold, textColor,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-8, -4));
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(9f, fontSize);
            text.fontSizeMax = fontSize;
            return button;
        }

        private void SelectCategory(int category)
        {
            selectedCategory = category;
            string[] hints =
            {
                "Clic: activar plano · arrastrar tarjeta: colocar.",
                "Mejorá la instalación seleccionada.",
                "Pedí suministros a la Tierra. La tripulación se administra en ASTRONAUTAS."
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
            EnsureUpgradeCatalog();
            foreach (ActionCard card in actionCards)
            {
                bool visible = card.Category == selectedCategory;
                card.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                string reason = card.LockReason();
                bool available = string.IsNullOrEmpty(reason);
                card.Button.interactable = available;
                card.Background.color = available ? SpaceFarmPalette.Button : SpaceFarmPalette.PanelRaised;
                card.Title.color = available ? SpaceFarmPalette.Text : SpaceFarmPalette.MutedText;
                card.Detail.color = available ? SpaceFarmPalette.Ice : SpaceFarmPalette.MutedText;
                card.Icon.color = available ? Color.white : new Color(.62f, .65f, .66f, .55f);
                card.Detail.text = available ? card.Cost() : card.Cost() + " · " + reason;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionCardsRoot);
        }

        private void EnsureUpgradeCatalog()
        {
            if (state == null || actionCardsRoot == null) return;
            var signature = new System.Text.StringBuilder();
            signature.Append(state.SolarLevel).Append('|').Append(state.RoverLevel).Append('|').Append(state.OxygenPlants)
                .Append('|').Append(state.HasUpgrade("solar", 0)).Append('|').Append(state.HasUpgrade("rover", 0))
                .Append('|').Append(state.GreenhouseCells.Count).Append('|').Append(state.WarehouseCount).Append('|');
            for (int i = 0; i < state.Deposits.Count; i++)
                signature.Append(state.Deposit(i).MineLevel).Append(':').Append(state.ExtractorLevel(i)).Append(':')
                    .Append(state.HasUpgrade("mine", i)).Append(':').Append(state.HasUpgrade("extractor", i)).Append('|');
            for (int i = 0; i < state.OxygenPlants; i++) signature.Append(state.OxygenLevel(i)).Append(':').Append(state.HasUpgrade("oxygen", i)).Append('|');
            for (int i = 0; i < state.GreenhouseCells.Count; i++) signature.Append(state.GreenhouseLevel(i)).Append(':').Append(state.HasUpgrade("greenhouse", i)).Append('|');
            for (int i = 0; i < state.WarehouseCount; i++) signature.Append(state.WarehouseLevel(i)).Append(':').Append(state.HasUpgrade("warehouse", i)).Append('|');
            string next = signature.ToString();
            if (next == upgradeCatalogSignature) return;
            upgradeCatalogSignature = next;
            for (int i = actionCards.Count - 1; i >= 0; i--)
            {
                if (actionCards[i].Category != 1) continue;
                if (actionCards[i].Root != null) Destroy(actionCards[i].Root.gameObject);
                actionCards.RemoveAt(i);
            }

            AddUpgradeEntry("Panel solar", state.SolarLevel, state.SolarUpgradeCost,
                state.SolarLevel < SpaceFarmState.MaxLevel, state.HasUpgrade("solar", 0), "solar", 0, "solar", 0,
                "Más generación y capacidad de batería", "Art/space-farm-world-modules#solar-3d");
            AddUpgradeEntry("Rover", state.RoverLevel, state.RoverUpgradeCost,
                state.RoverLevel < 2, state.HasUpgrade("rover", 0), "rover", 0, "rover", 0,
                "Más carga y radio de exploración", "Art/space-farm-rover-directions#rover-facing-south");
            for (int i = 0; i < state.OxygenPlants; i++)
                AddUpgradeEntry($"Planta O2 {i + 1}", state.OxygenLevel(i), state.OxygenUpgradeCost(i),
                    state.OxygenLevel(i) < SpaceFarmState.MaxLevel, state.HasUpgrade("oxygen", i), "oxygen", i, i == 0 ? "oxygen1" : "oxygen2", i == 0 ? 0 : i - 1,
                    "Aumenta producción local de oxígeno", "Art/space-farm-world-modules#oxygen-3d");
            for (int i = 0; i < state.GreenhouseCells.Count; i++)
                AddUpgradeEntry($"Invernadero {i + 1}", state.GreenhouseLevel(i), state.GreenhouseUpgradeCost(i),
                    state.GreenhouseLevel(i) < SpaceFarmState.MaxLevel, state.HasUpgrade("greenhouse", i), "greenhouse", i, "greenhouse", i,
                    "Aumenta producción local de comida", "Art/space-farm-world-modules#greenhouse-3d");
            for (int i = 0; i < state.WarehouseCount; i++)
                AddUpgradeEntry($"Almacén {i + 1}", state.WarehouseLevel(i), state.WarehouseUpgradeCost(i),
                    state.WarehouseLevel(i) < SpaceFarmState.MaxLevel, state.HasUpgrade("warehouse", i), "warehouse", i, "warehouse", i,
                    "Aumenta capacidad compartida", "Art/space-farm-warehouse");
            for (int i = 0; i < state.Deposits.Count; i++)
            {
                SpaceFarmState.DepositState d = state.Deposit(i);
                if (d.MineLevel > 0)
                    AddUpgradeEntry($"Mina {d.Name}", d.MineLevel, state.MineUpgradeCost(i), d.MineLevel < SpaceFarmState.MaxLevel,
                        state.HasUpgrade("mine", i), "mine", i, "deposit", i, "Aumenta extracción de hierro", "Art/space-farm-iron-mine-animation#iron-mine-base-3d");
                else if (d.Extractor)
                    AddUpgradeEntry($"Extractor {d.Name}", state.ExtractorLevel(i), state.ExtractorUpgradeCost(i), state.ExtractorLevel(i) < SpaceFarmState.MaxLevel,
                        state.HasUpgrade("extractor", i), "extractor", i, "deposit", i, "Aumenta extracción de agua", "Art/space-farm-world-modules#water-3d");
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionCardsRoot);
        }

        private void AddUpgradeEntry(string name, int level, int cost, bool belowMaximum, bool inProgress,
            string upgradeKind, int upgradeTarget, string building, int index, string benefit, string icon)
        {
            if (!belowMaximum) return;
            AddAction($"{name} · nivel {level} → {level + 1}",
                () => $"{cost} Fe · {state.UpgradeSecondsToDisplay(upgradeKind, upgradeTarget)} s",
                1,
                () =>
                {
                    bool selected = SelectWorldBuilding(building, index);
                    if (selected)
                    {
                        GameObject target = SelectedUpgradeTarget();
                        if (target != null) FocusWorld(target.transform.position);
                        SetReport($"{name}: {benefit}. Enviá el rover desde la ficha y confirmá al llegar.");
                    }
                    return selected;
                },
                () => inProgress ? "Mejora en curso" : string.Empty,
                StructureKind.None, icon, benefit + " · confirmar en la ficha del edificio");
        }

        private string MissingIron(int amount) => state.Iron < amount ? $"Faltan {amount - state.Iron} Fe" : string.Empty;

        private CrewJob SelectedWorkerJob(out int target)
        {
            target = -1;
            if (selectedBuilding == "deposit")
            {
                SpaceFarmState.DepositState deposit = state.Deposit(selectedDeposit);
                if (deposit == null) return CrewJob.None;
                target = selectedDeposit;
                return deposit.MineLevel > 0 ? CrewJob.Mine : deposit.Extractor ? CrewJob.Extractor : CrewJob.None;
            }
            if (selectedBuilding == "greenhouse") { target = selectedBuildingIndex; return CrewJob.Greenhouse; }
            if (selectedBuilding == "oxygen1") { target = 0; return CrewJob.Oxygen; }
            if (selectedBuilding == "oxygen2") { target = selectedBuildingIndex + 1; return CrewJob.Oxygen; }
            if (selectedBuilding == "solar") { target = 0; return CrewJob.Solar; }
            return CrewJob.None;
        }

        private void ToggleConstructionMenu()
        {
            if (constructionMenuOpen) CloseConstructionMenu();
            else OpenConstructionMenu();
        }

        private void OpenUpgradeMenu()
        {
            if (editMode) SetEditMode(false);
            OpenConstructionMenu();
            SelectCategory(1);
        }

        private void EnterExploreMode()
        {
            if (activeStructure != StructureKind.None) CancelPlacement();
            if (barrierMode) EndBarrierMode();
            if (editMode) SetEditMode(false);
            if (constructionMenuOpen) CloseConstructionMenu();
            if (crewMenuOpen) CloseCrewMenu();
            SetReport("Modo explorar · seleccioná una casilla para mover el rover.");
        }

        private void OpenConstructionMenu()
        {
            if (paused || titleScreenOpen) return;
            if (editMode) SetEditMode(false);
            if (crewMenuOpen) CloseCrewMenu();
            if (barrierMode) EndBarrierMode();
            if (activeStructure != StructureKind.None) CancelPlacement(false);
            if (infoPanel != null) infoPanel.SetActive(false);
            leftPressValid = false;
            constructionMenuOpen = true;
            SetNavigationButtonsVisible(false);
            ApplyConstructionMenuLayout();
            constructionMenuRoot.SetActive(true);
            constructionMenuRoot.transform.SetAsLastSibling();
            RefreshActionCards();
            constructionMenuToggle.GetComponentInChildren<TextMeshProUGUI>().text = "CERRAR MENÚ";
        }

        private void CloseConstructionMenu()
        {
            constructionMenuOpen = false;
            if (constructionMenuRoot != null) constructionMenuRoot.SetActive(false);
            SetNavigationButtonsVisible(true);
            if (constructionMenuToggle != null)
                constructionMenuToggle.GetComponentInChildren<TextMeshProUGUI>().text = "CONSTRUIR";
            if (infoPanel != null && selectedBuilding != null && activeStructure == StructureKind.None)
            {
                infoPanel.SetActive(true);
                RefreshSystemsHud();
            }
        }

        private void ApplyConstructionMenuLayout()
        {
            if (constructionMenuRoot == null) return;
            RectTransform rect = constructionMenuRoot.GetComponent<RectTransform>();
            bool dockLeft = minimapRight;
            rect.anchorMin = new Vector2(dockLeft ? 0 : 1, 0);
            rect.anchorMax = new Vector2(dockLeft ? 0 : 1, 1);
            rect.offsetMin = new Vector2(dockLeft ? 12 : -382, FooterHeight + 10);
            rect.offsetMax = new Vector2(dockLeft ? 382 : -12, -(HeaderHeight + 10));
        }

        private void SetNavigationButtonsVisible(bool visible)
        {
            if (baseNavButton != null) baseNavButton.gameObject.SetActive(visible);
            if (roverNavButton != null) roverNavButton.gameObject.SetActive(visible);
            if (followButton != null) followButton.gameObject.SetActive(visible);
        }

        private void RefreshHud()
        {
            if (statsText == null) return;
            statsText.text = $"SOL <b>{state.Sol:00}</b>";
            roverText.text = collectionActive
                ? $"ROVER {state.RoverLevel} · LOGÍSTICA\nCarga {state.RoverCargoTotal}/{state.RoverCapacity} · Fe {state.RoverCargo} O2 {state.RoverOxygenCargo}\nAg {state.RoverWaterCargo} Co {state.RoverFoodCargo}"
                : travel != null
                    ? $"ROVER {state.RoverLevel} · EN RUTA\nSector {Sector(roverDestination)}\nAlmacén {state.WarehouseUsed}/{state.WarehouseCapacity}"
                    : $"ROVER {state.RoverLevel} · LISTO\nRadio {state.RoverRange} · visión {state.ScanRadius}\nAlmacén {state.WarehouseUsed}/{state.WarehouseCapacity}";
            if (state.GameOver)
                objectiveText.text = "MISIÓN TERMINADA · Recursos vitales agotados. Abrí PAUSA (Esc) para reiniciar este mapa o probar otro.";
            else if (state.MissionComplete)
                objectiveText.text = "MISIÓN COMPLETA · Primera cosecha y colonia protegida tras la incursión.";
            else objectiveText.text = "SIGUIENTE PASO  ·  " + NextObjective();
            if (editModeToggle != null)
            {
                editModeToggle.GetComponent<Image>().color = editMode ? SpaceFarmPalette.Iron : SpaceFarmPalette.PanelRaised;
                TextMeshProUGUI editLabel = editModeToggle.GetComponentInChildren<TextMeshProUGUI>();
                if (editLabel != null) editLabel.color = editMode ? SpaceFarmPalette.Background : SpaceFarmPalette.Text;
            }
            RefreshActionCards();
            RefreshResourceHud();
            RefreshSystemsHud();
            if (crewMenuOpen) RefreshCrewMenu();
        }

        private void CreateResourceHud(Transform header)
        {
            string[] labels = { "Hierro", "Energía", "Oxígeno", "Agua", "Comida", "Colonos", "Salud" };
            Color[] colors =
            {
                SpaceFarmPalette.Iron, SpaceFarmPalette.Ice, SpaceFarmPalette.Oxygen,
                SpaceFarmPalette.Ice, new Color(.63f,.82f,.48f), SpaceFarmPalette.Text,
                SpaceFarmPalette.Danger
            };
            for (int i = 0; i < labels.Length; i++)
            {
                GameObject chip = new GameObject(labels[i], typeof(RectTransform));
                chip.transform.SetParent(header, false);
                RectTransform rect = chip.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(80 + i * 131, 4);
                rect.sizeDelta = new Vector2(126, 48);
                Image card = chip.AddComponent<Image>();
                card.color = SpaceFarmPalette.PanelRaised;
                card.raycastTarget = false;
                resourceChips.Add(rect);
                GameObject icon = new GameObject(labels[i] + " icono", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(chip.transform, false);
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0, 1);
                iconRect.anchorMax = new Vector2(0, 1);
                iconRect.pivot = new Vector2(0, 1);
                iconRect.anchoredPosition = new Vector2(5, -9);
                iconRect.sizeDelta = new Vector2(30, 30);
                Image image = icon.GetComponent<Image>();
                image.sprite = ResourceIcon(i);
                image.color = Color.white;
                image.raycastTarget = false;
                TextMeshProUGUI text = Label("", chip.transform, 10, FontStyles.Bold, SpaceFarmPalette.Text,
                    new Vector2(0,1),new Vector2(1,1),new Vector2(0,1),new Vector2(41,-5),new Vector2(-8,17));
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = true;
                text.fontSizeMin = 8;
                text.fontSizeMax = 10;
                resourceLabels.Add(text);

                TextMeshProUGUI amount = Label("", chip.transform, 10, FontStyles.Bold, colors[i],
                    new Vector2(0,1),new Vector2(1,1),new Vector2(0,1),new Vector2(41,-21),new Vector2(-8,16));
                amount.alignment = TextAlignmentOptions.TopLeft;
                amount.textWrappingMode = TextWrappingModes.NoWrap;
                amount.enableAutoSizing = true;
                amount.fontSizeMin = 8;
                amount.fontSizeMax = 10;
                resourceAmounts.Add(amount);

                GameObject barBack = new GameObject("Almacén", typeof(RectTransform), typeof(Image));
                barBack.transform.SetParent(chip.transform, false);
                RectTransform barRect = barBack.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0, 0);
                barRect.anchorMax = new Vector2(1, 0);
                barRect.pivot = new Vector2(.5f, 0);
                barRect.anchoredPosition = new Vector2(0, 7);
                barRect.sizeDelta = new Vector2(-14, 7);
                Image barBackImage = barBack.GetComponent<Image>();
                barBackImage.color = SpaceFarmPalette.Background;
                barBackImage.raycastTarget = false;
                GameObject fill = new GameObject("Ocupación", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(barBack.transform, false);
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.pivot = new Vector2(0, .5f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                Image fillImage = fill.GetComponent<Image>();
                fillImage.color = colors[i];
                fillImage.raycastTarget = false;
                resourceBarRects.Add(fillRect);
            }
        }

        private Sprite MakeResourceIcon(int kind)
        {
            Texture2D texture = new Texture2D(20, 20, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[400];
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
            {
                bool on = false;
                switch (kind)
                {
                    case 0: // Ingot of iron
                        on = (y >= 5 && y <= 15 && x >= (y < 9 ? 5 : 3) && x <= (y < 9 ? 14 : 16)) ||
                            (y == 4 && x >= 6 && x <= 13) || (y == 8 && x >= 4 && x <= 15); break;
                    case 1: // Energy bolt
                        on = (y >= 2 && y <= 10 && x >= 8 - (y - 2) * .45f && x <= 14 - (y - 2) * .15f) ||
                            (y >= 9 && y <= 18 && x >= 6 + (y - 9) * .18f && x <= 12 + (y - 9) * .5f); break;
                    case 2: // O2 molecule
                        on = (Mathf.Abs(Mathf.Pow(x - 7, 2) + Mathf.Pow(y - 10, 2) - 16) <= 4) ||
                            (Mathf.Abs(Mathf.Pow(x - 14, 2) + Mathf.Pow(y - 6, 2) - 9) <= 4) ||
                            (x >= 8 && x <= 12 && y >= 8 && y <= 10); break;
                    case 3: // Water droplet
                        on = y >= 4 && y <= 17 && Mathf.Abs(x - 10) <= (y < 9 ? (y - 3) * .72f : (17 - y) * .8f); break;
                    case 4: // Green leaf and vein
                        on = y >= 3 && y <= 17 && Mathf.Abs(x - (8 + (y - 3) * .28f)) <= (y < 10 ? (y - 2) * .42f : (18 - y) * .42f) ||
                            (y >= 7 && y <= 14 && x == 9); break;
                    case 5: // Two colonists
                        on = (Mathf.Pow(x - 7, 2) + Mathf.Pow(y - 5, 2) <= 5) ||
                            (Mathf.Pow(x - 14, 2) + Mathf.Pow(y - 7, 2) <= 4) ||
                            (y >= 9 && y <= 17 && x >= 4 && x <= 9) ||
                            (y >= 11 && y <= 18 && x >= 11 && x <= 16); break;
                    case 6: // Medical cross
                        on = (x >= 8 && x <= 11 && y >= 3 && y <= 17) ||
                            (y >= 8 && y <= 11 && x >= 3 && x <= 16); break;
                }
                pixels[y * 20 + x] = on ? new Color32(255,255,255,255) : new Color32(0,0,0,0);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0,0,20,20), new Vector2(.5f,.5f),20);
            runtimeAssets.Add(sprite);
            runtimeAssets.Add(texture);
            return sprite;
        }

        private Sprite ResourceIcon(int kind)
        {
            if (resourceIconSprites == null)
                resourceIconSprites = Resources.LoadAll<Sprite>("Art/space-farm-resource-icons");
            string[] names = { "icon-iron", "icon-energy", "icon-oxygen", "icon-water", "icon-food", "icon-colonists", "icon-health" };
            for (int i = 0; i < resourceIconSprites.Length; i++)
                if (resourceIconSprites[i] != null && resourceIconSprites[i].name == names[kind])
                    return resourceIconSprites[i];
            return MakeResourceIcon(kind);
        }

        private void RefreshResourceHud()
        {
            if (resourceLabels.Count == 0 || state == null) return;
            int[] now = { state.Iron, state.Energy, state.Oxygen, state.Water, state.Food, state.Population, state.Health };
            string[] names = { "Hierro", "Energía", "Oxígeno", "Agua", "Comida", "Colonos", "Salud" };
            int[] capacities = { state.WarehouseCapacity, state.EnergyCapacity, 60, 60, 60, state.HabitatCapacity, 3 };
            for (int i = 0; i < now.Length; i++)
            {
                bool critical = (i == 2 || i == 3 || i == 4) && now[i] <= state.Population * 2;
                resourceLabels[i].text = names[i];
                resourceAmounts[i].text = $"{now[i]} / {capacities[i]}";
                resourceAmounts[i].color = critical ? SpaceFarmPalette.Danger : colorsForResource(i);
                resourceBarRects[i].anchorMax = new Vector2(Mathf.Clamp01(now[i] / (float)capacities[i]), 1f);
                if (previousResources != null && now[i] != previousResources[i])
                    StartCoroutine(ResourceDelta(resourceChips[i], now[i] - previousResources[i]));
            }
            previousResources = now;
        }

        private static Color colorsForResource(int index)
        {
            switch (index)
            {
                case 0: return SpaceFarmPalette.Iron;
                case 1: return SpaceFarmPalette.Ice;
                case 2: return SpaceFarmPalette.Oxygen;
                case 3: return SpaceFarmPalette.Ice;
                case 4: return SpaceFarmPalette.Greenhouse;
                case 5: return SpaceFarmPalette.Text;
                default: return SpaceFarmPalette.Danger;
            }
        }

        private IEnumerator ResourceDelta(RectTransform chip, int delta)
        {
            TextMeshProUGUI popup = Label((delta > 0 ? "+" : "") + delta, chip, 12, FontStyles.Bold,
                delta > 0 ? SpaceFarmPalette.Oxygen : SpaceFarmPalette.Danger,
                new Vector2(0,0),new Vector2(0,0),new Vector2(0,0),new Vector2(23,20),new Vector2(55,18));
            float elapsed = 0f;
            while (elapsed < .7f)
            {
                elapsed += Time.unscaledDeltaTime;
                popup.rectTransform.anchoredPosition = new Vector2(23, 20 + elapsed * 17f);
                Color color = popup.color;
                color.a = 1f - Mathf.Clamp01(elapsed / .7f);
                popup.color = color;
                yield return null;
            }
            Destroy(popup.gameObject);
        }

        private void InitializeSystems()
        {
            Transform canvas = hudCanvas.transform;
            baseNavButton = AddButton("VOLVER A LA BASE", canvas, () => { SetFollow(false); FocusWorld(CellPosition(BaseCell)); }, 12,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),
                new Vector2(15,-106),new Vector2(170,31));
            roverNavButton = AddButton("IR AL ROVER", canvas, () => FocusWorld(rover.transform.position), 12,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),
                new Vector2(15,-143),new Vector2(98,31));
            followButton = AddButton("SEGUIR", canvas, () => SetFollow(!followRover), 11,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),
                new Vector2(119,-143),new Vector2(66,31));

            buildModePanel = Panel("Modo construcción", canvas, SpaceFarmPalette.PanelRaised,
                new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(.5f,1),
                new Vector2(0,-105),new Vector2(480,54));
            buildModePanel.GetComponent<Image>().raycastTarget = true;
            buildModeText = Label("", buildModePanel.transform, 15, FontStyles.Bold, SpaceFarmPalette.Iron,
                new Vector2(0,0),new Vector2(0,1),new Vector2(0,.5f),new Vector2(10,0),new Vector2(350,-8));
            AddButton("CANCELAR", buildModePanel.transform,
                () => { if (barrierMode) EndBarrierMode(); else CancelPlacement(); }, 12,
                SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),
                new Vector2(-9,0),new Vector2(106,35));
            buildModePanel.SetActive(false);

            CreateEducationalInfoCard(canvas);
            infoPanel.SetActive(false);
            RefreshFollowButton();
        }

        private void SetFollow(bool value)
        {
            followRover = value;
            RefreshFollowButton();
            if (value) SetReport("La cámara sigue al rover. Mover la cámara o pulsar SEGUIR otra vez la libera.");
        }

        private void RefreshFollowButton()
        {
            if (followButton == null) return;
            followButton.GetComponent<Image>().color = followRover ? SpaceFarmPalette.Iron : SpaceFarmPalette.PanelRaised;
            TextMeshProUGUI label = followButton.GetComponentInChildren<TextMeshProUGUI>();
            label.color = followRover ? SpaceFarmPalette.Background : SpaceFarmPalette.Text;
            label.text = followRover ? "SIGUIENDO" : "SEGUIR";
        }

        private string SummarizeSol()
        {
            SpaceFarmState.SolResults sol = state.LastSol;
            string text = $"Sol {state.Sol:00}: solar +{sol.Solar.Actual}, O2 +{sol.OxygenOne.Actual + sol.OxygenTwo.Actual}, " +
                $"agua +{sol.Water.Actual}, comida +{sol.Greenhouse.Actual}, minas +{sol.Mine.Actual} Fe almacenado. " +
                $"Tripulación -{sol.CrewConsumed} recursos vitales.";
            if (sol.Storm) text += " Tormenta de polvo.";
            if (sol.DamagedStructure != DamagedStructureKind.None)
                text += $" La tormenta averió {state.DamagedStructureName(sol.DamagedStructure, sol.DamagedTarget)}. Reparación: {state.RepairIronCost} Fe y {state.RepairEnergyCost} energía.";
            if (sol.ShipmentDelivered) text += " Llegó la nave de suministros: +5 agua y +5 comida.";
            if (sol.RaidFoodLoss > 0)
                text += $" Incursión: -{sol.RaidFoodLoss} comida. Los muros cercanos al hábitat reducen la pérdida.";
            if (state.GameOver) text += " Misión perdida.";
            else if (state.MissionComplete) text += " ¡Misión completa!";
            return text;
        }

        private void BeginBarrierMode()
        {
            CancelPlacement(false);
            CloseConstructionMenu();
            barrierMode = true;
            interactionMode = InteractionMode.PlacingBarrier;
            HideWorldSelection();
            buildModePanel.SetActive(true);
            SetFollow(false);
            FocusWorld(CellPosition(BaseCell));
            RefreshSystems();
            SetReport("Elegí sectores libres para colocar muros por 1 Fe cada uno. Los tramos vecinos se unen solos; Escape termina.");
        }

        private void EndBarrierMode()
        {
            barrierMode = false;
            interactionMode = InteractionMode.Explore;
            if (barrierPreview != null) barrierPreview.SetActive(false);
            if (buildModePanel != null) buildModePanel.SetActive(false);
            RefreshSystems();
            SetReport($"Construcción terminada. Muros colocados: {state.BarrierCount}.");
        }

        private bool SelectWorldBuilding(string building, int index = 0)
        {
            leftPressValid = false;
            selectedBuilding = building;
            selectedBuildingIndex = index;
            selectedDeposit = -1;
            EnsureInfoCardUsesCanvasRect();
            infoPanel.SetActive(true);
            infoPanel.transform.SetAsLastSibling();
            RefreshHud();
            return true;
        }

        private bool SelectDeposit(int depositId)
        {
            leftPressValid = false;
            selectedBuilding = "deposit";
            selectedDeposit = depositId;
            EnsureInfoCardUsesCanvasRect();
            infoPanel.SetActive(true);
            infoPanel.transform.SetAsLastSibling();
            RefreshHud();
            return true;
        }

        private void HideWorldSelection()
        {
            bool changed = selectedBuilding != null;
            selectedBuilding = null;
            selectedDeposit = -1;
            if (infoPanel != null) infoPanel.SetActive(false);
            ClearEducationalMetricCards();
            if (changed && actionCardsRoot != null) RefreshActionCards();
        }

        private void ContextPrimaryAction()
        {
            if (paused || state == null) return;
            if (editMode) { BeginEditMoveSelected(); return; }
            SpaceFarmState.DepositState deposit = state.Deposit(selectedDeposit);
            if (selectedBuilding == "rover") return;
            if (selectedBuilding == "warehouse") { OrderCargoDelivery(); RefreshHud(); return; }
            if (selectedBuilding == "oxygen1" || selectedBuilding == "oxygen2")
            {
                StartResourceCollection(MaterialResource.Oxygen, selectedBuilding == "oxygen1" ? 0 : selectedBuildingIndex + 1);
                RefreshHud(); return;
            }
            if (selectedBuilding == "greenhouse") { StartResourceCollection(MaterialResource.Food, selectedBuildingIndex); RefreshHud(); return; }
            if (selectedBuilding != "deposit" || deposit == null) return;
            int id = deposit.Id;
            if (deposit.MineLevel > 0) StartMineCollection(id);
            else if (deposit.Extractor) StartResourceCollection(MaterialResource.Water, id);
            else BeginStructurePlacement(deposit.Kind == DepositKind.Iron ? StructureKind.Mine : StructureKind.Extractor);
        }

        private void ContextSecondaryAction()
        {
            if (paused || state == null) return;
            if (editMode) { ConfirmDeleteSelected(); return; }
            if (selectedBuilding == "rover") { BeginSelectedUpgrade(() => Act((out string message) => state.UpgradeRover(out message))); return; }
            if (selectedBuilding == "solar") { BeginSelectedUpgrade(() => Act((out string message) => state.UpgradeSolar(out message))); return; }
            if (selectedBuilding == "oxygen1" || selectedBuilding == "oxygen2")
            {
                int target = selectedBuilding == "oxygen1" ? 0 : selectedBuildingIndex + 1;
                BeginSelectedUpgrade(() => Act((out string message) => state.UpgradeOxygen(target, out message))); return;
            }
            if (selectedBuilding == "greenhouse")
            {
                int target = selectedBuildingIndex;
                BeginSelectedUpgrade(() => Act((out string message) => state.UpgradeGreenhouse(target, out message))); return;
            }
            if (selectedBuilding == "warehouse")
            {
                int target = selectedBuildingIndex;
                BeginSelectedUpgrade(() => Act((out string message) => state.UpgradeWarehouse(target, out message))); return;
            }
            SpaceFarmState.DepositState deposit = state.Deposit(selectedDeposit);
            if (selectedBuilding != "deposit" || deposit == null) return;
            int id = deposit.Id;
            if (deposit.MineLevel > 0) BeginSelectedUpgrade(() => Act((out string message) => state.UpgradeMine(id, out message)));
            else if (deposit.Extractor) BeginSelectedUpgrade(() => Act((out string message) => state.UpgradeExtractor(id, out message)));
        }

        private void RefreshSystemsHud()
        {
            if (state == null) return;
            if (buildModeText != null && barrierMode)
                buildModeText.text = $"MURO · 1 Fe\nMuros {state.BarrierCount} · Hierro {state.Iron} · Unión automática";
            if (defenseText != null)
            {
                defenseText.text = state.ColonyLevel >= SpaceFarmState.MaxLevel
                    ? $"NIVEL {state.ColonyLevel} · MÁXIMO\nXP {state.Xp}"
                    : $"NIVEL {state.ColonyLevel}/5 · XP {state.Xp}/{state.XpToNext}";
                if (xpBarFill != null) xpBarFill.sizeDelta = new Vector2(XpBarWidth * state.ColonyLevelProgress, 0);
            }
            if (infoPanel != null && !string.IsNullOrEmpty(selectedBuilding)) RefreshEducationalCard();
        }

        private string ResultText(SpaceFarmState.SystemResult result, string status, int nominal, int expectedEnergy, string extra)
        {
            if (status == "Operativa" && result.Nominal == 0 && state.Sol > 1) status = "Sin ciclo todavía";
            else if (status == "Operativa" && result.Nominal > 0 && result.Actual == 0 && state.Sol > 1) status = "Detenida";
            return $"Estado: {status}\nNominal: {(result.Nominal > 0 ? result.Nominal : nominal)}/sol  ·  Real último: {result.Actual}\nEnergía: {expectedEnergy}/sol (usó {result.EnergyUsed})\n{extra}\n{result.Reason}";
        }

        /// <summary>Card of the selected deposit: its own survey, mine or extractor data, never a shared total.</summary>
        private void RefreshDepositCard()
        {
            SpaceFarmState.DepositState deposit = state.Deposit(selectedDeposit);
            if (deposit == null) { HideWorldSelection(); return; }
            bool iron = deposit.Kind == DepositKind.Iron;
            bool busy = collectionActive || travel != null || state.GameOver;
            string place = $"A {deposit.Distance} sectores, al {SpaceFarmMap.Compass(BaseCell, map.Deposits[deposit.Id].Cell)} de la base.";
            TextMeshProUGUI primaryLabel = infoPrimary.GetComponentInChildren<TextMeshProUGUI>();
            infoSecondary.gameObject.SetActive(false);
            if (deposit.MineLevel > 0)
            {
                infoTitle.text = $"Mina {deposit.Name} · nivel {deposit.MineLevel}";
                bool damaged = state.IsDamaged(DamagedStructureKind.Mine, deposit.Id);
                string status = damaged ? "Averiada" : deposit.MineStored == deposit.MineCapacity ? "Almacén lleno" : "Operativa";
                string reason = damaged ? $"Averiada: reparar por {state.RepairIronCost} Fe y {state.RepairEnergyCost} energía." : deposit.LastResult.Reason;
                int mineNominal = deposit.MineNominal + state.ActiveWorkers(CrewJob.Mine, deposit.Id) * SpaceFarmState.WorkerBonus(CrewJob.Mine);
                infoBody.text = $"Estado: {status}\nNominal: {mineNominal}/sol  ·  Real último: {deposit.LastResult.Actual}\n" +
                    $"Energía: {deposit.MineEnergy}/sol (usó {deposit.LastResult.EnergyUsed})\n" +
                    $"Almacén {deposit.MineStored}/{deposit.MineCapacity} Fe  ·  Rover {state.RoverCargo}/{state.RoverCapacity} Fe\n{place}\n{reason}";
                SpaceFarmState.CrewMember assigned = state.WorkerOn(CrewJob.Mine, deposit.Id);
                infoBody.text += assigned == null ? "\nSin trabajador. Abrí ASTRONAUTAS." :
                    $"\nAstronauta {assigned.Id}: {(assigned.Resting ? "descansando" : "trabajando")} · energía {assigned.Energy:0} % · bono +{SpaceFarmState.WorkerBonus(CrewJob.Mine)}/sol.";
                infoPrimary.gameObject.SetActive(true);
                primaryLabel.text = "RECOGER";
                infoPrimary.interactable = deposit.MineStored > 0 && !busy;
                infoSecondary.gameObject.SetActive(deposit.MineLevel < 2);
                infoSecondary.interactable = !state.GameOver;
                infoSecondary.GetComponentInChildren<TextMeshProUGUI>().text = $"MEJORAR · {state.MineUpgradeCost(deposit.Id)} · {state.UpgradeSecondsToDisplay("mine", deposit.Id)} s";
            }
            else if (deposit.Extractor)
            {
                infoTitle.text = $"Extractor {deposit.Name}";
                int waterNominal = 8 + state.ActiveWorkers(CrewJob.Extractor, deposit.Id) * SpaceFarmState.WorkerBonus(CrewJob.Extractor);
                infoBody.text = ResultText(deposit.LastResult, "Operativa", waterNominal, 2, $"Agua {state.Water}/60\n{place}");
                infoPrimary.gameObject.SetActive(false);
            }
            else if (iron)
            {
                infoTitle.text = $"Yacimiento {deposit.Name}";
                string blocker = state.PlacementBlocker(StructureKind.Mine, map.Deposits[deposit.Id].Cell,
                    IsRevealed(map.Deposits[deposit.Id].Cell));
                infoBody.text = $"Hierro confirmado. {place}\nMina: {state.MineCost} Fe · produce 5 Fe/sol · usa 2 de energía.\n" +
                    $"Minas activas {state.MineCount}/{state.IronDepositCount} yacimientos de hierro de este mapa. No requiere operador para construirse; un astronauta asignado aumenta la producción. Cada mina nueva cuesta 4 Fe más.\n" +
                    (blocker.Length > 0 ? blocker + "." : "Lista para construir.");
                infoPrimary.gameObject.SetActive(true);
                primaryLabel.text = $"COLOCAR MINA · {state.MineCost}";
                infoPrimary.interactable = blocker.Length == 0 && !state.GameOver;
            }
            else
            {
                infoTitle.text = $"Depósito {deposit.Name}";
                string blocker = state.PlacementBlocker(StructureKind.Extractor, map.Deposits[deposit.Id].Cell,
                    IsRevealed(map.Deposits[deposit.Id].Cell));
                infoBody.text = $"Hielo confirmado. {place}\nExtractor: {state.ExtractorCost} Fe · produce 8 de agua/sol · usa 2 de energía.\n" +
                    $"Extractores {state.ExtractorCount}/{SpaceFarmState.MaxExtractors}: el segundo cuesta 4 Fe más y el agua nunca supera 30.\n" +
                    (blocker.Length > 0 ? blocker + "." : "Listo para construir.");
                infoPrimary.gameObject.SetActive(true);
                primaryLabel.text = $"COLOCAR EXTRACTOR · {state.ExtractorCost}";
                infoPrimary.interactable = blocker.Length == 0 && !state.GameOver;
            }
        }
    }
}

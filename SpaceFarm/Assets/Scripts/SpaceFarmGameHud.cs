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
        private GameObject infoPanel;
        private TextMeshProUGUI infoTitle;
        private TextMeshProUGUI infoBody;
        private Button infoPrimary;
        private Button infoSecondary;
        private Button followButton;
        private Button baseNavButton;
        private Button roverNavButton;
        private string selectedBuilding;
        private int selectedDeposit = -1;
        private GameObject constructionMenuRoot;
        private bool constructionMenuOpen;
        private Button constructionMenuToggle;

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
                new Vector2(712, -12), new Vector2(132, 48));
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
            AddButton("II  PAUSA", header.transform, () => SetPaused(true), 12, SpaceFarmPalette.PanelRaised,
                SpaceFarmPalette.Text, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-344, -17), new Vector2(86, 48));

            GameObject footer = Panel("Barra de mensajes", canvasObject.transform, SpaceFarmPalette.Panel,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), Vector2.zero, new Vector2(0, FooterHeight));
            footer.GetComponent<Image>().raycastTarget = true;
            reportText = Label("", footer.transform, 14, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(18, -5), new Vector2(-185, 34));
            reportText.enableAutoSizing = true;
            reportText.fontSizeMin = 10;
            reportText.fontSizeMax = 14;
            constructionMenuToggle = AddButton("CONSTRUIR", footer.transform, ToggleConstructionMenu,
                14, SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-10, 0), new Vector2(152, 34));

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
            GameObject cards = new GameObject("Action Cards", typeof(RectTransform), typeof(VerticalLayoutGroup));
            cards.transform.SetParent(constructionMenuRoot.transform, false);
            actionCardsRoot = cards.GetComponent<RectTransform>();
            actionCardsRoot.anchorMin = new Vector2(0, 0);
            actionCardsRoot.anchorMax = new Vector2(1, 1);
            actionCardsRoot.offsetMin = new Vector2(10, 8);
            actionCardsRoot.offsetMax = new Vector2(-10, -100);
            VerticalLayoutGroup cardLayout = cards.GetComponent<VerticalLayoutGroup>();
            cardLayout.spacing = 4;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            AddAction("Mina de hierro", () => $"{state.MineCost} Fe · {state.MineCount}/{state.MaxMines}", 0,
                null, () => state.PlacementAvailability(StructureKind.Mine), StructureKind.Mine,
                "Art/space-farm-iron-mine-animation#iron-mine-base-3d", "Extrae hierro del yacimiento elegido");
            AddAction("Extractor de agua", () => $"{state.ExtractorCost} Fe · {state.ExtractorCount}/{SpaceFarmState.MaxExtractors}", 0,
                null, () => state.PlacementAvailability(StructureKind.Extractor), StructureKind.Extractor,
                "Art/space-farm-world-modules#water-3d", "Convierte hielo en agua");
            AddAction("Invernadero", () => "6 Fe · una unidad", 0, null,
                () => state.PlacementAvailability(StructureKind.Greenhouse), StructureKind.Greenhouse,
                "Art/space-farm-world-modules#greenhouse-3d", "Produce alimentos con agua y energía");
            AddAction("Planta de O2", () => "9 Fe · segunda planta", 0, null,
                () => state.PlacementAvailability(StructureKind.OxygenPlant), StructureKind.OxygenPlant,
                "Art/space-farm-world-modules#oxygen-3d", "Produce oxígeno para más colonos");
            AddAction("Construir muros", () => "1 Fe por muro · hasta 12", 0, null,
                () => state.PlacementAvailability(StructureKind.Barrier), StructureKind.Barrier,
                "Art/space-farm-world-modules#habitat-3d", "Colocá muros en cualquier sector libre").BarrierMode = true;
            AddAction("Mejorar mina", () => "6 Fe", 1, () => state.UpgradeMine(MineTarget(), out _),
                () => state.MineCount == 0 ? "Construí primero una mina" : MineTarget() < 0 ? "No hay minas para mejorar (nivel máximo)" : MissingIron(6),
                StructureKind.None, "Art/space-farm-iron-mine-animation#iron-mine-base-3d", "Sube una mina al nivel 2");
            AddAction("Mejorar rover", () => "5 hierro", 1, () => state.UpgradeRover(out _),
                () => state.RoverLevel >= 2 ? "Nivel máximo de la demo" : MissingIron(5),
                StructureKind.None, "Art/space-farm-rover-directions#rover-facing-south", "Amplía alcance y capacidad");
            AddAction("Recibir colono", () => "reservas vitales", 2, () => state.AddColonist(out _),
                () => state.Population >= 4 ? "El hábitat está completo" : state.OxygenPerSol < (state.Population + 1) * 1.25f ? "Aumentá O2 y conservá un 25 % de reserva" : state.Oxygen < 8 || state.Water < 8 || state.Food < 8 ? "Necesitás al menos 8 de O2, agua y comida" : string.Empty,
                StructureKind.None, "Art/space-farm-colonist-directions#colonist-facing-south", "Más manos para operar minas");
            AddAction("Pedir a la Tierra", () => "4 hierro", 2, () => state.OrderFromEarth(out _),
                () => state.ShipmentArrivalSol != 0 ? "Ya hay un pedido en camino" : MissingIron(4),
                StructureKind.None, "Art/space-farm-world-modules#habitat-3d", "Recibe agua y alimentos");

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
                bool success = card.Action();
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
                "Cada colono extra permite operar una mina más, pero consume O2, agua y comida."
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
                card.Button.interactable = available;
                card.Background.color = available ? SpaceFarmPalette.Button : SpaceFarmPalette.PanelRaised;
                card.Title.color = available ? SpaceFarmPalette.Text : SpaceFarmPalette.MutedText;
                card.Detail.color = available ? SpaceFarmPalette.Ice : SpaceFarmPalette.MutedText;
                card.Icon.color = available ? Color.white : new Color(.62f, .65f, .66f, .55f);
                card.Detail.text = available ? card.Cost() : card.Cost() + " · " + reason;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionCardsRoot);
        }

        private string MissingIron(int amount) => state.Iron < amount ? $"Faltan {amount - state.Iron} Fe" : string.Empty;

        private void ToggleConstructionMenu()
        {
            if (constructionMenuOpen) CloseConstructionMenu();
            else OpenConstructionMenu();
        }

        private void OpenConstructionMenu()
        {
            if (paused || titleScreenOpen) return;
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
                ? $"ROVER {state.RoverLevel} · RECOGIDA\nCarga {state.RoverCargo}/{state.RoverCapacity} Fe"
                : travel != null
                    ? $"ROVER {state.RoverLevel} · EN RUTA\nSector {Sector(roverDestination)}"
                    : $"ROVER {state.RoverLevel} · LISTO\nSector {Sector(roverCell)} · alcance {state.ScanRadius}";
            if (state.GameOver)
                objectiveText.text = "MISIÓN TERMINADA · Recursos vitales agotados. Abrí PAUSA (Esc) para reiniciar este mapa o probar otro.";
            else if (state.MissionComplete)
                objectiveText.text = "MISIÓN COMPLETA · Primera cosecha y colonia protegida tras la incursión.";
            else objectiveText.text = "SIGUIENTE PASO  ·  " + NextObjective();
            RefreshActionCards();
            RefreshResourceHud();
            RefreshSystemsHud();
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
            int[] capacities = { 50, 50, 50, 50, 50, 4, 3 };
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

            infoPanel = Panel("Ficha de instalación", canvas, SpaceFarmPalette.Panel,
                new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),
                new Vector2(-15,-108),new Vector2(285,208));
            infoPanel.GetComponent<Image>().raycastTarget = true;
            infoTitle = Label("", infoPanel.transform, 17, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(0,1),new Vector2(1,1),new Vector2(0,1),
                new Vector2(12,-8),new Vector2(-52,29));
            infoTitle.enableAutoSizing = true;
            infoTitle.fontSizeMin = 12;
            infoTitle.fontSizeMax = 17;
            infoBody = Label("", infoPanel.transform, 12, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(1,1),new Vector2(0,1),
                new Vector2(12,-39),new Vector2(-24,116));
            infoBody.alignment = TextAlignmentOptions.TopLeft;
            infoBody.enableAutoSizing = true;
            infoBody.fontSizeMin = 10;
            infoBody.fontSizeMax = 12;
            infoPrimary = AddButton("RECOGER", infoPanel.transform, ContextPrimaryAction, 11,
                SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(0,0),new Vector2(0,0),new Vector2(0,0),
                new Vector2(10,9),new Vector2(158,34));
            infoSecondary = AddButton("MEJORAR", infoPanel.transform, ContextSecondaryAction, 11,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1,0),new Vector2(1,0),new Vector2(1,0),
                new Vector2(-9,9),new Vector2(106,34));
            AddButton("×", infoPanel.transform, HideWorldSelection, 18,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),
                new Vector2(-6,-6),new Vector2(28,27));
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
            if (sol.RaidFoodLoss > 0)
                text += $" Incursión ({state.BarrierCount}/12 muros): -{sol.RaidFoodLoss} comida. Las minas siguen operativas.";
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
            SetReport("Elegí cualquier sector libre del mapa para colocar un muro por 1 Fe. R cambia la orientación; Escape termina.");
        }

        private void EndBarrierMode()
        {
            barrierMode = false;
            interactionMode = InteractionMode.Explore;
            if (barrierPreview != null) barrierPreview.SetActive(false);
            if (buildModePanel != null) buildModePanel.SetActive(false);
            RefreshSystems();
            SetReport($"Construcción terminada. Muros {state.BarrierCount}/{SpaceFarmState.MaxBarrierCount}.");
        }

        private bool SelectWorldBuilding(string building)
        {
            selectedBuilding = building;
            selectedDeposit = -1;
            infoPanel.SetActive(true);
            RefreshHud();
            return true;
        }

        private bool SelectDeposit(int depositId)
        {
            selectedBuilding = "deposit";
            selectedDeposit = depositId;
            infoPanel.SetActive(true);
            RefreshHud();
            return true;
        }

        private void HideWorldSelection()
        {
            bool changed = selectedBuilding != null;
            selectedBuilding = null;
            selectedDeposit = -1;
            if (infoPanel != null) infoPanel.SetActive(false);
            if (changed && actionCardsRoot != null) RefreshActionCards();
        }

        private void ContextPrimaryAction()
        {
            SpaceFarmState.DepositState deposit = state.Deposit(selectedDeposit);
            if (paused || selectedBuilding != "deposit" || deposit == null) return;
            int id = deposit.Id;
            if (deposit.MineLevel > 0) StartMineCollection(id);
            else if (deposit.Extractor) return;
            else BeginStructurePlacement(deposit.Kind == DepositKind.Iron ? StructureKind.Mine : StructureKind.Extractor);
        }

        private void ContextSecondaryAction()
        {
            SpaceFarmState.DepositState deposit = state.Deposit(selectedDeposit);
            if (paused || selectedBuilding != "deposit" || deposit == null || deposit.MineLevel == 0) return;
            int id = deposit.Id;
            Act((out string message) => state.UpgradeMine(id, out message));
        }

        private void RefreshSystemsHud()
        {
            if (state == null) return;
            if (buildModeText != null && barrierMode)
                buildModeText.text = $"MURO · 1 Fe\nMuros {state.BarrierCount}/12 · Hierro {state.Iron} · R gira";
            if (defenseText != null)
                defenseText.text = $"MUROS {state.BarrierCount}/12\nMINAS {state.MineCount}/{state.MaxMines}";
            if (infoPanel == null || string.IsNullOrEmpty(selectedBuilding)) return;
            if (selectedBuilding == "deposit")
            {
                RefreshDepositCard();
                return;
            }
            SpaceFarmState.SystemResult result = null;
            string title;
            string extra = "";
            int nominal = 4;
            int expectedEnergy = 3;
            switch (selectedBuilding)
            {
                case "oxygen1": title = "Planta de oxígeno 1"; result = state.LastSol.OxygenOne; break;
                case "oxygen2": title = "Planta de oxígeno 2"; result = state.LastSol.OxygenTwo; break;
                case "greenhouse": title = "Invernadero"; result = state.LastSol.Greenhouse; expectedEnergy = 2;
                    extra = $"Insumo: {result.OtherUsed} agua en el último sol."; break;
                case "solar": title = "Generación solar"; result = state.LastSol.Solar; nominal = 9; expectedEnergy = 0;
                    extra = $"Batería {state.Energy}/30  ·  Tormenta: {(state.LastSol.Storm ? "sí" : "no")}"; break;
                default: title = "Hábitat";
                    extra = $"Colonos {state.Population}/4  ·  Salud {state.Health}/3\nMinas operables: {state.MaxMines} (una por colono a partir del segundo).\nCarga del rover: {state.RoverCargo}/{state.RoverCapacity} Fe"; break;
            }
            infoTitle.text = title;
            infoBody.text = result == null ? extra : ResultText(result, "Operativa", nominal, expectedEnergy, extra);
            infoPrimary.gameObject.SetActive(false);
            infoSecondary.gameObject.SetActive(false);
        }

        private string ResultText(SpaceFarmState.SystemResult result, string status, int nominal, int expectedEnergy, string extra)
        {
            if (status == "Operativa" && result.Nominal == 0 && state.Sol > 1) status = "Sin ciclo todavía";
            else if (status == "Operativa" && result.Nominal > 0 && result.Actual == 0 && state.Sol > 1) status = "Detenida";
            return $"Estado: {status}\nNominal: {nominal}/sol  ·  Real último: {result.Actual}\nEnergía: {expectedEnergy}/sol (usó {result.EnergyUsed})\n{extra}\n{result.Reason}";
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
                string status = deposit.MineStored == deposit.MineCapacity ? "Almacén lleno" : "Operativa";
                string reason = deposit.LastResult.Reason;
                infoBody.text = $"Estado: {status}\nNominal: {deposit.MineNominal}/sol  ·  Real último: {deposit.LastResult.Actual}\n" +
                    $"Energía: {deposit.MineEnergy}/sol (usó {deposit.LastResult.EnergyUsed})\n" +
                    $"Almacén {deposit.MineStored}/{deposit.MineCapacity} Fe  ·  Rover {state.RoverCargo}/{state.RoverCapacity} Fe\n{place}\n{reason}";
                infoPrimary.gameObject.SetActive(true);
                primaryLabel.text = "RECOGER";
                infoPrimary.interactable = deposit.MineStored > 0 && !busy;
                infoSecondary.gameObject.SetActive(deposit.MineLevel < 2);
                infoSecondary.interactable = !state.GameOver;
                infoSecondary.GetComponentInChildren<TextMeshProUGUI>().text = "MEJORAR · 6";
            }
            else if (deposit.Extractor)
            {
                infoTitle.text = $"Extractor {deposit.Name}";
                infoBody.text = ResultText(deposit.LastResult, "Operativa", 5, 2, $"Agua {state.Water}/30\n{place}");
                infoPrimary.gameObject.SetActive(false);
            }
            else if (iron)
            {
                infoTitle.text = $"Yacimiento {deposit.Name}";
                string blocker = state.PlacementBlocker(StructureKind.Mine, map.Deposits[deposit.Id].Cell,
                    IsRevealed(map.Deposits[deposit.Id].Cell));
                infoBody.text = $"Hierro confirmado. {place}\nMina: {state.MineCost} Fe · produce 3 Fe/sol · usa 2 de energía.\n" +
                    $"Minas activas {state.MineCount}/{state.MaxMines}: cada mina necesita un operador (una por colono a partir del segundo) y cada mina nueva cuesta 4 Fe más.\n" +
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
                infoBody.text = $"Hielo confirmado. {place}\nExtractor: {state.ExtractorCost} Fe · produce 5 de agua/sol · usa 2 de energía.\n" +
                    $"Extractores {state.ExtractorCount}/{SpaceFarmState.MaxExtractors}: el segundo cuesta 4 Fe más y el agua nunca supera 30.\n" +
                    (blocker.Length > 0 ? blocker + "." : "Listo para construir.");
                infoPrimary.gameObject.SetActive(true);
                primaryLabel.text = $"COLOCAR EXTRACTOR · {state.ExtractorCost}";
                infoPrimary.interactable = blocker.Length == 0 && !state.GameOver;
            }
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>Large, responsive STEM cards for selectable world objects.</summary>
    public sealed partial class SpaceFarmGame
    {
        private Image infoPreview;
        private TextMeshProUGUI infoLevelState;
        private TextMeshProUGUI infoEducationText;
        private RectTransform infoMetricsRoot;
        private Button infoRepair;
        private Button infoCrew;
        private readonly List<GameObject> infoMetricCards = new List<GameObject>();

        private void CreateEducationalInfoCard(Transform canvas)
        {
            infoPanel = Panel("Ficha STEM", canvas, new Color(.025f, .045f, .065f, .9f),
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            infoPanel.GetComponent<Image>().raycastTarget = true;

            GameObject card = Panel("Ficha", infoPanel.transform, SpaceFarmPalette.Panel,
                new Vector2(.025f, .045f), new Vector2(.975f, .955f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            Image cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;
            Panel("Acento superior", card.transform, SpaceFarmPalette.Iron,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 4));

            infoTitle = Label("", card.transform, 24, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(22, -12), new Vector2(-100, 34));
            infoTitle.enableAutoSizing = true;
            infoTitle.fontSizeMin = 17;
            infoTitle.fontSizeMax = 24;
            infoLevelState = Label("", card.transform, 12, FontStyles.Bold, SpaceFarmPalette.Oxygen,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(23, -45), new Vector2(-30, 22));
            infoLevelState.enableAutoSizing = true;
            infoLevelState.fontSizeMin = 9;
            infoLevelState.fontSizeMax = 12;
            Button close = AddButton("CERRAR  ×", card.transform, HideWorldSelection, 12,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-12, -12), new Vector2(105, 34));
            close.navigation = new Navigation { mode = Navigation.Mode.None };

            GameObject preview = Panel("Vista del objeto", card.transform, SpaceFarmPalette.Background,
                new Vector2(0, 0), new Vector2(.29f, 1), new Vector2(0, .5f),
                new Vector2(14, -8), new Vector2(-8, -150));
            preview.GetComponent<Image>().raycastTarget = true;
            GameObject artwork = new GameObject("Arte del objeto", typeof(RectTransform), typeof(Image));
            artwork.transform.SetParent(preview.transform, false);
            infoPreview = artwork.GetComponent<Image>();
            infoPreview.color = Color.white;
            infoPreview.preserveAspect = true;
            infoPreview.raycastTarget = false;
            infoPreview.rectTransform.anchorMin = new Vector2(.08f, .14f);
            infoPreview.rectTransform.anchorMax = new Vector2(.92f, .86f);
            infoPreview.rectTransform.offsetMin = Vector2.zero;
            infoPreview.rectTransform.offsetMax = Vector2.zero;
            Label("OBJETO SELECCIONADO", preview.transform, 11, FontStyles.Bold, SpaceFarmPalette.MutedText,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1),
                new Vector2(0, -10), new Vector2(-16, 20)).alignment = TextAlignmentOptions.Center;

            GameObject right = new GameObject("Datos y aprendizaje", typeof(RectTransform));
            right.transform.SetParent(card.transform, false);
            RectTransform rightRect = right.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(.31f, 0);
            rightRect.anchorMax = new Vector2(1, 1);
            rightRect.offsetMin = new Vector2(0, 72);
            rightRect.offsetMax = new Vector2(-16, -105);

            GameObject metrics = new GameObject("Tarjetas de datos", typeof(RectTransform), typeof(GridLayoutGroup));
            metrics.transform.SetParent(right.transform, false);
            infoMetricsRoot = metrics.GetComponent<RectTransform>();
            infoMetricsRoot.anchorMin = new Vector2(0, .66f);
            infoMetricsRoot.anchorMax = new Vector2(1, 1);
            infoMetricsRoot.offsetMin = new Vector2(0, 0);
            infoMetricsRoot.offsetMax = Vector2.zero;
            GridLayoutGroup grid = metrics.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(145, Screen.height < 600 ? 52 : 63);
            grid.spacing = new Vector2(7, 7);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

            GameObject scroll = new GameObject("Explicación desplazable", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            scroll.transform.SetParent(right.transform, false);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, .63f);
            scrollRect.offsetMin = new Vector2(0, 0);
            scrollRect.offsetMax = new Vector2(0, -7);
            Image scrollImage = scroll.GetComponent<Image>();
            scrollImage.color = SpaceFarmPalette.Background;
            scrollImage.raycastTarget = true;
            scroll.GetComponent<Mask>().showMaskGraphic = false;
            GameObject content = new GameObject("Contenido", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(14, 14, 11, 11);
            contentLayout.spacing = 10;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            infoEducationText = Label("", content.transform, 15, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            infoEducationText.alignment = TextAlignmentOptions.TopLeft;
            infoEducationText.fontSize = Screen.height < 600 ? 13 : 15;
            infoEducationText.enableAutoSizing = false;
            infoEducationText.textWrappingMode = TextWrappingModes.Normal;
            LayoutElement textLayout = infoEducationText.gameObject.AddComponent<LayoutElement>();
            textLayout.minHeight = 130;
            ScrollRect scrollView = scroll.GetComponent<ScrollRect>();
            scrollView.viewport = scrollRect;
            scrollView.content = contentRect;
            scrollView.horizontal = false;
            scrollView.vertical = true;
            scrollView.movementType = ScrollRect.MovementType.Clamped;
            scrollView.scrollSensitivity = 30;

            infoPrimary = AddButton("", card.transform, ContextPrimaryAction, 12,
                SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(16, 13), new Vector2(200, 38));
            infoSecondary = AddButton("", card.transform, ContextSecondaryAction, 12,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(224, 13), new Vector2(230, 38));
            infoRepair = AddButton("REPARAR", card.transform, RepairSelected, 12, SpaceFarmPalette.Danger, SpaceFarmPalette.Background,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(462, 13), new Vector2(145, 38));
            infoCrew = AddButton("ADMINISTRAR ASTRONAUTAS", card.transform, OpenCrewFromInfo, 11, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 13), new Vector2(220, 38));
            infoPanel.SetActive(false);
        }

        private void EnsureInfoCardUsesCanvasRect()
        {
            if (infoPanel == null) return;
            RectTransform rect = infoPanel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image shade = infoPanel.GetComponent<Image>();
            if (shade != null)
            {
                shade.color = new Color(.025f, .045f, .065f, .9f);
                shade.raycastTarget = true;
            }
        }

        private void RefreshEducationalCard()
        {
            if (infoPanel == null || string.IsNullOrEmpty(selectedBuilding)) return;
            string title = "", level = "Sin niveles", status = "Operativa", facts = "", reality = "", game = "";
            List<string[]> metrics = new List<string[]>();
            Sprite sprite = null;
            CrewJob job = SelectedWorkerJob(out int target);
            SpaceFarmState.CrewMember worker = job == CrewJob.None ? null : state.WorkerOn(job, target);
            bool damaged = false;
            bool hasWorkerAccess = job != CrewJob.None;
            bool showPrimary = false, showSecondary = false, showRepair = false;
            string primaryText = "", secondaryText = "";
            bool primaryEnabled = false, secondaryEnabled = false;
            string actionReason = "";
            int ironIcon = 0, energyIcon = 1, oxygenIcon = 2, waterIcon = 3, foodIcon = 4, peopleIcon = 5;

            if (selectedBuilding == "deposit")
            {
                SpaceFarmState.DepositState d = state.Deposit(selectedDeposit);
                if (d == null) { HideWorldSelection(); return; }
                sprite = FindWorldSprite(selectedDeposit, true);
                Vector2Int cell = map.Deposits[d.Id].Cell;
                string distance = $"A {d.Distance} sectores de la base, hacia el {SpaceFarmMap.Compass(BaseCell, cell)}.";
                if (d.MineLevel > 0)
                {
                    title = $"Mina {d.Name}"; level = $"Nivel {d.MineLevel}/{SpaceFarmState.MaxLevel} · Depósito local {d.MineStored}/{d.MineCapacity} Fe";
                    damaged = state.IsDamaged(DamagedStructureKind.Mine, d.Id);
                    status = damaged ? "Averiada por tormenta" : d.MineStored >= d.MineCapacity ? "Almacén lleno" : "Operativa";
                    AddProduction(metrics, "Hierro", d.MineNominal + state.ActiveWorkers(CrewJob.Mine, d.Id) * 3,
                        d.LastResult, ironIcon, "Fe/sol");
                    metrics.Add(new[] { "Energía", $"-{d.MineEnergy}/sol · real -{d.LastResult.EnergyUsed}", energyIcon.ToString() });
                    metrics.Add(new[] { "Depósito local", $"{d.MineStored}/{d.MineCapacity} Fe", ironIcon.ToString() });
                    metrics.Add(new[] { "Carga del rover", $"{state.RoverCargoTotal}/{state.RoverCapacity} total · Fe {state.RoverCargo}", ironIcon.ToString() });
                    facts = $"{distance}\n{d.LastResult.Reason}";
                    reality = "La composición del suelo marciano cambia de un lugar a otro. Una señal mineral no garantiza que haya hierro fácil de extraer: una misión real tendría que analizar muestras y evaluar el terreno.";
                    game = "El yacimiento estudiado se convierte en una mina abstracta. Cada mina guarda su propio hierro; enviá el rover para recogerlo y vigila su consumo de energía.";
                    showPrimary = true; primaryText = "RECOGER HIERRO"; primaryEnabled = d.MineStored > 0 && !RoverBusy && !state.GameOver;
                    actionReason = d.MineStored == 0 ? "La mina todavía no tiene hierro almacenado." : RoverBusy ? "El rover está ocupado." : "";
                    showSecondary = d.MineLevel < SpaceFarmState.MaxLevel; secondaryText = UpgradeActionText("mine", d.Id, state.MineUpgradeCost(d.Id)); secondaryEnabled = !state.HasUpgrade("mine", d.Id) && !state.GameOver && state.Iron >= state.MineUpgradeCost(d.Id);
                    if (!secondaryEnabled) actionReason = state.HasUpgrade("mine", d.Id) ? $"Mejora en curso: {state.UpgradeRemaining("mine", d.Id)} s." : state.Iron < state.MineUpgradeCost(d.Id) ? $"Faltan {state.MineUpgradeCost(d.Id) - state.Iron} Fe para mejorar." : actionReason;
                    showRepair = damaged;
                }
                else if (d.Extractor)
                {
                    title = $"Extractor de agua · {d.Name}"; level = $"Nivel {state.ExtractorLevel(d.Id)}/{SpaceFarmState.MaxLevel}"; damaged = false;
                    status = d.LastResult.Actual == 0 && state.Sol > 1 ? "Sin producción" : "Operativa";
                    AddProduction(metrics, "Agua", 8 + state.ActiveWorkers(CrewJob.Extractor, d.Id) * 3, d.LastResult, waterIcon, "unid./sol juego");
                    metrics.Add(new[] { "Energía", $"-2/sol · real -{d.LastResult.EnergyUsed}", energyIcon.ToString() });
                    metrics.Add(new[] { "Depósito local", $"{d.WaterStored}/{SpaceFarmState.LocalStorageCapacity} agua", waterIcon.ToString() });
                    metrics.Add(new[] { "Reserva utilizable", $"{state.Water}/60", waterIcon.ToString() });
                    metrics.Add(new[] { "Carga del rover", $"{state.RoverWaterCargo}/{state.RoverCapacity} agua", ironIcon.ToString() });
                    facts = $"{distance}\n{d.LastResult.Reason}";
                    reality = "Orbitadores han cartografiado hielo subterráneo en algunas regiones de Marte. Su profundidad y accesibilidad varían; extraerlo requiere localizarlo, perforar y procesar el material.";
                    game = "El juego representa el hielo confirmado como un depósito y resume la perforación en una instalación. La cantidad real depende de energía, capacidad disponible y agua que ya haya en reserva.";
                    showPrimary = true; primaryText = "RECOGER AGUA"; primaryEnabled = d.WaterStored > 0 && !RoverBusy && !state.GameOver;
                    actionReason = d.WaterStored <= 0 ? "El depósito local todavía no tiene agua." : RoverBusy ? "El rover está ocupado." : "";
                    showSecondary = state.ExtractorLevel(d.Id) < SpaceFarmState.MaxLevel;
                    secondaryText = UpgradeActionText("extractor", d.Id, state.ExtractorUpgradeCost(d.Id));
                    secondaryEnabled = !state.HasUpgrade("extractor", d.Id) && !state.GameOver && state.Iron >= state.ExtractorUpgradeCost(d.Id);
                }
                else
                {
                    bool iron = d.Kind == DepositKind.Iron;
                    title = iron ? $"Yacimiento de hierro · {d.Name}" : $"Depósito de hielo · {d.Name}";
                    status = "Estudiado · listo para construir";
                    metrics.Add(new[] { "Distancia", $"{d.Distance} sectores", "" });
                    metrics.Add(new[] { "Mineral", iron ? "Hierro" : "Hielo", iron ? ironIcon.ToString() : waterIcon.ToString() });
                    metrics.Add(new[] { "Costo", $"{(iron ? state.MineCost : state.ExtractorCost)} Fe", ironIcon.ToString() });
                    facts = distance;
                    reality = iron ? "La superficie de Marte contiene minerales, pero su tipo, concentración y facilidad de extracción cambian según la zona. Un rover científico estudia primero el terreno antes de elegir dónde trabajar." : "El hielo de agua puede estar bajo la superficie y cubierto por regolito. Las misiones usan mapas y mediciones para buscar depósitos accesibles; no se supone que haya hielo aprovechable en cualquier sitio.";
                    StructureKind kind = iron ? StructureKind.Mine : StructureKind.Extractor;
                    actionReason = state.PlacementBlocker(kind, cell, IsRevealed(cell));
                    game = actionReason.Length > 0 ? actionReason : iron ? "Elegí colocar una mina para comenzar la extracción." : "Elegí colocar un extractor para comenzar la extracción.";
                    showPrimary = true; primaryText = iron ? $"CONSTRUIR MINA · {state.MineCost} Fe" : $"CONSTRUIR EXTRACTOR · {state.ExtractorCost} Fe";
                    primaryEnabled = actionReason.Length == 0 && !state.GameOver;
                }
            }
            else switch (selectedBuilding)
            {
                case "rover":
                    title = "Rover de exploración"; level = $"Nivel {state.RoverLevel} · Alcance {state.RoverRange} sectores (colonia nivel {state.ColonyLevel})";
                    damaged = state.IsDamaged(DamagedStructureKind.Rover); status = damaged ? "Averiado" : RoverBusy ? "En recorrido" : "Listo";
                    sprite = roverRenderer != null ? roverRenderer.sprite : null;
                    metrics.Add(new[] { "Alcance", $"{state.RoverRange} sectores", "" });
                    metrics.Add(new[] { "Exploración", $"Radio {state.ScanRadius} sectores", "" });
                    metrics.Add(new[] { "Carga total", $"{state.RoverCargoTotal}/{state.RoverCapacity} unidades", ironIcon.ToString() });
                    metrics.Add(new[] { "Fe / O2 / agua / comida", $"{state.RoverCargo} / {state.RoverOxygenCargo} / {state.RoverWaterCargo} / {state.RoverFoodCargo}", "" });
                    metrics.Add(new[] { "Movimiento", $"{RoverSpeed:0.0} unidades de juego/s", "" });
                    facts = $"Destino: sector {roverDestination.x},{roverDestination.y}. La velocidad indicada es solo la animación del juego; no representa una velocidad real de un rover.";
                    reality = "Los rovers usan imágenes de cámaras y mapas del terreno para planear rutas seguras. Los equipos evitan pendientes peligrosas, rocas y obstáculos; los comandos se planifican por puntos de recorrido porque Marte está muy lejos para controlarlo en tiempo real.";
                    game = "Este rover ficticio recorre una cuadrícula y revela sectores con su escáner. Mejorarlo aumenta su carga y radio; el nivel de colonia limita hasta dónde puede explorar.";
                    showSecondary = state.RoverLevel < 2; secondaryText = UpgradeActionText("rover", 0, state.RoverUpgradeCost); secondaryEnabled = !state.HasUpgrade("rover", 0) && state.Iron >= state.RoverUpgradeCost && !state.GameOver;
                    showRepair = damaged;
                    break;
                case "solar":
                    title = "Paneles solares"; level = $"Nivel {state.SolarLevel}/{SpaceFarmState.MaxLevel}"; damaged = state.IsDamaged(DamagedStructureKind.Solar);
                    status = damaged ? "Averiados" : state.LastSol.Storm ? "Producción afectada por tormenta" : "Operativos";
                    sprite = solarWorld != null ? solarWorld.GetComponent<SpriteRenderer>().sprite : null;
                    AddProduction(metrics, "Energía", state.SolarPerSol + state.ActiveWorkers(CrewJob.Solar, 0) * 5, state.LastSol.Solar, energyIcon, "unid./sol");
                    metrics.Add(new[] { "Batería", $"{state.Energy}/{state.EnergyCapacity}", energyIcon.ToString() });
                    metrics.Add(new[] { "Clima", state.LastSol.Storm ? "Tormenta" : "Sin tormenta", "" });
                    facts = state.LastSol.Solar.Reason;
                    reality = "Las celdas fotovoltaicas convierten la luz en electricidad. Una tormenta de polvo puede reducir la luz que llega a los paneles y acumular polvo en su superficie, así que una base necesita almacenamiento y planes de respaldo.";
                    game = "El juego resume la generación en energía por sol y reduce la producción durante tormentas. Ampliar los paneles aumenta generación y capacidad de batería.";
                    showSecondary = state.SolarLevel < SpaceFarmState.MaxLevel; secondaryText = UpgradeActionText("solar", 0, state.SolarUpgradeCost); secondaryEnabled = !state.HasUpgrade("solar", 0) && state.Iron >= state.SolarUpgradeCost && !state.GameOver;
                    showRepair = damaged;
                    break;
                case "oxygen1": case "oxygen2":
                    int oxygenIndex = selectedBuilding == "oxygen1" ? 0 : selectedBuildingIndex + 1;
                    title = $"Planta de oxígeno {oxygenIndex + 1}"; level = $"Nivel {state.OxygenLevel(oxygenIndex)}/{SpaceFarmState.MaxLevel}";
                    sprite = oxygenIndex == 0 ? oxygenPlantWorld.GetComponent<SpriteRenderer>().sprite : oxygenWorlds[oxygenIndex - 1].GetComponent<SpriteRenderer>().sprite;
                    SpaceFarmState.SystemResult oxygenResult = oxygenIndex == 0 ? state.LastSol.OxygenOne : oxygenIndex < state.LastSol.OxygenUnits.Count ? state.LastSol.OxygenUnits[oxygenIndex] : new SpaceFarmState.SystemResult();
                    AddProduction(metrics, "Oxígeno", 7 + state.ActiveWorkers(CrewJob.Oxygen, oxygenIndex) * 3, oxygenResult, oxygenIcon, "unid./sol");
                    metrics.Add(new[] { "Depósito local", $"{state.OxygenLocal(oxygenIndex)}/{SpaceFarmState.LocalStorageCapacity} O2", oxygenIcon.ToString() });
                    showPrimary = true; primaryText = "RECOGER OXÍGENO"; primaryEnabled = state.OxygenLocal(oxygenIndex) > 0 && !RoverBusy && !state.GameOver;
                    actionReason = state.OxygenLocal(oxygenIndex) <= 0 ? "El depósito local todavía no tiene oxígeno." : RoverBusy ? "El rover está ocupado." : "";
                    metrics.Add(new[] { "Energía", $"-2/sol · real -{oxygenResult.EnergyUsed}", energyIcon.ToString() });
                    metrics.Add(new[] { "Reserva", $"{state.Oxygen}/60", oxygenIcon.ToString() });
                    status = oxygenResult.Actual == 0 && state.Sol > 1 ? "Detenida o sin energía" : "Operativa";
                    facts = oxygenResult.Reason;
                    reality = "MOXIE fue un experimento tecnológico del rover Perseverance. Recogía dióxido de carbono marciano y lo separaba electroquímicamente para producir oxígeno; fue una demostración, no una planta capaz de sostener por sí sola a una colonia.";
                    game = "La planta del juego está inspirada en MOXIE, pero produce oxígeno de forma continua y simplificada. No simula CO₂, presión, pureza ni el tamaño industrial que necesitaría una misión humana.";
                    showSecondary = state.OxygenLevel(oxygenIndex) < SpaceFarmState.MaxLevel;
                    secondaryText = UpgradeActionText("oxygen", oxygenIndex, state.OxygenUpgradeCost(oxygenIndex));
                    secondaryEnabled = !state.HasUpgrade("oxygen", oxygenIndex) && !state.GameOver && state.Iron >= state.OxygenUpgradeCost(oxygenIndex);
                    break;
                case "greenhouse":
                    int greenhouseIndex = selectedBuildingIndex;
                    title = $"Invernadero {greenhouseIndex + 1}"; level = $"Nivel {state.GreenhouseLevel(greenhouseIndex)}/{SpaceFarmState.MaxLevel}"; damaged = state.IsDamaged(DamagedStructureKind.Greenhouse, greenhouseIndex);
                    sprite = greenhouseWorlds[greenhouseIndex].GetComponent<SpriteRenderer>().sprite;
                    SpaceFarmState.SystemResult greenhouseResult = greenhouseIndex < state.LastSol.GreenhouseUnits.Count ? state.LastSol.GreenhouseUnits[greenhouseIndex] : new SpaceFarmState.SystemResult();
                    AddProduction(metrics, "Comida", 7 + state.ActiveWorkers(CrewJob.Greenhouse, greenhouseIndex) * 3, greenhouseResult, foodIcon, "unid./sol");
                    metrics.Add(new[] { "Depósito local", $"{state.GreenhouseLocal(greenhouseIndex)}/{SpaceFarmState.LocalStorageCapacity} comida", foodIcon.ToString() });
                    showPrimary = true; primaryText = "RECOGER COMIDA"; primaryEnabled = state.GreenhouseLocal(greenhouseIndex) > 0 && !RoverBusy && !state.GameOver;
                    actionReason = state.GreenhouseLocal(greenhouseIndex) <= 0 ? "El depósito local todavía no tiene comida." : RoverBusy ? "El rover está ocupado." : "";
                    metrics.Add(new[] { "Agua usada", $"-{greenhouseResult.OtherUsed} unid./sol real", waterIcon.ToString() });
                    metrics.Add(new[] { "Energía", $"-2/sol · real -{greenhouseResult.EnergyUsed}", energyIcon.ToString() });
                    status = damaged ? "Averiado" : greenhouseResult.Actual == 0 && state.Sol > 1 ? "Sin producción" : "Operativo";
                    facts = greenhouseResult.Reason;
                    reality = "Las plantas necesitan luz, agua, nutrientes y un ambiente controlado. En el espacio, los equipos estudian cómo cultivar alimentos con recursos limitados y proteger los cultivos de condiciones extremas.";
                    game = "El invernadero convierte agua y energía en comida. La simulación no modela nutrientes, temperatura, iluminación por cultivo ni tiempos biológicos; el jugador decide cuándo puede sostener ese gasto.";
                    showSecondary = state.GreenhouseLevel(greenhouseIndex) < SpaceFarmState.MaxLevel;
                    secondaryText = UpgradeActionText("greenhouse", greenhouseIndex, state.GreenhouseUpgradeCost(greenhouseIndex));
                    secondaryEnabled = !state.HasUpgrade("greenhouse", greenhouseIndex) && !state.GameOver && state.Iron >= state.GreenhouseUpgradeCost(greenhouseIndex);
                    showRepair = damaged;
                    break;
                case "dormitory":
                    title = "Dormitorio de astronautas"; level = "Sin niveles"; damaged = state.IsDamaged(DamagedStructureKind.Dormitory);
                    status = damaged ? "Averiado" : "Disponible"; sprite = dormWorld.GetComponent<SpriteRenderer>().sprite;
                    metrics.Add(new[] { "Colonos", $"{state.Population}/{state.HabitatCapacity}", peopleIcon.ToString() });
                    metrics.Add(new[] { "Libres", $"{state.FreeWorkers}", peopleIcon.ToString() });
                    metrics.Add(new[] { "Salud", $"{state.Health}/3", "6" });
                    facts = damaged ? "El dormitorio está averiado; la recuperación de energía durante el descanso es menor hasta repararlo." : "Los astronautas vuelven aquí automáticamente cuando necesitan descansar.";
                    reality = "Las tripulaciones necesitan sueño y períodos de descanso para mantener su salud y capacidad de trabajo. Un hábitat real debe controlar aire, temperatura, humedad, ruido y protección, además de ofrecer un lugar para dormir.";
                    game = "El dormitorio modela el descanso y la recuperación de energía. No simula sueño, salud ni condiciones médicas con detalle; una avería ralentiza la recuperación.";
                    showRepair = damaged;
                    break;
                case "warehouse":
                    title = "Almacén de recursos"; level = $"Nivel {state.WarehouseLevel(selectedBuildingIndex)}/{SpaceFarmState.MaxLevel}"; status = state.WarehouseFree <= 0 ? "Almacén lleno" : "Operativo";
                    sprite = warehouseWorlds[selectedBuildingIndex].GetComponent<SpriteRenderer>().sprite;
                    metrics.Add(new[] { "Capacidad compartida", $"{state.WarehouseUsed}/{state.WarehouseCapacity} unidades", "" });
                    metrics.Add(new[] { "Hierro / oxígeno", $"{state.Iron} / {state.Oxygen}", "" });
                    metrics.Add(new[] { "Agua / comida", $"{state.Water} / {state.Food}", "" });
                    metrics.Add(new[] { "Almacenes", $"{state.WarehouseCount}", "" });
                    metrics.Add(new[] { "Carga rover", $"{state.RoverCargoTotal}/{state.RoverCapacity} · Fe {state.RoverCargo}, O2 {state.RoverOxygenCargo}, Ag {state.RoverWaterCargo}, Co {state.RoverFoodCargo}", ironIcon.ToString() });
                    facts = "Los cuatro recursos ocupan el mismo espacio. La carga que no cabe permanece a bordo del rover.";
                    reality = "Las bases de exploración necesitan logística para guardar muestras, herramientas y repuestos. El almacenamiento debe proteger el contenido y permitir que la tripulación lo encuentre cuando hace falta.";
                    game = "Oxígeno, agua, comida y hierro comparten capacidad. Las reservas iniciales y las entregas también ocupan espacio.";
                    showPrimary = state.RoverCargoTotal > 0; primaryText = "ENVIAR ROVER A DESCARGAR";
                    primaryEnabled = state.RoverCargoTotal > 0 && !RoverBusy && !state.GameOver && state.WarehouseFree > 0;
                    actionReason = state.WarehouseFree <= 0 ? "La capacidad compartida está completa." : RoverBusy ? "El rover está ocupado." : "";
                    showSecondary = state.WarehouseLevel(selectedBuildingIndex) < SpaceFarmState.MaxLevel;
                    secondaryText = UpgradeActionText("warehouse", selectedBuildingIndex, state.WarehouseUpgradeCost(selectedBuildingIndex));
                    secondaryEnabled = !state.HasUpgrade("warehouse", selectedBuildingIndex) && !state.GameOver && state.Iron >= state.WarehouseUpgradeCost(selectedBuildingIndex);
                    break;
                case "wall":
                    title = "Muro modular"; level = "Sin niveles"; sprite = Resources.Load<Sprite>("Art/space-farm-wall-post");
                    metrics.Add(new[] { "Perímetro", $"{state.BarrierCount} tramos", "" });
                    metrics.Add(new[] { "Costo", "1 Fe por tramo", ironIcon.ToString() });
                    status = "Protección de perímetro";
                    facts = "Los tramos vecinos se conectan visualmente para cerrar el perímetro. Los muros cercanos al hábitat reducen las pérdidas de comida durante una incursión.";
                    reality = "Una barrera física puede separar áreas y proteger equipos de ciertos peligros, pero no convierte una base en un refugio contra toda amenaza. La radiación requiere materiales, masa y diseño especializados.";
                    game = "Las incursiones de criaturas alienígenas y su defensa son ficción del juego. Los muros reducen pérdidas de comida; no protegen contra radiación ni representan una solución real de ingeniería marciana.";
                    break;
                default:
                    title = "Hábitat principal"; level = $"Colonia nivel {state.ColonyLevel}/5"; sprite = habitatWorld.GetComponent<SpriteRenderer>().sprite;
                    metrics.Add(new[] { "Colonos", $"{state.Population}/{state.HabitatCapacity}", peopleIcon.ToString() });
                    metrics.Add(new[] { "Oxígeno", $"{state.Oxygen}/60", oxygenIcon.ToString() });
                    metrics.Add(new[] { "Agua", $"{state.Water}/60", waterIcon.ToString() });
                    metrics.Add(new[] { "Comida", $"{state.Food}/60", foodIcon.ToString() });
                    metrics.Add(new[] { "Energía", $"{state.Energy}/{state.EnergyCapacity}", energyIcon.ToString() });
                    metrics.Add(new[] { "Salud", $"{state.Health}/3", "6" });
                    status = state.GameOver ? "Misión finalizada" : "Base operativa";
                    facts = $"Astronautas libres: {state.FreeWorkers}. XP: {state.Xp}/{(state.XpToNext == 0 ? state.Xp : state.XpToNext)}. El espacio para nuevos colonos depende del nivel de colonia.";
                    reality = "Un hábitat necesita mantener presión y temperatura, renovar el aire, recuperar agua y proteger a la tripulación de radiación y micrometeoritos. Todos estos subsistemas se afectan entre sí.";
                    game = "La partida resume la supervivencia con reservas de oxígeno, agua, comida, energía y camas. El modelo educativo no simula presión, temperatura ni dosis de radiación de forma numérica.";
                    break;
            }

            string editRemovalReason = string.Empty;
            if (editMode)
            {
                bool hasSelection = SelectedEditTarget(out StructureKind editKind, out int editIndex, out bool movable);
                editRemovalReason = hasSelection ? state.RemovalBlocker(editKind, editIndex) : "Esta estructura base no se puede eliminar";
                showRepair = false;
                showPrimary = movable;
                primaryText = "MOVER";
                primaryEnabled = movable && !RoverBusy && !state.GameOver;
                showSecondary = hasSelection;
                secondaryText = "ELIMINAR";
                secondaryEnabled = hasSelection && editRemovalReason.Length == 0 && !state.GameOver;
                actionReason = movable ? RoverBusy ? "Esperá a que el rover termine su recorrido." : "Elegí MOVER y luego un sector válido." : "Las minas y los extractores quedan anclados al yacimiento.";
                facts = "Modo Editar conserva el nivel y el trabajador al mover. El reembolso es aproximadamente el 50 % del costo base; los depósitos minerales quedan en su sitio.";
            }

            if (sprite != null) infoPreview.sprite = sprite;
            infoPreview.enabled = sprite != null;
            infoTitle.text = title;
            infoLevelState.text = $"{level}     ·     ESTADO: {status}";
            if (damaged && state.DamagedStructure != DamagedStructureKind.None)
                facts += $"\nReparación: {state.RepairIronCost} Fe y {state.RepairEnergyCost} energía.";
            if (worker != null) facts += "\n" + WorkerText(worker, job);
            string secondaryReason = "";
            if (editMode && showSecondary && !secondaryEnabled) secondaryReason = editRemovalReason;
            if (!editMode && showSecondary && !secondaryEnabled)
            {
                if (selectedBuilding == "deposit")
                {
                    SpaceFarmState.DepositState d = state.Deposit(selectedDeposit);
                    if (d != null && state.HasUpgrade("mine", d.Id)) secondaryReason = $"Mejora en curso: {state.UpgradeRemaining("mine", d.Id)} s.";
                    else if (d != null && state.Iron < state.MineUpgradeCost(d.Id)) secondaryReason = $"Faltan {state.MineUpgradeCost(d.Id) - state.Iron} Fe para mejorar.";
                }
                else if (selectedBuilding == "rover") secondaryReason = state.HasUpgrade("rover", 0) ? $"Mejora en curso: {state.UpgradeRemaining("rover", 0)} s." : $"Faltan {Mathf.Max(0, state.RoverUpgradeCost - state.Iron)} Fe para mejorar.";
                else if (selectedBuilding == "solar") secondaryReason = state.HasUpgrade("solar", 0) ? $"Mejora en curso: {state.UpgradeRemaining("solar", 0)} s." : $"Faltan {Mathf.Max(0, state.SolarUpgradeCost - state.Iron)} Fe para mejorar.";
            }
            string nextStep = actionReason.Length > 0 ? actionReason : secondaryReason;
            string reasonLine = nextStep.Length > 0 ? $"\n\n<b>SIGUIENTE PASO</b>  {nextStep}" : "";
            infoEducationText.text = $"<b>DATOS DE ESTA INSTALACIÓN</b>\n{facts}\n\n<b>¿CÓMO FUNCIONA EN LA REALIDAD?</b>\n{reality}\n\n<b>EN SPACE FARM</b>\n{game}{reasonLine}";
            infoEducationText.fontSize = Screen.height < 600 ? 13 : 15;
            RefreshMetricCards(metrics);

            infoPrimary.gameObject.SetActive(showPrimary);
            infoPrimary.interactable = primaryEnabled;
            if (showPrimary) infoPrimary.GetComponentInChildren<TextMeshProUGUI>().text = primaryText;
            infoSecondary.gameObject.SetActive(showSecondary);
            infoSecondary.interactable = secondaryEnabled;
            if (showSecondary) infoSecondary.GetComponentInChildren<TextMeshProUGUI>().text = secondaryText;
            bool globalRepair = state.DamagedStructure != DamagedStructureKind.None && DamagedSelectionMatches();
            infoRepair.gameObject.SetActive(showRepair || globalRepair);
            infoRepair.interactable = state.Iron >= state.RepairIronCost && state.Energy >= state.RepairEnergyCost && !state.GameOver && !RoverBusy && !state.IsRepairing;
            if (infoRepair.gameObject.activeSelf)
                infoRepair.GetComponentInChildren<TextMeshProUGUI>().text = state.IsRepairing
                    ? $"REPARACIÓN · {state.RepairSecondsRemaining} s"
                    : state.DamagedStructure == DamagedStructureKind.Rover
                        ? $"REPARAR EN TALLER · {state.RepairIronCost} Fe · 30 s"
                        : $"{(RoverAdjacentToSelected() ? "INICIAR REPARACIÓN" : "ENVIAR ROVER A REPARAR")} · {state.RepairIronCost} Fe · 30 s";
            infoCrew.gameObject.SetActive(hasWorkerAccess);

            LayoutRebuilder.ForceRebuildLayoutImmediate(infoMetricsRoot);
        }

        private void RefreshMetricCards(List<string[]> metrics)
        {
            foreach (GameObject old in infoMetricCards) if (old != null) Destroy(old);
            infoMetricCards.Clear();
            GridLayoutGroup metricGrid = infoMetricsRoot.GetComponent<GridLayoutGroup>();
            if (metricGrid != null && infoMetricsRoot.rect.width > 0)
                metricGrid.cellSize = new Vector2(Mathf.Max(120, (infoMetricsRoot.rect.width - metricGrid.spacing.x * 2f) / 3f), Screen.height < 600 ? 52 : 63);
            for (int i = 0; i < metrics.Count; i++)
            {
                string[] metric = metrics[i];
                GameObject tile = Panel("Dato · " + metric[0], infoMetricsRoot, SpaceFarmPalette.PanelRaised,
                    Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                tile.GetComponent<Image>().raycastTarget = true;
                infoMetricCards.Add(tile);
                int iconIndex;
                if (int.TryParse(metric[2], out iconIndex))
                {
                    GameObject iconObject = new GameObject("Icono", typeof(RectTransform), typeof(Image));
                    iconObject.transform.SetParent(tile.transform, false);
                    Image icon = iconObject.GetComponent<Image>();
                    icon.sprite = ResourceIcon(iconIndex);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    icon.rectTransform.anchorMin = new Vector2(0, .2f);
                    icon.rectTransform.anchorMax = new Vector2(0, .8f);
                    icon.rectTransform.pivot = new Vector2(0, .5f);
                    icon.rectTransform.anchoredPosition = new Vector2(8, 0);
                    icon.rectTransform.sizeDelta = new Vector2(34, 34);
                }
                float left = metric[2].Length > 0 ? 48 : 9;
                Label(metric[0], tile.transform, 10, FontStyles.Bold, SpaceFarmPalette.MutedText,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(left, -6), new Vector2(-left - 7, 17));
                TextMeshProUGUI value = Label(metric[1], tile.transform, 15, FontStyles.Bold, SpaceFarmPalette.Text,
                    new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, .5f), new Vector2(left, -8), new Vector2(-left - 7, 31));
                value.enableAutoSizing = true;
                value.fontSizeMin = 10;
                value.fontSizeMax = 15;
            }
        }

        private static void AddProduction(List<string[]> metrics, string name, int nominal, SpaceFarmState.SystemResult result, int icon, string unit)
        {
            bool cycle = result != null && result.Nominal > 0;
            string actual = cycle ? result.Actual.ToString() : "sin ciclo";
            metrics.Add(new[] { name, $"{nominal}/{unit} · real {actual}", icon.ToString() });
        }

        private string WorkerText(SpaceFarmState.CrewMember worker, CrewJob job)
        {
            if (job == CrewJob.None) return "";
            if (worker == null) return "Tripulación: sin astronauta asignado. Podés abrir Administración de astronautas.";
            return $"Astronauta {worker.Id}: {(worker.Resting ? "descansando" : "trabajando")} · energía personal {worker.Energy:0}% · bono {SpaceFarmState.WorkerBonus(job)} unidades/sol cuando trabaja.";
        }

        private string UpgradeButtonText(string kind, int target, int cost) => state.HasUpgrade(kind, target)
            ? $"MEJORA · {state.UpgradeRemaining(kind, target)} s" : $"MEJORAR · {cost} Fe · {state.UpgradeSecondsToDisplay(kind, target)} s";

        private string UpgradeActionText(string kind, int target, int cost) => state.HasUpgrade(kind, target)
            ? $"MEJORA EN CURSO · {state.UpgradeRemaining(kind, target)} s"
            : $"{(RoverAdjacentToSelected() ? "MEJORAR" : "ENVIAR ROVER")} · {cost} Fe";

        private Sprite FindWorldSprite(int depositId, bool mine)
        {
            if (depositViews == null || depositId < 0 || depositId >= depositViews.Length) return null;
            GameObject building = depositViews[depositId].Building;
            if (building != null) return building.GetComponent<SpriteRenderer>()?.sprite;
            return depositViews[depositId].Marker != null ? depositViews[depositId].Marker.sprite : null;
        }

        private bool DamagedSelectionMatches()
        {
            switch (state.DamagedStructure)
            {
                case DamagedStructureKind.Rover: return selectedBuilding == "rover";
                case DamagedStructureKind.Solar: return selectedBuilding == "solar";
                case DamagedStructureKind.Dormitory: return selectedBuilding == "dormitory";
                case DamagedStructureKind.Greenhouse: return selectedBuilding == "greenhouse" && selectedBuildingIndex == state.DamagedTarget;
                case DamagedStructureKind.Mine: return selectedBuilding == "deposit" && selectedDeposit == state.DamagedTarget;
                default: return false;
            }
        }

        private void RepairSelected()
        {
            if (paused || state == null || !DamagedSelectionMatches()) return;
            OrderRepairSelected();
        }

        private void OpenCrewFromInfo()
        {
            if (infoPanel != null) infoPanel.SetActive(false);
            OpenCrewMenu();
        }

        private void ClearEducationalMetricCards()
        {
            foreach (GameObject metric in infoMetricCards)
                if (metric != null) Destroy(metric);
            infoMetricCards.Clear();
        }
    }
}

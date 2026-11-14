using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    public sealed partial class SpaceFarmGame
    {
        private readonly List<TextMeshProUGUI> resourceLabels = new List<TextMeshProUGUI>();
        private readonly List<RectTransform> resourceChips = new List<RectTransform>();
        private readonly List<Sprite> iconSprites = new List<Sprite>();
        private readonly List<Texture2D> iconTextures = new List<Texture2D>();
        private int[] previousResources;
        private GameObject buildModePanel;
        private TextMeshProUGUI buildModeText;
        private TextMeshProUGUI defenseText;
        private GameObject infoPanel;
        private TextMeshProUGUI infoTitle;
        private TextMeshProUGUI infoBody;
        private Button infoPrimary;
        private Button infoSecondary;
        private string selectedBuilding;

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
                rect.anchoredPosition = new Vector2(80 + i * 131, 3);
                rect.sizeDelta = new Vector2(128, 24);
                resourceChips.Add(rect);
                GameObject icon = new GameObject(labels[i] + " icono", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(chip.transform, false);
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0, .5f);
                iconRect.anchorMax = new Vector2(0, .5f);
                iconRect.pivot = new Vector2(0, .5f);
                iconRect.anchoredPosition = new Vector2(0, 0);
                iconRect.sizeDelta = new Vector2(18, 18);
                Image image = icon.GetComponent<Image>();
                image.sprite = MakeResourceIcon(i);
                image.color = colors[i];
                image.raycastTarget = false;
                TextMeshProUGUI text = Label("", chip.transform, 13, FontStyles.Bold, SpaceFarmPalette.Text,
                    new Vector2(0,0),new Vector2(0,0),new Vector2(0,0),new Vector2(22,0),new Vector2(106,24));
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = true;
                text.fontSizeMin = 11;
                text.fontSizeMax = 13;
                resourceLabels.Add(text);
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
                    case 0: on = Mathf.Abs(x - 10) + Mathf.Abs(y - 10) < 8 && y > 3; break;
                    case 1: on = (Mathf.Abs(x - (14f - y * .48f)) <= 2f && y >= 3 && y <= 17) ||
                        (y >= 9 && y <= 11 && x >= 7 && x <= 14); break;
                    case 2: on = x >= 5 && x <= 14 && y >= 4 && y <= 15 &&
                        ((x == 5 || x == 14) || y == 4 || y == 15 || (y == 10 && x > 7 && x < 12)); break;
                    case 3: on = y < 15 && Mathf.Abs(x - 10) <= (15 - y) * .53f && y > 4; break;
                    case 4: on = (y >= 5 && y <= 13 && x >= 5 && x <= 14 &&
                        (y == 5 || y == 13 || x == 5 || x == 14)) || (y == 10 && x >= 7 && x <= 12); break;
                    case 5: on = (Mathf.Pow(x - 10,2) + Mathf.Pow(y - 14,2) <= 9) ||
                        (y >= 3 && y <= 9 && x >= 5 && x <= 15); break;
                    case 6: on = (x >= 8 && x <= 11 && y >= 3 && y <= 17) ||
                        (y >= 8 && y <= 11 && x >= 3 && x <= 17); break;
                }
                pixels[y * 20 + x] = on ? new Color32(255,255,255,255) : new Color32(0,0,0,0);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0,0,20,20), new Vector2(.5f,.5f),20);
            iconTextures.Add(texture);
            iconSprites.Add(sprite);
            return sprite;
        }

        private void RefreshResourceHud()
        {
            if (resourceLabels.Count == 0 || state == null) return;
            int[] now = { state.Iron, state.Energy, state.Oxygen, state.Water, state.Food, state.Population, state.Health };
            string[] names = { "Hierro", "Energía", "Oxígeno", "Agua", "Comida", "Colonos", "Salud" };
            for (int i = 0; i < now.Length; i++)
            {
                string value = i == 6 ? now[i] + "/3" : now[i].ToString();
                bool critical = (i == 2 || i == 3 || i == 4) && now[i] <= state.Population * 2;
                resourceLabels[i].text = critical ? $"<color=#EF806C>{names[i]} {value} !</color>" : $"{names[i]} {value}";
                if (previousResources != null && now[i] != previousResources[i])
                    StartCoroutine(ResourceDelta(resourceChips[i], now[i] - previousResources[i]));
            }
            previousResources = now;
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
            Transform canvas = GameObject.Find("Space Farm HUD").transform;
            AddButton("VOLVER A LA BASE", canvas, () => FocusWorld(CellPosition(BaseCell)), 12,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),
                new Vector2(15,-106),new Vector2(155,31));
            AddButton("IR AL ROVER", canvas, () => FocusWorld(rover.transform.position), 12,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),
                new Vector2(15,-143),new Vector2(155,31));
            defenseText = Label("", canvas, 12, FontStyles.Bold, SpaceFarmPalette.Iron,
                new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),
                new Vector2(18,-180),new Vector2(160,24));

            buildModePanel = Panel("Modo construcción", canvas, SpaceFarmPalette.PanelRaised,
                new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(.5f,1),
                new Vector2(0,-105),new Vector2(480,54));
            buildModePanel.GetComponent<Image>().raycastTarget = true;
            buildModeText = Label("", buildModePanel.transform, 15, FontStyles.Bold, SpaceFarmPalette.Iron,
                new Vector2(0,0),new Vector2(0,1),new Vector2(0,.5f),new Vector2(10,0),new Vector2(350,-8));
            AddButton("TERMINAR", buildModePanel.transform, EndBarrierMode, 12,
                SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),
                new Vector2(-9,0),new Vector2(106,35));
            buildModePanel.SetActive(false);

            infoPanel = Panel("Ficha de instalación", canvas, SpaceFarmPalette.Panel,
                new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),
                new Vector2(-15,-108),new Vector2(285,208));
            infoPanel.GetComponent<Image>().raycastTarget = true;
            infoTitle = Label("", infoPanel.transform, 18, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(0,1),new Vector2(1,1),new Vector2(0,1),
                new Vector2(12,-8),new Vector2(-24,29));
            infoBody = Label("", infoPanel.transform, 12, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(1,1),new Vector2(0,1),
                new Vector2(12,-39),new Vector2(-24,110));
            infoBody.alignment = TextAlignmentOptions.TopLeft;
            infoPrimary = AddButton("RECOGER", infoPanel.transform, StartMineCollection, 11,
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
        }

        private void FocusWorld(Vector3 position)
        {
            gameCamera.transform.position = new Vector3(position.x, position.y, -10f);
            ClampCamera();
        }

        private string SummarizeSol()
        {
            SpaceFarmState.SolResults sol = state.LastSol;
            string text = $"Sol {state.Sol:00}: solar +{sol.Solar.Actual}, O2 +{sol.OxygenOne.Actual + sol.OxygenTwo.Actual}, " +
                $"agua +{sol.Water.Actual}, comida +{sol.Greenhouse.Actual}, mina +{sol.Mine.Actual} Fe almacenado. " +
                $"Tripulación -{sol.CrewConsumed} recursos vitales.";
            if (sol.Storm) text += " Tormenta de polvo.";
            if (sol.RaidFoodLoss > 0)
                text += $" Incursión ({state.BarrierCount}/12): -{sol.RaidFoodLoss} comida" +
                    (sol.RaidDamagedMine ? ", mina averiada." : ", mina protegida.");
            if (state.GameOver) text += " Misión perdida.";
            else if (state.MissionComplete) text += " ¡Misión completa!";
            return text;
        }

        private void BeginBarrierMode()
        {
            barrierMode = true;
            HideWorldSelection();
            buildModePanel.SetActive(true);
            RefreshSystemsHud();
            RefreshSystems();
            SetReport("Elegí una posición iluminada del perímetro. Cada clic coloca un segmento por 1 Fe; Escape cancela.");
        }

        private void EndBarrierMode()
        {
            barrierMode = false;
            if (barrierPreview != null) barrierPreview.SetActive(false);
            if (barrierPreviewVertical != null) barrierPreviewVertical.SetActive(false);
            if (buildModePanel != null) buildModePanel.SetActive(false);
            RefreshSystems();
            SetReport($"Construcción terminada. Perímetro {state.BarrierCount}/{SpaceFarmState.BarrierSlotCount}.");
        }

        private bool SelectWorldBuilding(string building)
        {
            selectedBuilding = building;
            infoPanel.SetActive(true);
            RefreshSystemsHud();
            return true;
        }

        private void HideWorldSelection()
        {
            selectedBuilding = null;
            if (infoPanel != null) infoPanel.SetActive(false);
        }

        private void ContextSecondaryAction()
        {
            if (selectedBuilding != "mine") return;
            if (state.MineDamaged) Act(state.RepairMine);
            else Act(state.UpgradeMine);
            RefreshSystemsHud();
        }

        private void RefreshSystemsHud()
        {
            if (state == null) return;
            if (buildModeText != null)
                buildModeText.text = $"BARRERA  ·  1 Fe por segmento\nPerímetro {state.BarrierCount}/12  ·  Hierro {state.Iron}";
            if (defenseText != null)
                defenseText.text = $"PERÍMETRO {state.BarrierCount}/12";
            if (infoPanel == null || string.IsNullOrEmpty(selectedBuilding)) return;
            SpaceFarmState.SystemResult result = null;
            string title = "";
            string status = "Operativa";
            string extra = "";
            switch (selectedBuilding)
            {
                case "mine":
                    title = $"Mina de hierro · nivel {state.MineLevel}";
                    result = state.LastSol.Mine;
                    status = state.MineDamaged ? "Averiada" : state.MineStored == state.MineCapacity ? "Almacén lleno" : "Operativa";
                    extra = $"Almacén {state.MineStored}/{state.MineCapacity} Fe  ·  Rover {state.RoverCargo}/{state.RoverCapacity} Fe";
                    break;
                case "oxygen1": title = "Planta de oxígeno 1"; result = state.LastSol.OxygenOne; break;
                case "oxygen2": title = "Planta de oxígeno 2"; result = state.LastSol.OxygenTwo; break;
                case "water": title = "Extractor de agua"; result = state.LastSol.Water; break;
                case "greenhouse": title = "Invernadero"; result = state.LastSol.Greenhouse;
                    extra = $"Insumo: {result.OtherUsed} agua en el último sol."; break;
                case "solar": title = "Generación solar"; result = state.LastSol.Solar;
                    extra = $"Batería {state.Energy}/30  ·  Tormenta: {(state.LastSol.Storm ? "sí" : "no")}"; break;
                default: title = "Hábitat"; extra = $"Colonos {state.Population}/4  ·  Salud {state.Health}/3"; break;
            }
            infoTitle.text = title;
            if (result == null) infoBody.text = extra;
            else
            {
                string reason = result.Reason;
                if (selectedBuilding == "mine" && state.MineDamaged) reason = "Averiada: repará la mina.";
                if (status == "Operativa" && result.Nominal == 0 && state.Sol > 1)
                    status = "Sin ciclo todavía";
                else if (status == "Operativa" && result.Nominal > 0 && result.Actual == 0 &&
                    state.Sol > 1) status = "Detenida";
                int nominal = selectedBuilding == "mine" ? (state.MineLevel == 2 ? 5 : 3) :
                    selectedBuilding == "solar" ? 9 : selectedBuilding == "water" ? 5 :
                    selectedBuilding == "greenhouse" ? 4 : 4;
                int expectedEnergy = selectedBuilding == "mine" ? (state.MineLevel == 2 ? 3 : 2) :
                    selectedBuilding == "solar" ? 0 : selectedBuilding == "water" ||
                    selectedBuilding == "greenhouse" ? 2 : 3;
                infoBody.text = $"Estado: {status}\nNominal: {nominal}/sol  ·  Real último: {result.Actual}\nEnergía: {expectedEnergy}/sol (usó {result.EnergyUsed})\n{extra}\n{reason}";
            }
            infoPrimary.gameObject.SetActive(selectedBuilding == "mine");
            infoPrimary.interactable = selectedBuilding == "mine" && state.MineStored > 0 &&
                !collectionActive && travel == null && !state.GameOver;
            infoSecondary.gameObject.SetActive(selectedBuilding == "mine" && (state.MineDamaged || state.MineLevel < 2));
            infoSecondary.interactable = !state.GameOver;
            if (selectedBuilding == "mine")
                infoSecondary.GetComponentInChildren<TextMeshProUGUI>().text = state.MineDamaged ? "REPARAR" : "MEJORAR";
        }

        private void DisposeSystems()
        {
            foreach (Sprite sprite in iconSprites) if (sprite != null) Destroy(sprite);
            foreach (Texture2D texture in iconTextures) if (texture != null) Destroy(texture);
        }
    }
}

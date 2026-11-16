using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>Dedicated crew roster: select an astronaut, then choose an available workplace.</summary>
    public sealed partial class SpaceFarmGame
    {
        private sealed class CrewStation
        {
            public CrewJob Job;
            public int Target;
            public string Name;
        }

        private const int CrewRowsPerPage = 6;
        private readonly List<Button> crewRowButtons = new List<Button>();
        private readonly List<Button> stationRowButtons = new List<Button>();
        private readonly List<CrewStation> crewStations = new List<CrewStation>();
        private Button crewMenuToggle;
        private GameObject crewMenuRoot;
        private TextMeshProUGUI crewSummary;
        private TextMeshProUGUI crewPageLabel;
        private TextMeshProUGUI stationPageLabel;
        private int selectedCrewId = 1;
        private int crewPage;
        private int stationPage;
        private bool crewMenuOpen;

        private void CreateCrewMenu()
        {
            crewMenuRoot = Panel("Administrar astronautas", hudCanvas.transform,
                new Color(.025f, .045f, .07f, .88f), Vector2.zero, Vector2.one,
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            crewMenuRoot.GetComponent<Image>().raycastTarget = true;
            GameObject panel = Panel("Panel de tripulación", crewMenuRoot.transform, SpaceFarmPalette.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(850, 500));
            panel.GetComponent<Image>().raycastTarget = true;
            Label("TRIPULACIÓN  /  ASIGNACIONES", panel.transform, 22, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(650, 32));
            AddButton("CERRAR", panel.transform, CloseCrewMenu, 12, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -14), new Vector2(110, 34));
            crewSummary = Label("", panel.transform, 13, FontStyles.Normal, SpaceFarmPalette.MutedText,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -57), new Vector2(-40, 38));
            Label("1 · ELEGÍ UN ASTRONAUTA", panel.transform, 14, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -105), new Vector2(390, 25));
            Label("2 · ELEGÍ UNA INSTALACIÓN", panel.transform, 14, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(445, -105), new Vector2(380, 25));
            for (int i = 0; i < CrewRowsPerPage; i++)
            {
                int row = i;
                float y = -137 - 44 * i;
                crewRowButtons.Add(AddButton("", panel.transform, () => SelectCrewRow(row), 12,
                    SpaceFarmPalette.Button, SpaceFarmPalette.Text,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(20, y), new Vector2(390, 38)));
                stationRowButtons.Add(AddButton("", panel.transform, () => AssignCrewRow(row), 12,
                    SpaceFarmPalette.Button, SpaceFarmPalette.Text,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(445, y), new Vector2(380, 38)));
            }
            AddButton("ANTERIORES", panel.transform, () => { crewPage = Mathf.Max(0, crewPage - 1); RefreshCrewMenu(); },
                11, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -411), new Vector2(110, 34));
            crewPageLabel = Label("", panel.transform, 12, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, -411), new Vector2(125, 34));
            AddButton("SIGUIENTES", panel.transform, () => { crewPage++; RefreshCrewMenu(); },
                11, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(280, -411), new Vector2(130, 34));
            AddButton("ANTERIORES", panel.transform, () => { stationPage = Mathf.Max(0, stationPage - 1); RefreshCrewMenu(); },
                11, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(445, -411), new Vector2(110, 34));
            stationPageLabel = Label("", panel.transform, 12, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(565, -411), new Vector2(125, 34));
            AddButton("SIGUIENTES", panel.transform, () => { stationPage++; RefreshCrewMenu(); },
                11, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(705, -411), new Vector2(130, 34));
            AddButton("DEJAR TAREA / DESCANSAR", panel.transform, UnassignSelectedCrew,
                12, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -456), new Vector2(390, 34));
            Label("El bono se aplica por sol mientras trabaja; al cansarse descansa automáticamente.", panel.transform,
                11, FontStyles.Normal, SpaceFarmPalette.MutedText,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(445, -456), new Vector2(385, 34));
            crewMenuRoot.SetActive(false);
        }

        private void ToggleCrewMenu()
        {
            if (crewMenuOpen) CloseCrewMenu();
            else OpenCrewMenu();
        }

        private void OpenCrewMenu()
        {
            if (paused || titleScreenOpen || state.GameOver) return;
            if (editMode) SetEditMode(false);
            CancelEditMove();
            if (constructionMenuOpen) CloseConstructionMenu();
            if (barrierMode) EndBarrierMode();
            if (activeStructure != StructureKind.None) CancelPlacement(false);
            if (infoPanel != null) infoPanel.SetActive(false);
            leftPressValid = false;
            crewMenuOpen = true;
            SetNavigationButtonsVisible(false);
            crewMenuRoot.SetActive(true);
            crewMenuRoot.transform.SetAsLastSibling();
            crewMenuToggle.GetComponentInChildren<TextMeshProUGUI>().text = SpaceFarmLocalization.T("CERRAR EQUIPO");
            RefreshCrewMenu();
        }

        private void CloseCrewMenu()
        {
            crewMenuOpen = false;
            if (crewMenuRoot != null) crewMenuRoot.SetActive(false);
            if (crewMenuToggle != null) crewMenuToggle.GetComponentInChildren<TextMeshProUGUI>().text = SpaceFarmLocalization.T("ASTRONAUTAS");
            SetNavigationButtonsVisible(true);
            if (infoPanel != null && selectedBuilding != null && activeStructure == StructureKind.None)
            {
                infoPanel.SetActive(true);
                RefreshSystemsHud();
            }
        }

        private void SelectCrewRow(int row)
        {
            int index = crewPage * CrewRowsPerPage + row;
            if (index < state.Crew.Count)
            {
                selectedCrewId = state.Crew[index].Id;
                RefreshCrewMenu();
            }
        }

        private void AssignCrewRow(int row)
        {
            int index = stationPage * CrewRowsPerPage + row;
            if (index >= crewStations.Count) return;
            CrewStation station = crewStations[index];
            state.AssignCrew(selectedCrewId, station.Job, station.Target, out string message);
            SetReport(message);
            RefreshCrewMenu();
            RefreshHud();
        }

        private void UnassignSelectedCrew()
        {
            state.AssignCrew(selectedCrewId, CrewJob.None, -1, out string message);
            SetReport(message);
            RefreshCrewMenu();
            RefreshHud();
        }

        private void RefreshCrewMenu()
        {
            if (!crewMenuOpen || state == null) return;
            SpaceFarmState.CrewMember selected = null;
            foreach (SpaceFarmState.CrewMember member in state.Crew)
                if (member.Id == selectedCrewId) { selected = member; break; }
            if (selected == null && state.Crew.Count > 0) { selected = state.Crew[0]; selectedCrewId = selected.Id; }
            crewStations.Clear();
            crewStations.Add(new CrewStation { Job = CrewJob.Solar, Target = 0, Name = "Paneles solares" });
            for (int i = 0; i < state.OxygenPlants; i++)
                crewStations.Add(new CrewStation { Job = CrewJob.Oxygen, Target = i, Name = $"Planta de O2 {i + 1}" });
            foreach (SpaceFarmState.DepositState deposit in state.Deposits)
            {
                if (deposit.Extractor) crewStations.Add(new CrewStation { Job = CrewJob.Extractor, Target = deposit.Id, Name = $"Extractor {deposit.Name}" });
                if (deposit.MineLevel > 0) crewStations.Add(new CrewStation { Job = CrewJob.Mine, Target = deposit.Id, Name = $"Mina {deposit.Name}" });
            }
            for (int i = 0; i < state.GreenhouseCells.Count; i++)
                crewStations.Add(new CrewStation { Job = CrewJob.Greenhouse, Target = i, Name = $"Invernadero {i + 1}" });
            crewPage = Mathf.Clamp(crewPage, 0, Mathf.Max(0, (state.Crew.Count - 1) / CrewRowsPerPage));
            stationPage = Mathf.Clamp(stationPage, 0, Mathf.Max(0, (crewStations.Count - 1) / CrewRowsPerPage));
            string stationName = "sin tarea";
            foreach (CrewStation station in crewStations)
                if (selected != null && station.Job == selected.Job && station.Target == selected.Target) { stationName = station.Name; break; }
            crewSummary.text = selected == null ? SpaceFarmLocalization.T("No hay astronautas.") :
                SpaceFarmLocalization.F("Astronauta {0}: {1} · energía {2:0}% · {3}. Libres: {4}/{5}.",
                    selected.Id, SpaceFarmLocalization.T(stationName), selected.Energy,
                    SpaceFarmLocalization.T(selected.Resting ? "DESCANSANDO" : selected.Job == CrewJob.None ? "LIBRE" : "TRABAJANDO"),
                    state.FreeWorkers, state.Population);
            crewPageLabel.text = $"{crewPage + 1}/{Mathf.Max(1, (state.Crew.Count + CrewRowsPerPage - 1) / CrewRowsPerPage)}";
            stationPageLabel.text = $"{stationPage + 1}/{Mathf.Max(1, (crewStations.Count + CrewRowsPerPage - 1) / CrewRowsPerPage)}";
            for (int row = 0; row < CrewRowsPerPage; row++)
            {
                int crewIndex = crewPage * CrewRowsPerPage + row;
                Button crewButton = crewRowButtons[row];
                crewButton.gameObject.SetActive(crewIndex < state.Crew.Count);
                if (crewIndex < state.Crew.Count)
                {
                    SpaceFarmState.CrewMember member = state.Crew[crewIndex];
                    crewButton.GetComponentInChildren<TextMeshProUGUI>().text =
                        SpaceFarmLocalization.F("A-{0:00} · {1} · {2:0}%", member.Id,
                            SpaceFarmLocalization.T(member.Resting ? "DURMIENDO" : member.Job == CrewJob.None ? "LIBRE" : "TRABAJANDO"), member.Energy);
                    crewButton.GetComponent<Image>().color = member.Id == selectedCrewId ? SpaceFarmPalette.Iron : SpaceFarmPalette.Button;
                }
                int stationIndex = stationPage * CrewRowsPerPage + row;
                Button stationButton = stationRowButtons[row];
                stationButton.gameObject.SetActive(stationIndex < crewStations.Count);
                if (stationIndex < crewStations.Count)
                {
                    CrewStation station = crewStations[stationIndex];
                    SpaceFarmState.CrewMember occupant = state.WorkerOn(station.Job, station.Target);
                    bool available = occupant == null || occupant.Id == selectedCrewId;
                    stationButton.interactable = available;
                    stationButton.GetComponentInChildren<TextMeshProUGUI>().text =
                        SpaceFarmLocalization.F("{0} · +{1}/sol", SpaceFarmLocalization.T(station.Name), SpaceFarmState.WorkerBonus(station.Job)) +
                        (occupant == null ? "" : SpaceFarmLocalization.F(" · A-{0:00}", occupant.Id));
                    stationButton.GetComponent<Image>().color = occupant != null && occupant.Id == selectedCrewId ? SpaceFarmPalette.Iron : SpaceFarmPalette.Button;
                }
            }
        }
    }
}

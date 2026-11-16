using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SpaceFarm
{
    /// <summary>Pointer modes and the visual blueprint. The rules and resource transaction live in SpaceFarmState.</summary>
    public sealed partial class SpaceFarmGame
    {
        private enum InteractionMode { Explore, DraggingStructure, PlacingStructure, PlacingBarrier, EditingStructure, PanningCamera }

        private InteractionMode interactionMode;
        private StructureKind activeStructure;
        private bool suppressRightPan;
        private SpriteRenderer placementPreview;
        private SpriteRenderer placementFootprint;
        private RectTransform placementTooltip;
        private TextMeshProUGUI placementTooltipText;
        private readonly List<SpriteRenderer> placementDepositHighlights = new List<SpriteRenderer>();
        private bool editMode;
        private bool editMoveActive;
        private StructureKind editKind;
        private int editIndex = -1;
        private SpriteRenderer editPreview;

        public bool IsPlacingStructure => activeStructure != StructureKind.None;
        public bool IsConstructionMenuOpen => constructionMenuOpen;
        public StructureKind ActiveStructure => activeStructure;
        public void OpenConstructionForTools() => OpenConstructionMenu();
        public void CloseConstructionForTools() => CloseConstructionMenu();
        public void BeginPlacementForTools(StructureKind kind) => BeginStructurePlacement(kind);
        public void ConfirmPlacementForTools(Vector2Int cell) => ConfirmPlacement(cell);
        public void CancelPlacementForTools() => CancelPlacement();
        public bool EditModeForTools => editMode;

        private void SetEditMode(bool active)
        {
            editMode = active;
            if (active)
            {
                CancelPlacement(false);
                EndBarrierMode();
                CloseConstructionMenu();
                if (crewMenuOpen) CloseCrewMenu();
                SetReport("MODO EDITAR · Seleccioná un muro o instalación. La ficha permite mover o eliminar.");
            }
            else
            {
                CancelEditMove();
                SetReport("Modo explorar · clic para seleccionar y enviar el rover.");
            }
            RefreshHud();
        }

        private bool BeginEditMoveSelected()
        {
            if (!editMode || string.IsNullOrEmpty(selectedBuilding)) return false;
            editKind = StructureKind.None; editIndex = -1;
            if (selectedBuilding == "wall") { editKind = StructureKind.Barrier; editIndex = selectedBuildingIndex; }
            else if (selectedBuilding == "greenhouse") { editKind = StructureKind.Greenhouse; editIndex = selectedBuildingIndex; }
            else if (selectedBuilding == "oxygen1") { editKind = StructureKind.OxygenPlant; editIndex = -1; }
            else if (selectedBuilding == "oxygen2") { editKind = StructureKind.OxygenPlant; editIndex = selectedBuildingIndex; }
            else if (selectedBuilding == "warehouse") { editKind = StructureKind.Warehouse; editIndex = selectedBuildingIndex; }
            if (editKind == StructureKind.None)
            {
                SetReport(selectedBuilding == "deposit" ? "Las minas y extractores están anclados a su yacimiento; se pueden eliminar, pero no mover." : "Esta estructura base está fija.");
                return false;
            }
            GameObject target = editKind == StructureKind.Barrier ? null : SelectedUpgradeTarget();
            Sprite sprite = editKind == StructureKind.Barrier ? Resources.Load<Sprite>("Art/space-farm-wall-post") : target != null ? target.GetComponent<SpriteRenderer>()?.sprite : null;
            if (sprite == null) { SetReport("No se encontró la imagen de esta estructura para moverla."); return false; }
            if (editPreview != null) Destroy(editPreview.gameObject);
            editPreview = CreateWorldSprite("Previsualización de movimiento", sprite, CellPosition(roverCell),
                editKind == StructureKind.Barrier ? new Vector3(.9f, .65f, 1f) : target.transform.localScale,
                new Color(.58f, 1f, .88f, .72f), 16, worldRoot.transform);
            editMoveActive = true;
            interactionMode = InteractionMode.EditingStructure;
            HideWorldSelection();
            SetReport("Mover instalación · apuntá a un sector válido y confirmá con clic. Escape cancela.");
            return true;
        }

        private bool SelectedEditTarget(out StructureKind kind, out int index, out bool movable)
        {
            kind = StructureKind.None; index = selectedBuildingIndex; movable = false;
            if (selectedBuilding == "wall") { kind = StructureKind.Barrier; movable = true; }
            else if (selectedBuilding == "greenhouse") { kind = StructureKind.Greenhouse; movable = true; }
            else if (selectedBuilding == "oxygen1") { kind = StructureKind.OxygenPlant; index = -1; movable = true; }
            else if (selectedBuilding == "oxygen2") { kind = StructureKind.OxygenPlant; movable = true; }
            else if (selectedBuilding == "warehouse") { kind = StructureKind.Warehouse; movable = true; }
            else if (selectedBuilding == "deposit")
            {
                index = selectedDeposit;
                SpaceFarmState.DepositState d = state.Deposit(index);
                if (d == null) return false;
                kind = d.MineLevel > 0 ? StructureKind.Mine : d.Extractor ? StructureKind.Extractor : StructureKind.None;
            }
            return kind != StructureKind.None;
        }

        private void UpdateEditMovePreview(Mouse mouse)
        {
            if (!editMoveActive || mouse == null || editPreview == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (PointerOverUi(pointer)) { editPreview.enabled = false; return; }
            editPreview.enabled = true;
            Vector2Int cell = WorldToCell(ScreenToWorld(pointer));
            editPreview.transform.position = CellPosition(cell) + new Vector3(0f, 0f, -.25f);
            string blocker = state.EditMoveBlocker(editKind, editIndex, cell, IsRevealed(cell));
            if (blocker.Length == 0 && ActorInEditFootprint(cell)) blocker = "No pases por encima de un astronauta o rover";
            editPreview.color = blocker.Length == 0 ? new Color(.58f, 1f, .88f, .72f) : new Color(1f, .42f, .35f, .72f);
            if (buildModeText != null) buildModeText.text = blocker.Length == 0 ? "SECTOR VÁLIDO · CLIC PARA MOVER" : blocker;
        }

        private bool ActorInEditFootprint(Vector2Int cell)
        {
            int radius = editKind == StructureKind.Barrier ? 0 : 1;
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                if (IsActorOccupyingCell(cell + new Vector2Int(x, y))) return true;
            return false;
        }

        private void ConfirmEditMove(Vector2Int cell)
        {
            if (!editMoveActive) return;
            if (ActorInEditFootprint(cell)) { SetReport("El rover o un astronauta ocupa esa huella. Elegí otro sector."); return; }
            bool moved = state.MoveStructure(editKind, editIndex, cell, IsRevealed(cell), out string message);
            SetReport(message);
            if (!moved) { RefreshHud(); return; }
            CancelEditMove();
            // Moving an obstacle can invalidate the rover's cached route. Recompute before its next step.
            roverRouteDirty = true;
            RefreshWorld(); RefreshHud();
            PlayWorldPing(CellPosition(cell), SpaceFarmPalette.Oxygen);
        }

        private void CancelEditMove()
        {
            editMoveActive = false;
            if (editPreview != null) Destroy(editPreview.gameObject);
            editPreview = null;
            if (interactionMode == InteractionMode.EditingStructure) interactionMode = InteractionMode.Explore;
        }

        private void ConfirmDeleteSelected()
        {
            if (!editMode || string.IsNullOrEmpty(selectedBuilding)) return;
            StructureKind kind = StructureKind.None; int index = selectedBuildingIndex;
            if (selectedBuilding == "wall") kind = StructureKind.Barrier;
            else if (selectedBuilding == "greenhouse") kind = StructureKind.Greenhouse;
            else if (selectedBuilding == "oxygen2") kind = StructureKind.OxygenPlant;
            else if (selectedBuilding == "warehouse") kind = StructureKind.Warehouse;
            else if (selectedBuilding == "deposit")
            {
                SpaceFarmState.DepositState d = state.Deposit(selectedDeposit);
                if (d != null) kind = d.MineLevel > 0 ? StructureKind.Mine : d.Extractor ? StructureKind.Extractor : StructureKind.None;
                index = selectedDeposit;
            }
            if (kind == StructureKind.None) { SetReport("Esta estructura base no se puede eliminar."); return; }
            string blocker = state.RemovalBlocker(kind, index);
            if (blocker.Length > 0) { SetReport(blocker + "."); RefreshHud(); return; }
            ShowDeleteConfirmation(kind, index);
        }

        private void ShowDeleteConfirmation(StructureKind kind, int index)
        {
            GameObject dialog = Panel("Confirmar eliminación", hudCanvas.transform, new Color(.02f,.035f,.05f,.92f),
                Vector2.zero, Vector2.one, new Vector2(.5f,.5f), Vector2.zero, Vector2.zero);
            GameObject box = Panel("Confirmación de estructura", dialog.transform, SpaceFarmPalette.Panel,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(480,190));
            Label("ELIMINAR ESTRUCTURA", box.transform, 20, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-20),new Vector2(-30,34)).alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI detail = Label("Reembolso: aproximadamente 50 % del costo base. Los depósitos minerales permanecen en el terreno.",
                box.transform,13,FontStyles.Normal,SpaceFarmPalette.MutedText,new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-62),new Vector2(-44,48));
            detail.alignment = TextAlignmentOptions.Center; detail.enableAutoSizing = true; detail.fontSizeMin = 10;
            AddButton("ELIMINAR",box.transform,() => { bool removed = state.RemoveStructure(kind,index,out string message); Destroy(dialog); SetReport(message); if (removed) HideWorldSelection(); RefreshWorld(); RefreshHud(); },13,
                SpaceFarmPalette.Danger,SpaceFarmPalette.Text,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-92,20),new Vector2(170,40));
            AddButton("CANCELAR",box.transform,() => Destroy(dialog),13,SpaceFarmPalette.PanelRaised,SpaceFarmPalette.Text,
                new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(96,20),new Vector2(170,40));
        }

        private static string StructureName(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Mine: return "Mina de hierro";
                case StructureKind.Extractor: return "Extractor de agua";
                case StructureKind.Greenhouse: return "Invernadero";
                case StructureKind.OxygenPlant: return "Planta de O2";
                case StructureKind.Warehouse: return "Almacén de recursos";
                default: return "Estructura";
            }
        }

        private static string StructureArt(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Mine: return "Art/space-farm-iron-mine-animation#iron-mine-base-3d";
                case StructureKind.Extractor: return "Art/space-farm-world-modules#water-3d";
                case StructureKind.Greenhouse: return "Art/space-farm-world-modules#greenhouse-3d";
                case StructureKind.Warehouse: return "Art/space-farm-warehouse";
                default: return "Art/space-farm-world-modules#oxygen-3d";
            }
        }

        private void BeginStructurePlacement(StructureKind kind, bool dragging = false)
        {
            if (paused || titleScreenOpen || state.GameOver) return;
            string blocker = state.PlacementAvailability(kind);
            if (blocker.Length > 0) { SetReport(blocker + "."); RefreshHud(); return; }
            if (barrierMode) EndBarrierMode();
            CancelPlacement(false);
            activeStructure = kind;
            interactionMode = dragging ? InteractionMode.DraggingStructure : InteractionMode.PlacingStructure;
            HideWorldSelection();
            if (!dragging) CloseConstructionMenu();
            placementPreview = CreateArtwork("Plano " + StructureName(kind), StructureArt(kind), CellPosition(BaseCell),
                1.75f, 1.17f, 12, worldRoot.transform);
            placementFootprint = CreateWorldSprite("Huella de construcción", ringSprite, CellPosition(BaseCell),
                new Vector3(1.8f, 1.15f, 1f), SpaceFarmPalette.Oxygen, 11, worldRoot.transform);
            placementTooltip = Panel("Estado del plano", hudCanvas.transform, SpaceFarmPalette.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 0), Vector2.zero, new Vector2(280, 55))
                .GetComponent<RectTransform>();
            placementTooltipText = Label("", placementTooltip, 12, FontStyles.Bold, SpaceFarmPalette.Text,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-14, -8));
            placementTooltipText.enableAutoSizing = true;
            placementTooltipText.fontSizeMin = 9;
            placementTooltip.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            if (buildModePanel != null) buildModePanel.SetActive(true);
            if ((kind == StructureKind.Mine || kind == StructureKind.Extractor) &&
                depositViews != null && placementDepositHighlights.Count == 0)
                foreach (DepositView deposit in depositViews)
                {
                    SpriteRenderer ring = CreateWorldSprite("Yacimiento apto", ringSprite,
                        CellPosition(deposit.Info.Cell) + new Vector3(0f, 0f, -.27f),
                        new Vector3(1.65f, 1.25f, 1f), SpaceFarmPalette.Oxygen, 8, worldRoot.transform);
                    ring.enabled = false;
                    placementDepositHighlights.Add(ring);
                }
            SetReport($"Plano: {StructureName(kind)}. Elegí un sector válido; clic confirma, clic derecho o Esc cancela.");
        }

        private void UpdatePlacementPreview(UnityEngine.InputSystem.Mouse mouse)
        {
            if (activeStructure == StructureKind.None || mouse == null || placementPreview == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            Vector2Int cell = WorldToCell(ScreenToWorld(pointer));
            Vector3 position = CellPosition(cell) + new Vector3(0f, 0f, -.2f);
            placementPreview.transform.position = position;
            placementFootprint.transform.position = position + new Vector3(0f, 0f, -.02f);
            bool overUi = PointerOverUi(pointer);
            bool roverCanMoveAside = (activeStructure == StructureKind.Mine || activeStructure == StructureKind.Extractor) &&
                !RoverBusy && roverCell == cell && TryRoverAsideCell(cell, out _);
            string reason = overUi ? "Soltá sobre el mapa" : IsActorOccupyingCell(cell) && !roverCanMoveAside
                ? "No coloqués una estructura debajo del rover"
                : state.PlacementBlocker(activeStructure, cell, IsRevealed(cell));
            bool valid = reason.Length == 0;
            placementPreview.enabled = placementFootprint.enabled = !overUi;
            placementPreview.color = valid ? new Color(.78f, 1f, .94f, .72f) : new Color(1f, .52f, .47f, .68f);
            placementFootprint.color = valid ? new Color(.48f, 1f, .88f, .8f) : new Color(1f, .31f, .27f, .8f);
            placementTooltipText.text = SpaceFarmLocalization.F("{0}\n{1} Fe · tenés {2} Fe",
                valid ? SpaceFarmLocalization.T("VÁLIDO · CLIC PARA COLOCAR")
                    : SpaceFarmLocalization.T("NO DISPONIBLE ·") + " " + SpaceFarmLocalization.T(reason),
                state.StructureCost(activeStructure), state.Iron);
            placementTooltipText.color = valid ? SpaceFarmPalette.Oxygen : SpaceFarmPalette.Danger;
            if (buildModeText != null)
                buildModeText.text = SpaceFarmLocalization.F("{0} · {1} Fe (tenés {2})\n{3}",
                    SpaceFarmLocalization.T(StructureName(activeStructure)), state.StructureCost(activeStructure), state.Iron,
                    SpaceFarmLocalization.T(valid ? "Sector válido · clic para construir" : reason));
            RectTransform canvasRect = hudCanvas.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer, null, out Vector2 local))
            {
                Rect bounds = canvasRect.rect;
                placementTooltip.anchoredPosition = new Vector2(
                    Mathf.Clamp(local.x + 18f, bounds.xMin + 4f, bounds.xMax - 284f),
                    Mathf.Clamp(local.y - 62f, bounds.yMin + 4f, bounds.yMax - 60f));
            }
            for (int i = 0; i < placementDepositHighlights.Count; i++)
            {
                MapDeposit deposit = map.Deposits[i];
                placementDepositHighlights[i].enabled = activeStructure == StructureKind.Mine || activeStructure == StructureKind.Extractor
                    ? state.PlacementBlocker(activeStructure, deposit.Cell, IsRevealed(deposit.Cell)).Length == 0 : false;
            }
        }

        private bool PointerOverUi(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screen };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            return hits.Count > 0;
        }

        private void ConfirmPlacement(Vector2Int cell)
        {
            if (activeStructure == StructureKind.None || paused || titleScreenOpen) return;
            StructureKind kind = activeStructure;
            bool placed;
            string message;
            Vector2Int aside = default;
            bool roverCanMoveAside = (kind == StructureKind.Mine || kind == StructureKind.Extractor) &&
                !RoverBusy && roverCell == cell && TryRoverAsideCell(cell, out aside);
            if (IsActorOccupyingCell(cell) && !roverCanMoveAside)
            {
                placed = false;
                message = "No coloqués una estructura debajo del rover.";
            }
            else placed = state.PlaceStructure(kind, cell, IsRevealed(cell), out message);
            SetReport(message);
            if (!placed) { RefreshHud(); return; }
            if (roverCanMoveAside) MoveRoverAside(aside);
            CancelPlacement(false);
            CloseConstructionMenu();
            PlayWorldPing(CellPosition(cell), SpaceFarmPalette.Oxygen);
            FlashScreen(SpaceFarmPalette.Oxygen, .09f, .2f);
            RefreshWorld();
            RefreshHud();
            if (kind == StructureKind.Greenhouse) SelectWorldBuilding("greenhouse", state.GreenhouseCells.Count - 1);
            else if (kind == StructureKind.OxygenPlant) SelectWorldBuilding("oxygen2", state.OxygenCells.Count - 1);
            else
            {
                MapDeposit deposit = map.DepositAt(cell);
                if (deposit != null) SelectDeposit(deposit.Id);
                else HideWorldSelection();
            }
        }

        private bool TryRoverAsideCell(Vector2Int builtCell, out Vector2Int result)
        {
            for (int radius = 2; radius <= 4; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                Vector2Int candidate = builtCell + new Vector2Int(x, y);
                if (!map.Inside(candidate) || !state.CanRoverReach(candidate) || state.IsCellBlocked(candidate)) continue;
                result = candidate;
                return true;
            }
            result = default;
            return false;
        }

        private void MoveRoverAside(Vector2Int candidate)
        {
            roverCell = roverDestination = candidate;
            rover.transform.position = CellPosition(candidate) + new Vector3(0f, 0f, -.5f);
            routeMarker.enabled = false;
        }

        private void CancelPlacement(bool report = true)
        {
            if (activeStructure == StructureKind.None) return;
            activeStructure = StructureKind.None;
            interactionMode = InteractionMode.Explore;
            if (placementPreview != null) Destroy(placementPreview.gameObject);
            if (placementFootprint != null) Destroy(placementFootprint.gameObject);
            if (placementTooltip != null) Destroy(placementTooltip.gameObject);
            placementPreview = placementFootprint = null;
            placementTooltip = null;
            foreach (SpriteRenderer ring in placementDepositHighlights) if (ring != null) ring.enabled = false;
            if (buildModePanel != null) buildModePanel.SetActive(false);
            if (report) SetReport("Plano cancelado. No se gastó hierro.");
        }

        private void DropCardPlacement(Vector2 screen)
        {
            if (interactionMode != InteractionMode.DraggingStructure || activeStructure == StructureKind.None) return;
            if (!PointerOverUi(screen)) ConfirmPlacement(WorldToCell(ScreenToWorld(screen)));
            else SetReport("Soltá el plano sobre un sector válido del mapa.");
            if (activeStructure != StructureKind.None) CancelPlacement(false);
        }
    }
}

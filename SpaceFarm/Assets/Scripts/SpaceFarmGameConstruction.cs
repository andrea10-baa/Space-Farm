using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceFarm
{
    /// <summary>Pointer modes and the visual blueprint. The rules and resource transaction live in SpaceFarmState.</summary>
    public sealed partial class SpaceFarmGame
    {
        private enum InteractionMode { Explore, DraggingStructure, PlacingStructure, PlacingBarrier, PanningCamera }

        private InteractionMode interactionMode;
        private StructureKind activeStructure;
        private bool suppressRightPan;
        private SpriteRenderer placementPreview;
        private SpriteRenderer placementFootprint;
        private RectTransform placementTooltip;
        private TextMeshProUGUI placementTooltipText;
        private readonly List<SpriteRenderer> placementDepositHighlights = new List<SpriteRenderer>();

        public bool IsPlacingStructure => activeStructure != StructureKind.None;
        public bool IsConstructionMenuOpen => constructionMenuOpen;
        public StructureKind ActiveStructure => activeStructure;
        public void OpenConstructionForTools() => OpenConstructionMenu();
        public void CloseConstructionForTools() => CloseConstructionMenu();
        public void BeginPlacementForTools(StructureKind kind) => BeginStructurePlacement(kind);
        public void ConfirmPlacementForTools(Vector2Int cell) => ConfirmPlacement(cell);
        public void CancelPlacementForTools() => CancelPlacement();

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
            placementTooltipText.text = $"{(valid ? "VÁLIDO · CLIC PARA COLOCAR" : "NO DISPONIBLE · " + reason)}\n" +
                $"{state.StructureCost(activeStructure)} Fe · tenés {state.Iron} Fe";
            placementTooltipText.color = valid ? SpaceFarmPalette.Oxygen : SpaceFarmPalette.Danger;
            if (buildModeText != null)
                buildModeText.text = $"{StructureName(activeStructure)} · {state.StructureCost(activeStructure)} Fe (tenés {state.Iron})\n" +
                    (valid ? "Sector válido · clic para construir" : reason);
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
            else SelectDeposit(map.DepositAt(cell).Id);
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

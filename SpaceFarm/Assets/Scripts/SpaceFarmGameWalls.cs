using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SpaceFarm
{
    /// <summary>
    /// Mars fortification artwork. A wall occupies one sector; adjacent sectors share a full
    /// span, and the post at each sector covers every join and corner. The visual topology
    /// follows the same occupied cells used by rover and astronaut pathfinding.
    /// </summary>
    public sealed partial class SpaceFarmGame
    {
        private GameObject builtWallRoot;
        private string builtWallLayout = string.Empty;
        private string previewWallLayout = string.Empty;

        private static readonly Color WallCyan = new Color(.28f, .80f, .88f, 1f);

        private void CreateBarrierWorld()
        {
            builtWallRoot = new GameObject("Muralla marciana");
            builtWallRoot.transform.SetParent(worldRoot.transform, false);
            barrierPreview = new GameObject("Plano de muro modular");
            barrierPreview.transform.SetParent(worldRoot.transform, false);
            barrierPreview.SetActive(false);
        }

        private void RefreshWallNetwork()
        {
            if (state == null || worldRoot == null) return;
            StringBuilder signature = new StringBuilder(state.Barriers.Count * 9 + 8);
            signature.Append(state.ColonyLevel).Append('|');
            foreach (SpaceFarmState.BarrierState wall in state.Barriers)
                signature.Append(wall.Cell.x).Append(',').Append(wall.Cell.y).Append(';');
            string layout = signature.ToString();
            if (layout == builtWallLayout) return;
            builtWallLayout = layout;

            if (builtWallRoot != null)
            {
                builtWallRoot.SetActive(false);
                Destroy(builtWallRoot);
            }
            builtWallRoot = new GameObject("Muralla marciana");
            builtWallRoot.transform.SetParent(worldRoot.transform, false);

            HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
            foreach (SpaceFarmState.BarrierState wall in state.Barriers) cells.Add(wall.Cell);
            int level = Mathf.Clamp(state.ColonyLevel, 1, SpaceFarmState.MaxLevel);

            // Draw uninterrupted spans first; the chunky posts hide their ends at intersections.
            foreach (Vector2Int cell in cells)
            {
                Vector2Int right = cell + Vector2Int.right;
                Vector2Int up = cell + Vector2Int.up;
                if (cells.Contains(right)) DrawWallSpan(CellPosition(cell), CellPosition(right), true,
                    level, false, true, builtWallRoot.transform);
                if (cells.Contains(up)) DrawWallSpan(CellPosition(cell), CellPosition(up), false,
                    level, false, true, builtWallRoot.transform);
            }
            foreach (Vector2Int cell in cells)
                DrawWallPost(CellPosition(cell), level, false, true, builtWallRoot.transform);

            // The ghost must be rebuilt when a newly placed wall becomes its neighbour.
            previewWallLayout = string.Empty;
        }

        private void UpdateWallPreview(Vector2Int cell, bool show, bool valid)
        {
            if (!show || state == null || worldRoot == null)
            {
                HideWallPreview();
                return;
            }
            string layout = $"{builtWallLayout}|{cell.x},{cell.y}|{valid}";
            if (layout == previewWallLayout && barrierPreview != null)
            {
                barrierPreview.SetActive(true);
                return;
            }
            previewWallLayout = layout;
            if (barrierPreview != null)
            {
                barrierPreview.SetActive(false);
                Destroy(barrierPreview);
            }
            barrierPreview = new GameObject("Plano de muro modular");
            barrierPreview.transform.SetParent(worldRoot.transform, false);

            int level = Mathf.Clamp(state.ColonyLevel, 1, SpaceFarmState.MaxLevel);
            Vector3 center = CellPosition(cell);
            if (valid)
            {
                Vector2Int right = cell + Vector2Int.right;
                Vector2Int left = cell + Vector2Int.left;
                Vector2Int up = cell + Vector2Int.up;
                Vector2Int down = cell + Vector2Int.down;
                if (state.HasBarrierAt(right)) DrawWallSpan(center, CellPosition(right), true,
                    level, true, true, barrierPreview.transform);
                if (state.HasBarrierAt(left)) DrawWallSpan(CellPosition(left), center, true,
                    level, true, true, barrierPreview.transform);
                if (state.HasBarrierAt(up)) DrawWallSpan(center, CellPosition(up), false,
                    level, true, true, barrierPreview.transform);
                if (state.HasBarrierAt(down)) DrawWallSpan(CellPosition(down), center, false,
                    level, true, true, barrierPreview.transform);
            }
            DrawWallPost(center, level, true, valid, barrierPreview.transform);
        }

        private void HideWallPreview()
        {
            if (barrierPreview != null) barrierPreview.SetActive(false);
        }

        private void DrawWallSpan(Vector3 from, Vector3 to, bool horizontal, int level,
            bool preview, bool valid, Transform parent)
        {
            Vector3 middle = (from + to) * .5f;
            // The source art has transparent padding around its painted faces. Scale the
            // painted body to the posts, rather than measuring only the texture canvas.
            float width = horizontal ? Mathf.Abs(to.x - from.x) + .20f : .72f;
            float height = horizontal ? .66f : Mathf.Abs(to.y - from.y) + .19f;
            string art = horizontal ? "Art/space-farm-wall-horizontal" : "Art/space-farm-wall-vertical";
            WallArtwork(horizontal ? "Tramo este-oeste" : "Tramo norte-sur", art,
                middle + new Vector3(0f, -.045f, -.26f), width, height, 8, preview, valid, parent);
        }

        private void DrawWallPost(Vector3 center, int level, bool preview, bool valid, Transform parent)
        {
            WallArtwork("Pilar de unión", "Art/space-farm-wall-post",
                center + new Vector3(0f, -.025f, -.27f), .55f, .52f, 9, preview, valid, parent);
            if (level >= 3)
                WallLayer("Baliza de defensa", circleSprite, center + new Vector3(0f, -.02f, -.28f),
                    new Vector3(.055f, .055f, 1f), WallCyan, 10, preview, valid, parent);
        }

        private void WallArtwork(string name, string artPath, Vector3 position, float width, float height,
            int order, bool preview, bool valid, Transform parent)
        {
            SpriteRenderer renderer = CreateArtwork(name, artPath, position, width, height, order, parent);
            if (preview)
            {
                Color signal = valid ? WallCyan : SpaceFarmPalette.Danger;
                renderer.color = Color.Lerp(Color.white, signal, .48f);
                renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, .68f);
            }
        }

        private void WallLayer(string name, Sprite sprite, Vector3 position, Vector3 scale, Color baseColor,
            int order, bool preview, bool valid, Transform parent)
        {
            if (preview)
            {
                Color signal = valid ? WallCyan : SpaceFarmPalette.Danger;
                baseColor = Color.Lerp(baseColor, signal, .62f);
                baseColor.a *= .64f;
            }
            CreateWorldSprite(name, sprite, position,
                scale, baseColor, order, parent);
        }
    }
}

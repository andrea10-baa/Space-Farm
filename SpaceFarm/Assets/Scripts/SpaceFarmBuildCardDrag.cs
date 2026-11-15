using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceFarm
{
    /// <summary>Forwards a catalog card's drag without letting its Button also order the rover.</summary>
    public sealed class SpaceFarmBuildCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action Begin;
        public Action<Vector2> End;

        public void OnBeginDrag(PointerEventData eventData) => Begin?.Invoke();
        public void OnDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData) => End?.Invoke(eventData.position);
    }
}

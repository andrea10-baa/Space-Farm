using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceFarm
{
    /// <summary>Forwards presses and drags on the minimap picture. The UI event system consumes them, so they never reach the world.</summary>
    public sealed class SpaceFarmMinimapInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<Vector2, Camera> Navigate;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Navigate?.Invoke(eventData.position, eventData.pressEventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Navigate?.Invoke(eventData.position, eventData.pressEventCamera);
        }
    }
}

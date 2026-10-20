using UnityEngine;
using UnityEngine.InputSystem;

namespace RikuK0j1ma.RaisingStackChan.DebugTools
{
    /// <summary>Drags DebugDraggable objects with the left mouse button on a plane facing the camera.</summary>
    public sealed class DebugMouseDragger : MonoBehaviour
    {
        private Transform target;
        private Plane dragPlane;
        private Vector3 grabOffset;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null)
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    DebugDraggable draggable = hit.collider.GetComponentInParent<DebugDraggable>();
                    if (draggable != null)
                    {
                        target = draggable.transform;
                        dragPlane = new Plane(-cam.transform.forward, hit.point);
                        grabOffset = target.position - hit.point;
                    }
                }
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                target = null;
            }

            if (target != null && mouse.leftButton.isPressed && dragPlane.Raycast(ray, out float enter))
            {
                target.position = ray.GetPoint(enter) + grabOffset;
            }
        }
    }
}

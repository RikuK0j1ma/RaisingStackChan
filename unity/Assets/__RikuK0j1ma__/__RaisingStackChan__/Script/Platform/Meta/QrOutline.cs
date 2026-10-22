using System;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>
    /// Green rectangle drawn around the QR code being read, so the user can see it is detected.
    /// Sized from the trackable's PlaneRect; a small square is used when no plane is reported.
    /// </summary>
    internal sealed class QrOutline : IDisposable
    {
        private const float FallbackSize = 0.1f;
        private const float LineWidth = 0.004f;
        private static readonly Color Green = new Color(0.1f, 1f, 0.2f, 1f);

        private readonly GameObject root;
        private readonly LineRenderer line;
        private readonly Material material;

        public QrOutline()
        {
            root = new GameObject("QrOutline");
            root.SetActive(false);
            line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.startColor = Green;
            line.endColor = Green;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                material = new Material(shader) { color = Green };
                line.sharedMaterial = material;
            }
        }

        /// <summary>Shows the outline on the trackable, or hides it when null.</summary>
        public void Set(MRUKTrackable trackable)
        {
            if (trackable == null)
            {
                if (root.activeSelf) root.SetActive(false);
                return;
            }

            var half = new Vector2(FallbackSize, FallbackSize) * 0.5f;
            if (trackable.PlaneRect.HasValue) half = trackable.PlaneRect.Value.size * 0.5f;

            var center = trackable.PlaneRect.HasValue ? (Vector3)trackable.PlaneRect.Value.center : Vector3.zero;
            line.SetPosition(0, center + new Vector3(-half.x, -half.y, 0f));
            line.SetPosition(1, center + new Vector3(-half.x, half.y, 0f));
            line.SetPosition(2, center + new Vector3(half.x, half.y, 0f));
            line.SetPosition(3, center + new Vector3(half.x, -half.y, 0f));

            var t = trackable.transform;
            root.transform.SetPositionAndRotation(t.position, t.rotation);
            if (!root.activeSelf) root.SetActive(true);
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}

using UnityEngine;
using UnityEngine.XR;

namespace RikuK0j1ma.RaisingStackChan.Controller
{
    /// <summary>
    /// Controller input through the engine-level XR input API (works with OpenXR / Meta Quest controllers
    /// without any Meta SDK reference). Grip = grab, primary button (A / X) = spawn a sphere.
    /// </summary>
    public sealed class XrControllerInputSource : IHandInputSource
    {
        private const float GripThreshold = 0.6f;

        private readonly XRNode node;

        public XrControllerInputSource(XRNode node)
        {
            this.node = node;
        }

        public bool TryGetState(out HandInputState state)
        {
            state = default;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return false;
            if (!device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked) return false;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out var local)) return false;

            device.TryGetFeatureValue(CommonUsages.grip, out var grip);
            device.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);

            state = new HandInputState(ToWorld(local), grip >= GripThreshold, primary);
            return true;
        }

        /// <summary>
        /// Device poses are in tracking space. Assume the main camera sits directly under the tracking-space
        /// transform (true for XR Origin's Camera Offset and OVRCameraRig's TrackingSpace).
        /// </summary>
        private static Vector3 ToWorld(Vector3 local)
        {
            var cam = Camera.main;
            var space = cam != null ? cam.transform.parent : null;
            return space != null ? space.TransformPoint(local) : local;
        }
    }
}

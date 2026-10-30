using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>
    /// The "StackChan" object in the scene. Its transform follows StackChan's frame (origin = neck pivot, forward =
    /// servo zero direction) whenever the face pose is known, so scene tools and other code can use it as the
    /// StackChan reference. In the editor and in development builds it also draws the axes (X red, Y green, Z blue),
    /// a marker at the neck pivot and a line to what StackChan is looking at, to check the frame against the real device.
    /// </summary>
    public sealed class StackChanRigView : MonoBehaviour
    {
        private const float AxisLength = 0.1f;
        private const float LineWidth = 0.004f;
        private const float PivotMarkerSize = 0.02f;

        private HeadLookUseCase look;
        private GameObject visuals;
        private LineRenderer gazeLine;

        /// <summary>True when the debug drawing is shown. Defaults to editor / RSC_DEVELOPMENT builds only.</summary>
        public bool ShowVisuals { get; set; }
#if UNITY_EDITOR || RSC_DEVELOPMENT
            = true;
#endif

        /// <summary>True while StackChan's frame is known (the object is placed).</summary>
        public bool HasFrame { get; private set; }

        public void Initialize(HeadLookUseCase headLook)
        {
            look = headLook;
            BuildVisuals();
            visuals.SetActive(false);
        }

        private void LateUpdate()
        {
            if (look == null) return;

            HasFrame = look.TryGetFrame(out StackChanFrame frame);
            if (!HasFrame)
            {
                visuals.SetActive(false);
                return;
            }

            transform.SetPositionAndRotation(frame.Pivot, frame.Rotation);
            visuals.SetActive(ShowVisuals);
            if (!ShowVisuals) return;

            bool gaze = look.IsActive;
            gazeLine.enabled = gaze;
            if (gaze)
            {
                gazeLine.SetPosition(0, frame.Pivot);
                gazeLine.SetPosition(1, look.LastLookAt);
            }
        }

        private void BuildVisuals()
        {
            visuals = new GameObject("RigVisuals");
            visuals.transform.SetParent(transform, false);

            // Sprites/Default is in Always Included Shaders (Unlit/Color is stripped from player builds).
            var material = new Material(Shader.Find("Sprites/Default"));

            AddAxis("AxisX", Vector3.right, Color.red, material);
            AddAxis("AxisY", Vector3.up, Color.green, material);
            AddAxis("AxisZ", Vector3.forward, Color.blue, material);

            var pivot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pivot.name = "Pivot";
            Destroy(pivot.GetComponent<Collider>());
            pivot.transform.SetParent(visuals.transform, false);
            pivot.transform.localScale = Vector3.one * PivotMarkerSize;
            pivot.GetComponent<MeshRenderer>().material = new Material(material) { color = Color.white };

            var gazeObject = new GameObject("Gaze");
            gazeObject.transform.SetParent(visuals.transform, false);
            gazeLine = gazeObject.AddComponent<LineRenderer>();
            gazeLine.useWorldSpace = true;
            gazeLine.positionCount = 2;
            gazeLine.widthMultiplier = LineWidth;
            gazeLine.material = material;
            gazeLine.startColor = gazeLine.endColor = Color.yellow;
            gazeLine.enabled = false;
        }

        private void AddAxis(string axisName, Vector3 direction, Color color, Material material)
        {
            var go = new GameObject(axisName);
            go.transform.SetParent(visuals.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, direction * AxisLength);
            line.widthMultiplier = LineWidth;
            line.material = material;
            line.startColor = line.endColor = color;
        }
    }
}

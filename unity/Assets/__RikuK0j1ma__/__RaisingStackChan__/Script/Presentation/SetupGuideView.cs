using System;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>
    /// Guide UI built at runtime: a text panel in front of the user, a ring on StackChan's face once
    /// detected, and a connection indicator. Holds no logic; it renders <see cref="SetupFlow"/>.
    /// Also exposes <see cref="IsLinkConnected"/> for pausing sphere judgement while disconnected.
    /// </summary>
    public sealed class SetupGuideView : MonoBehaviour
    {
        private const float PanelDistance = 1.2f;
        private const float PanelHeightOffset = -0.1f;
        private const float RingRadius = 0.08f;
        private const float AutoAcceptSeconds = 8f;

        private SetupGuideUseCase guide;
        private IStackChanPoseProvider pose;
        private Transform head;

        private TextMesh panelText;
        private Transform panel;
        private LineRenderer ring;
        private Renderer indicator;
        private TextMesh indicatorText;
        private float explanationTime;

        /// <summary>Raised when the user asks to recalibrate (wire to the Quest menu).</summary>
        public event Action RecalibrateRequested;

        public bool IsLinkConnected => guide != null && guide.Flow.IsLinkConnected;

        public void Initialize(SetupGuideUseCase guideUseCase, IStackChanPoseProvider poseProvider, Transform headTransform)
        {
            guide = guideUseCase;
            pose = poseProvider;
            head = headTransform;
            Build();
            guide.Flow.PhaseChanged += OnPhaseChanged;
            guide.Flow.LinkStateChanged += OnLinkStateChanged;
            Refresh();
        }

        /// <summary>Menu entry "再キャリブ".</summary>
        public void RequestRecalibrate() => RecalibrateRequested?.Invoke();

        private void OnDestroy()
        {
            if (guide == null) return;
            guide.Flow.PhaseChanged -= OnPhaseChanged;
            guide.Flow.LinkStateChanged -= OnLinkStateChanged;
        }

        private void Update()
        {
            if (guide == null) return;

            if (guide.Flow.Phase == SetupPhase.PermissionExplanation)
            {
                explanationTime += Time.deltaTime;
                if (explanationTime >= AutoAcceptSeconds) guide.Flow.AcceptPermissionExplanation();
            }

            FollowHead();
            UpdateRing();
        }

        private void OnPhaseChanged(SetupPhase phase, SetupFailure failure) => Refresh();
        private void OnLinkStateChanged(StackChanConnectionState state) => Refresh();

        private void Refresh()
        {
            var flow = guide.Flow;
            var text = SetupGuideTexts.For(flow.Phase, flow.Failure);
            if (flow.Phase == SetupPhase.Failed && flow.Failure == SetupFailure.WifiMismatch
                && !string.IsNullOrEmpty(guide.LastError))
                text += "\n" + guide.LastError;
            panelText.text = text;
            panel.gameObject.SetActive(!string.IsNullOrEmpty(text));

            indicator.material.color = ColorFor(flow.LinkState);
            indicatorText.text = SetupGuideTexts.ForLink(flow.LinkState);
        }

        private void FollowHead()
        {
            if (head == null) return;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            var target = head.position + forward * PanelDistance + Vector3.up * PanelHeightOffset;
            panel.position = Vector3.Lerp(panel.position, target, 5f * Time.deltaTime);
            panel.rotation = Quaternion.LookRotation(panel.position - head.position, Vector3.up);
        }

        private void UpdateRing()
        {
            var show = guide.Flow.Phase >= SetupPhase.Detected && guide.Flow.Phase <= SetupPhase.Connected
                       && pose.TryGetFacePose(out _);
            ring.enabled = show;
            if (!show) return;

            pose.TryGetFacePose(out var face);
            var n = ring.positionCount;
            for (var i = 0; i < n; i++)
            {
                var a = i / (float)(n - 1) * Mathf.PI * 2f;
                var local = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * RingRadius;
                ring.SetPosition(i, face.position + face.rotation * local);
            }
        }

        private static Color ColorFor(StackChanConnectionState state)
        {
            switch (state)
            {
                case StackChanConnectionState.Connected: return new Color(0.2f, 0.85f, 0.4f);
                case StackChanConnectionState.Connecting:
                case StackChanConnectionState.Reconnecting: return new Color(0.95f, 0.75f, 0.2f);
                default: return new Color(0.9f, 0.25f, 0.25f);
            }
        }

        private void Build()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Noto Sans CJK JP", "Yu Gothic", "Meiryo", "Arial" }, 64);

            panel = new GameObject("SetupGuidePanel").transform;
            panel.SetParent(transform, false);
            panelText = CreateText(panel, font, 0.012f, TextAnchor.MiddleCenter);

            // Indicator: small sphere + label, parked at the lower left of the panel.
            var ind = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ind.name = "ConnectionIndicator";
            Destroy(ind.GetComponent<Collider>());
            ind.transform.SetParent(panel, false);
            ind.transform.localPosition = new Vector3(-0.28f, -0.3f, 0f);
            ind.transform.localScale = Vector3.one * 0.03f;
            indicator = ind.GetComponent<Renderer>();
            indicator.material = new Material(Shader.Find("Unlit/Color"));

            var label = new GameObject("IndicatorLabel").transform;
            label.SetParent(panel, false);
            label.localPosition = new Vector3(-0.25f, -0.3f, 0f);
            indicatorText = CreateText(label, font, 0.006f, TextAnchor.MiddleLeft);

            var ringObj = new GameObject("FaceRing");
            ringObj.transform.SetParent(transform, false);
            ring = ringObj.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.006f;
            ring.material = new Material(Shader.Find("Sprites/Default"));
            ring.startColor = ring.endColor = new Color(0.3f, 0.9f, 1f);
            ring.enabled = false;
        }

        private static TextMesh CreateText(Transform parent, Font font, float charSize, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.GetComponent<MeshRenderer>().material = font.material;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.color = Color.white;
            return tm;
        }
    }
}

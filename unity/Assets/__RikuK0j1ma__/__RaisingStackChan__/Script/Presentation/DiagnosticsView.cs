using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Presentation
{
    /// <summary>
    /// Diagnostics panel shown in the headset (world space, follows the head at the lower left):
    /// live status text from a callback, plus the latest tagged logs ("[QR] ...", warnings, errors).
    /// IMGUI is not visible in the headset, so this uses TextMesh like <see cref="SetupGuideView"/>.
    /// </summary>
    public sealed class DiagnosticsView : MonoBehaviour
    {
        private const float PanelDistance = 1.2f;
        private const float RefreshSeconds = 0.5f;
        private const int MaxLogLines = 10;
        private const int MaxLineLength = 110;

        private readonly object logLock = new object();
        private readonly Queue<string> logs = new Queue<string>();
        private readonly StringBuilder sb = new StringBuilder();

        private Func<string> statusProvider;
        private Transform head;
        private Transform panel;
        private TextMesh text;
        private float nextRefreshTime;

        public void Initialize(Func<string> status, Transform headTransform)
        {
            statusProvider = status;
            head = headTransform;
            Build();
            Application.logMessageReceivedThreaded += OnLog;
        }

        private void OnDestroy() => Application.logMessageReceivedThreaded -= OnLog;

        private void Update()
        {
            FollowHead();
            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            // Our own tagged logs ("[QR] ...") and anything that went wrong; skip the SDK's chatter.
            var tagged = message.Length > 0 && message[0] == '[';
            if (!tagged && type == LogType.Log) return;

            var prefix = type == LogType.Log ? "" : type == LogType.Warning ? "WARN " : "ERR ";
            var line = prefix + message.Replace('\n', ' ').Replace('\r', ' ');
            if (line.Length > MaxLineLength) line = line.Substring(0, MaxLineLength) + "...";
            lock (logLock)
            {
                logs.Enqueue(line);
                while (logs.Count > MaxLogLines) logs.Dequeue();
            }
        }

        private void Refresh()
        {
            sb.Length = 0;
            sb.AppendLine("== Diagnostics ==");
            try
            {
                var status = statusProvider?.Invoke();
                if (!string.IsNullOrEmpty(status)) sb.AppendLine(status);
            }
            catch (Exception e)
            {
                sb.AppendLine("status error: " + e.Message);
            }

            sb.AppendLine("-- log --");
            lock (logLock)
            {
                foreach (var line in logs) sb.AppendLine(line);
            }

            text.text = sb.ToString();
        }

        private void FollowHead()
        {
            if (head == null) return;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            var target = head.position + forward * PanelDistance - right * 0.55f + Vector3.down * 0.05f;
            panel.position = Vector3.Lerp(panel.position, target, 5f * Time.deltaTime);
            panel.rotation = Quaternion.LookRotation(panel.position - head.position, Vector3.up);
        }

        private void Build()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Noto Sans CJK JP", "Yu Gothic", "Meiryo", "Arial" }, 64);

            panel = new GameObject("DiagnosticsPanel").transform;
            panel.SetParent(transform, false);

            var go = new GameObject("Text");
            go.transform.SetParent(panel, false);
            text = go.AddComponent<TextMesh>();
            text.font = font;
            text.GetComponent<MeshRenderer>().material = font.material;
            text.fontSize = 64;
            text.characterSize = 0.003f;
            text.anchor = TextAnchor.MiddleLeft;
            text.alignment = TextAlignment.Left;
            text.color = new Color(0.85f, 1f, 0.85f);
        }
    }
}

using System.Text;
using RikuK0j1ma.RaisingStackChan.Presentation;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>
    /// Creates the in-headset diagnostics panel. Status = setup flow + link state + whatever the pose
    /// provider reports through <see cref="IDiagnosticsSource"/> (QR/MRUK details on device).
    /// </summary>
    public sealed class DiagnosticsEntryPoint : IStartable
    {
        private readonly SetupGuideUseCase guide;
        private readonly IStackChanLink link;
        private readonly IStackChanPoseProvider pose;
        private readonly StringBuilder sb = new StringBuilder();

        public DiagnosticsEntryPoint(SetupGuideUseCase guide, IStackChanLink link, IStackChanPoseProvider pose)
        {
            this.guide = guide;
            this.link = link;
            this.pose = pose;
        }

        public void Start()
        {
            var cam = Camera.main;
            var go = new GameObject("Diagnostics");
            go.AddComponent<DiagnosticsView>().Initialize(Describe, cam != null ? cam.transform : null);
        }

        private string Describe()
        {
            sb.Length = 0;
            sb.AppendLine($"Setup: {guide.Flow.Phase} (failure: {guide.Flow.Failure})");
            sb.AppendLine($"Link: {link.State}");
            if (!string.IsNullOrEmpty(link.LastError)) sb.AppendLine("Link error: " + link.LastError);
            sb.AppendLine(pose.TryGetFacePose(out var face)
                ? $"Face pose: {face.position:F2}"
                : "Face pose: none");
            if (pose is IDiagnosticsSource source) sb.Append(source.DescribeStatus());
            return sb.ToString();
        }
    }
}

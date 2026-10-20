using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.DebugTools
{
    /// <summary>Fake link: always "connected"; logs what would be sent and keeps it for the HUD.</summary>
    public sealed class DebugStackChanLink : IStackChanLink
    {
        public bool IsConnected { get; private set; }
        public string LastMessage { get; private set; } = "(nothing sent yet)";

        public UniTask ConnectAsync(CancellationToken ct)
        {
            IsConnected = true;
            Record("connected (debug link)");
            return UniTask.CompletedTask;
        }

        public UniTask SendEmotionAsync(StackChanEmotion emotion, int durationMs, CancellationToken ct)
        {
            Record($"emotion={emotion} durationMs={durationMs}");
            return UniTask.CompletedTask;
        }

        public UniTask SendShowQrAsync(CancellationToken ct)
        {
            Record("show_qr");
            return UniTask.CompletedTask;
        }

        private void Record(string message)
        {
            LastMessage = $"[{Time.time:F1}s] {message}";
            Debug.Log($"[DebugStackChanLink] {message}");
        }
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.Infrastructure
{
    /// <summary>No-op link used when no StackChan is available.</summary>
    public sealed class NullStackChanLink : IStackChanLink
    {
        public bool IsConnected => false;
        public StackChanConnectionState State => StackChanConnectionState.Disconnected;
        public string LastError => null;

#pragma warning disable CS0067
        public event Action<StackChanConnectionState> StateChanged;
#pragma warning restore CS0067

        public bool TrySetEndpointFromQr(string payload) => false;

        public UniTask ConnectAsync(CancellationToken ct) => UniTask.CompletedTask;

        public UniTask SendEmotionAsync(StackChanEmotion emotion, int durationMs, CancellationToken ct)
        {
            Debug.Log($"[NullStackChanLink] emotion={emotion} durationMs={durationMs}");
            return UniTask.CompletedTask;
        }

        public UniTask SendShowQrAsync(CancellationToken ct) => UniTask.CompletedTask;

        public UniTask SendLookAsync(float yawDeg, float pitchDeg, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask SendHideQrAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}

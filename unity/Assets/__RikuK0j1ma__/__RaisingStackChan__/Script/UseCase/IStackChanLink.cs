using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Connection to StackChan (WebSocket in production).</summary>
    public interface IStackChanLink
    {
        bool IsConnected { get; }
        StackChanConnectionState State { get; }

        /// <summary>Raised on every state change, on the Unity main thread.</summary>
        event Action<StackChanConnectionState> StateChanged;

        /// <summary>Last connection error for display (e.g. Wi-Fi blocks device-to-device traffic). Null when none.</summary>
        string LastError { get; }

        /// <summary>Sets the endpoint from a QR payload. Returns false when the payload cannot be parsed.</summary>
        bool TrySetEndpointFromQr(string payload);

        /// <summary>
        /// Connects and keeps the link alive (auto reconnect with backoff).
        /// Completes when first connected, or throws OperationCanceledException when ct is cancelled.
        /// </summary>
        UniTask ConnectAsync(CancellationToken ct);

        UniTask SendEmotionAsync(StackChanEmotion emotion, int durationMs, CancellationToken ct);
        UniTask SendShowQrAsync(CancellationToken ct);

        /// <summary>
        /// Tells StackChan which way to turn its neck. Angles are degrees seen from the StackChan face:
        /// yaw + is right, pitch + is up, (0, 0) is straight ahead. Throws when not connected.
        /// </summary>
        UniTask SendLookAsync(float yawDeg, float pitchDeg, CancellationToken ct);

        /// <summary>Asks StackChan to hide the QR and go back to the face (hideQr). The connection stays up.</summary>
        UniTask SendHideQrAsync(CancellationToken ct);
    }
}

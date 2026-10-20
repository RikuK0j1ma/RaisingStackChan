using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.Entity;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>Connection to StackChan (WebSocket in production).</summary>
    public interface IStackChanLink
    {
        bool IsConnected { get; }
        UniTask ConnectAsync(CancellationToken ct);
        UniTask SendEmotionAsync(StackChanEmotion emotion, int durationMs, CancellationToken ct);
        UniTask SendShowQrAsync(CancellationToken ct);
    }
}
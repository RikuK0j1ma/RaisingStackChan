using System.Threading;
using Cysharp.Threading.Tasks;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Entry point of the MR scene. Resolving it proves the container is wired.</summary>
    public sealed class MrSceneEntryPoint : IAsyncStartable
    {
        private readonly IStackChanLink link;

        public MrSceneEntryPoint(IStackChanLink link)
        {
            this.link = link;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            await link.ConnectAsync(ct);
            Debug.Log($"[MR] DI ready. link={link.GetType().Name} connected={link.IsConnected}");
        }
    }
}
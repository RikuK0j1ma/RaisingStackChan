using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Lives in the Boot scene. Loads the MR scene once the root scope exists.</summary>
    public sealed class BootEntryPoint : MonoBehaviour
    {
        public const string MrSceneName = "MR";

        private async UniTaskVoid Start()
        {
            // Wait one frame so the Root LifetimeScope finishes building.
            await UniTask.Yield();
            CancellationToken ct = this.GetCancellationTokenOnDestroy();
            await SceneManager.LoadSceneAsync(MrSceneName).ToUniTask(cancellationToken: ct);
        }
    }
}
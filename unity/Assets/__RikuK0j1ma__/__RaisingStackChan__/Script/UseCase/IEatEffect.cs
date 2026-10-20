using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RikuK0j1ma.RaisingStackChan.UseCase
{
    /// <summary>
    /// Visual effect of a sphere being sucked into the mouth. The real sphere is already despawned
    /// when this is called, so the implementation draws its own transient visual.
    /// </summary>
    public interface IEatEffect
    {
        UniTask PlayAsync(Vector3 from, Vector3 mouth, CancellationToken ct);
    }
}

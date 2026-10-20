using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Drives FeedSphereUseCase every frame.</summary>
    public sealed class FeedSphereEntryPoint : ITickable
    {
        private readonly FeedSphereUseCase useCase;

        public FeedSphereEntryPoint(FeedSphereUseCase useCase)
        {
            this.useCase = useCase;
        }

        public void Tick() => useCase.Tick(Time.deltaTime);
    }
}

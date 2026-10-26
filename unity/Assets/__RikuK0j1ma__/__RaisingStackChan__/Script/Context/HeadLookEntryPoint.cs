using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Drives HeadLookUseCase every frame.</summary>
    public sealed class HeadLookEntryPoint : ITickable
    {
        private readonly HeadLookUseCase useCase;

        public HeadLookEntryPoint(HeadLookUseCase useCase)
        {
            this.useCase = useCase;
        }

        public void Tick() => useCase.Tick(Time.deltaTime);
    }
}

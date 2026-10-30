using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Drives ExpressionUseCase every frame, after HeadLookEntryPoint (registered later).</summary>
    public sealed class ExpressionEntryPoint : ITickable
    {
        private readonly ExpressionUseCase useCase;

        public ExpressionEntryPoint(ExpressionUseCase useCase)
        {
            this.useCase = useCase;
        }

        public void Tick() => useCase.Tick(Time.deltaTime);
    }
}

using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Drives HungrySoundUseCase every frame, after HeadLookEntryPoint (registered later).</summary>
    public sealed class HungrySoundEntryPoint : ITickable
    {
        private readonly HungrySoundUseCase useCase;
        private readonly HeadLookUseCase look;

        public HungrySoundEntryPoint(HungrySoundUseCase useCase, HeadLookUseCase look)
        {
            this.useCase = useCase;
            this.look = look;
        }

        public void Tick() => useCase.Tick(look.IsActive && look.IsLookingAtFood, Time.deltaTime);
    }
}

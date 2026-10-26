using RikuK0j1ma.RaisingStackChan.Presentation;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>Creates the "StackChan" object in the scene (see StackChanRigView).</summary>
    public sealed class StackChanRigEntryPoint : IStartable
    {
        private readonly HeadLookUseCase look;

        public StackChanRigEntryPoint(HeadLookUseCase look)
        {
            this.look = look;
        }

        public void Start()
        {
            var go = new GameObject("StackChan");
            go.AddComponent<StackChanRigView>().Initialize(look);
        }
    }
}

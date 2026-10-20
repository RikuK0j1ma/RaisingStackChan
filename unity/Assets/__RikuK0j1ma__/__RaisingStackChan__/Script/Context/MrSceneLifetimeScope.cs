using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.Infrastructure;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>
    /// Scope of the MR scene (MrSceneLTS). Registers Null implementations so the
    /// scene runs without a headset or StackChan. Swap them for real ones
    /// (Infrastructure / Platform.Meta) in later issues.
    /// </summary>
    public sealed class MrSceneLifetimeScope : LifetimeScope
    {
        [SerializeField] private EatConfig eatConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(eatConfig);

            builder.Register<NullStackChanLink>(Lifetime.Singleton).As<IStackChanLink>();
            builder.Register<NullStackChanPoseProvider>(Lifetime.Singleton).As<IStackChanPoseProvider>();
            builder.Register<NullSphereSpawner>(Lifetime.Singleton).As<ISphereSpawner>();

            builder.RegisterEntryPoint<MrSceneEntryPoint>();
        }
    }
}
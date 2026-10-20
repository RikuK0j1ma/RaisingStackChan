using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.Infrastructure;
using RikuK0j1ma.RaisingStackChan.Presentation;
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

            // TODO(#8): switch to WebSocketStackChanLink (Infrastructure) once the QR flow (calibration
            // use case) calls TrySetEndpointFromQr + ConnectAsync. Until then the endpoint is unknown,
            // so the real link would only retry mDNS/last-endpoint; keep Null to run without a StackChan.
            builder.Register<NullStackChanLink>(Lifetime.Singleton).As<IStackChanLink>();
            builder.Register<NullStackChanPoseProvider>(Lifetime.Singleton).As<IStackChanPoseProvider>();
            builder.Register<NullSphereSpawner>(Lifetime.Singleton).As<ISphereSpawner>();

            builder.Register<SuckInEatEffect>(Lifetime.Singleton).As<IEatEffect>();
            builder.Register<NullEatSound>(Lifetime.Singleton).As<IEatSound>();
            builder.Register<FeedSphereUseCase>(Lifetime.Singleton); // spawner side must call Track(id)
            builder.RegisterEntryPoint<FeedSphereEntryPoint>();

            builder.RegisterEntryPoint<MrSceneEntryPoint>();
        }
    }
}
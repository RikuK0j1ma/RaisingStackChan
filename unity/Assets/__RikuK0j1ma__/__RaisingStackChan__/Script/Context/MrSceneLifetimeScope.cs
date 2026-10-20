using RikuK0j1ma.RaisingStackChan.Controller;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.Infrastructure;
using RikuK0j1ma.RaisingStackChan.Presentation;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using UnityEngine.XR;
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

        [Tooltip("Optional. Defaults are used when unassigned.")]
        [SerializeField] private SphereConfig sphereConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(eatConfig);

            // TODO(#8): switch to WebSocketStackChanLink (Infrastructure) once the QR flow (calibration
            // use case) calls TrySetEndpointFromQr + ConnectAsync. Until then the endpoint is unknown,
            // so the real link would only retry mDNS/last-endpoint; keep Null to run without a StackChan.
            builder.Register<NullStackChanLink>(Lifetime.Singleton).As<IStackChanLink>();
            builder.Register<NullStackChanPoseProvider>(Lifetime.Singleton).As<IStackChanPoseProvider>();

            // Real sphere spawner (Presentation) + controller input. Safe without a headset: the XR
            // devices are simply untracked, so no sphere appears. NullSphereSpawner stays for tests.
            builder.RegisterInstance(sphereConfig != null ? sphereConfig : ScriptableObject.CreateInstance<SphereConfig>());
            builder.Register<SphereSpawner>(Lifetime.Singleton).As<ISphereSpawner>().As<ISphereInteraction>();
            builder.RegisterInstance<IHandInputSource>(new XrControllerInputSource(XRNode.LeftHand));
            builder.RegisterInstance<IHandInputSource>(new XrControllerInputSource(XRNode.RightHand));
            builder.RegisterEntryPoint<SphereGrabController>();

            builder.RegisterEntryPoint<MrSceneEntryPoint>();
        }
    }
}
using System.Collections.Generic;
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

            // Real WebSocket link on device only. CalibrationEntryPoint feeds it the QR endpoint and connects.
#if UNITY_ANDROID && !UNITY_EDITOR
            // VContainer picks the constructor with the most parameters, so the store must be registered.
            builder.Register<PlayerPrefsEndpointStore>(Lifetime.Singleton);
            builder.Register<WebSocketStackChanLink>(Lifetime.Singleton).As<IStackChanLink>();
#else
            builder.Register<NullStackChanLink>(Lifetime.Singleton).As<IStackChanLink>();
#endif
#if UNITY_EDITOR || UNITY_ANDROID
            // QR-based face pose (MRUK). Falls back to Null when MRUK is not in the scene.
            if (!RikuK0j1ma.RaisingStackChan.Platform.Meta.MetaPlatformInstaller.TryRegisterQrPoseProvider(builder))
#endif
                builder.Register<NullStackChanPoseProvider>(Lifetime.Singleton).As<IStackChanPoseProvider>();

            // Real sphere spawner (Presentation) + controller input. Safe without a headset: the XR
            // devices are simply untracked, so no sphere appears. NullSphereSpawner stays for tests.
            builder.RegisterInstance(sphereConfig != null ? sphereConfig : ScriptableObject.CreateInstance<SphereConfig>());
            builder.Register<SphereSpawner>(Lifetime.Singleton).As<ISphereSpawner>().As<ISphereInteraction>();
            // One list instance: VContainer rejects two registrations with the same implementation type.
            builder.RegisterInstance<IReadOnlyList<IHandInputSource>>(new IHandInputSource[]
            {
                new XrControllerInputSource(XRNode.LeftHand),
                new XrControllerInputSource(XRNode.RightHand),
            });
            builder.RegisterEntryPoint<SphereGrabController>();

            builder.Register<SuckInEatEffect>(Lifetime.Singleton).As<IEatEffect>(); // takes SphereConfig
            builder.Register<NullEatSound>(Lifetime.Singleton).As<IEatSound>();
            builder.Register<FeedSphereUseCase>(Lifetime.Singleton); // subscribes to ISphereSpawner.Spawned itself
            builder.RegisterEntryPoint<FeedSphereEntryPoint>();

            // Face anchor persistence (#10). Spatial Anchors only work on device; elsewhere the QR flow is used.
            builder.Register<PlayerPrefsFaceAnchorIdStore>(Lifetime.Singleton).As<IFaceAnchorIdStore>();
#if UNITY_ANDROID && !UNITY_EDITOR
            builder.Register<RikuK0j1ma.RaisingStackChan.Platform.Meta.SpatialAnchorFaceAnchorStore>(Lifetime.Singleton).As<IFaceAnchorStore>();
#else
            builder.Register<NullFaceAnchorStore>(Lifetime.Singleton).As<IFaceAnchorStore>();
#endif
            builder.Register<NullCalibrationGuide>(Lifetime.Singleton).As<ICalibrationGuide>(); // the setup guide UI drives itself from SetupGuideUseCase
            builder.Register<FaceCalibrationUseCase>(Lifetime.Singleton);

            builder.Register<SetupGuideUseCase>(Lifetime.Singleton);
            builder.Register<RecalibrationService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SetupGuideEntryPoint>();
            builder.RegisterEntryPoint<DiagnosticsEntryPoint>();

            builder.RegisterEntryPoint<MrSceneEntryPoint>();

            // Development-only overrides (DebugTools) hook in here.
            MrSceneExtension.Invoke(builder);
        }
    }
}
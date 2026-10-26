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

            builder.RegisterInstance(sphereConfig != null ? sphereConfig : ScriptableObject.CreateInstance<SphereConfig>());
            // Meta SDK spawner: A / X or middle pinch spawns, the Interaction SDK grabs and throws.
#if UNITY_EDITOR || UNITY_ANDROID
            RikuK0j1ma.RaisingStackChan.Platform.Meta.MetaPlatformInstaller.RegisterSphereSpawner(builder);
#else
            builder.Register<NullSphereSpawner>(Lifetime.Singleton).As<ISphereSpawner>();
#endif

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

            // Y button: QR <-> face (#27).
#if UNITY_EDITOR || UNITY_ANDROID
            RikuK0j1ma.RaisingStackChan.Platform.Meta.MetaPlatformInstaller.RegisterQrToggleInput(builder);
#else
            builder.Register<NullQrToggleInput>(Lifetime.Singleton).As<IQrToggleInput>();
#endif
            builder.RegisterEntryPoint<QrToggleEntryPoint>();
            builder.RegisterEntryPoint<DiagnosticsEntryPoint>();

            builder.RegisterEntryPoint<MrSceneEntryPoint>();

            // StackChan turns its neck toward the headset while connected (#26).
#if UNITY_EDITOR || UNITY_ANDROID
            if (!RikuK0j1ma.RaisingStackChan.Platform.Meta.MetaPlatformInstaller.TryRegisterHeadPoseProvider(builder))
#endif
                builder.Register<NullHeadPoseProvider>(Lifetime.Singleton).As<IHeadPoseProvider>();
            builder.Register<HeadLookUseCase>(Lifetime.Singleton);
            builder.RegisterEntryPoint<HeadLookEntryPoint>();

            // Development-only overrides (DebugTools) hook in here.
            MrSceneExtension.Invoke(builder);
        }
    }
}
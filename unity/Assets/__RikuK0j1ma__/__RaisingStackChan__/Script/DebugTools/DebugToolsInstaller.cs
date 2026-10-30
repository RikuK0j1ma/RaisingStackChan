using RikuK0j1ma.RaisingStackChan.Context;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.UseCase;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.DebugTools
{
    /// <summary>
    /// Replaces the Null implementations with debug ones. Subscribes itself to
    /// MrSceneExtension, so Context needs no reference to this assembly; when
    /// RSC_DEVELOPMENT is off this assembly is not built and Null stays.
    /// </summary>
    public static class DebugToolsInstaller
    {
        private static readonly Vector3 FaceStartPosition = new Vector3(0f, 1.2f, 0.8f);
        private static readonly Vector3 MouthLocalOffset = new Vector3(0f, -0.04f, 0.05f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // -= first: safe with domain reload disabled.
            MrSceneExtension.Configuring -= Install;
            MrSceneExtension.Configuring += Install;
        }

        private static void Install(IContainerBuilder builder)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On the headset the real implementations (Meta spawner with A / X input, QR pose, WebSocket link) must stay.
            return;
#else
            // Editor with a headset (Link): keep the real implementations too. Overrides are for headset-less testing.
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;
            InstallEditorOverrides(builder);
#endif
        }

        private static void InstallEditorOverrides(IContainerBuilder builder)
        {
            // Face object lives in the scene so it can be moved in the editor.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "DebugStackChanFace";
            face.transform.position = FaceStartPosition;
            face.transform.localScale = new Vector3(0.1f, 0.08f, 0.02f);
            face.AddComponent<DebugDraggable>();

            builder.RegisterInstance(new DebugStackChanPoseProvider(face.transform, MouthLocalOffset))
                .As<IStackChanPoseProvider>();
            builder.Register<DebugStackChanLink>(Lifetime.Singleton).AsSelf().As<IStackChanLink>();
            builder.Register<DebugSphereSpawner>(Lifetime.Singleton).AsSelf().As<ISphereSpawner>();

            builder.RegisterEntryPoint<DebugToolsEntryPoint>();
        }

        internal sealed class DebugToolsEntryPoint : IStartable
        {
            private readonly DebugStackChanLink link;
            private readonly DebugSphereSpawner spawner;
            private readonly IStackChanPoseProvider pose;
            private readonly EatConfig config;

            public DebugToolsEntryPoint(DebugStackChanLink link, DebugSphereSpawner spawner,
                IStackChanPoseProvider pose, EatConfig config)
            {
                this.link = link;
                this.spawner = spawner;
                this.pose = pose;
                this.config = config;
            }

            public void Start()
            {
                var go = new GameObject("DebugTools");
                go.AddComponent<DebugMouseDragger>();
                go.AddComponent<DebugHud>().Initialize(link, spawner, pose, config);
            }
        }
    }
}

using System;
using Meta.XR.MRUtilityKit;
using RikuK0j1ma.RaisingStackChan.UseCase;
using VContainer;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Platform.Meta
{
    /// <summary>Registers Meta-specific implementations. Called from Context.</summary>
    public static class MetaPlatformInstaller
    {
        /// <summary>
        /// Registers the MRUK QR pose provider as IStackChanPoseProvider when MRUK is in the scene.
        /// Returns false (and registers nothing) otherwise, so the caller keeps its Null fallback.
        /// </summary>
        public static bool TryRegisterQrPoseProvider(IContainerBuilder builder)
        {
            if (UnityEngine.Object.FindFirstObjectByType<MRUK>() == null) return false;

            builder.Register<MrukQrStackChanPoseProvider>(Lifetime.Singleton)
                .As<IStackChanPoseProvider>()
                .As<IInitializable>()
                .As<ITickable>()
                .As<IDisposable>()
                .AsSelf();
            return true;
        }

        /// <summary>Registers the Meta SDK sphere spawner (spawn input + Interaction SDK grabbing).</summary>
        public static void RegisterSphereSpawner(IContainerBuilder builder)
        {
            builder.Register<MetaSphereSpawner>(Lifetime.Singleton)
                .As<ISphereSpawner>()
                .As<ITickable>();
        }
    }
}

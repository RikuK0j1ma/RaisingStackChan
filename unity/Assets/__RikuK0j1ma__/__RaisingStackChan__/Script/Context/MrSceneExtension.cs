using System;
using VContainer;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>
    /// Extension point of MrSceneLifetimeScope. Invoked at the end of Configure, so
    /// registrations added here override the defaults (VContainer: last one wins).
    /// Context never references the subscribers; development-only assemblies
    /// (DebugTools) subscribe themselves via RuntimeInitializeOnLoadMethod.
    /// </summary>
    public static class MrSceneExtension
    {
        public static event Action<IContainerBuilder> Configuring;

        internal static void Invoke(IContainerBuilder builder) => Configuring?.Invoke(builder);
    }
}

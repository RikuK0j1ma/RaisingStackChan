using VContainer;
using VContainer.Unity;

namespace RikuK0j1ma.RaisingStackChan.Context
{
    /// <summary>
    /// Root scope (RootLTS prefab). Set as Root Lifetime Scope in VContainerSettings.
    /// Put app-wide singletons here; scene-level wiring lives in MrSceneLifetimeScope.
    /// </summary>
    public sealed class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
        }
    }
}
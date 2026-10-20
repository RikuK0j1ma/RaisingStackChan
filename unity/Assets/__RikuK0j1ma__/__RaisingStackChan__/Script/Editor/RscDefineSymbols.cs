using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace RikuK0j1ma.RaisingStackChan.Editor
{
    /// <summary>
    /// Reads / writes scripting define symbols and exposes the RSC_DEVELOPMENT toggle.
    /// Menu: Tools/RaisingStackChan/Development Build (RSC_DEVELOPMENT)
    /// </summary>
    public static class RscDefineSymbols
    {
        public const string Development = "RSC_DEVELOPMENT";
        private const string MenuPath = "Tools/RaisingStackChan/Development Build (RSC_DEVELOPMENT)";

        public static readonly NamedBuildTarget[] Targets = { NamedBuildTarget.Standalone, NamedBuildTarget.Android };

        public static bool Has(NamedBuildTarget target, string symbol)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out var symbols);
            return symbols.Contains(symbol);
        }

        public static void Set(NamedBuildTarget target, string symbol, bool enabled)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out var symbols);
            var next = symbols.Where(s => s != symbol).ToList();
            if (enabled)
            {
                next.Add(symbol);
            }

            PlayerSettings.SetScriptingDefineSymbols(target, next.ToArray());
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            var enable = !Has(NamedBuildTarget.Android, Development);
            foreach (var target in Targets)
            {
                Set(target, Development, enable);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Has(NamedBuildTarget.Android, Development));
            return true;
        }
    }
}

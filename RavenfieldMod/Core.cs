using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(URM.Core), "URM", "1.1.0", "gaming", null)]
[assembly: MelonGame(null, null)]

namespace URM
{
    public class Core : MelonMod
    {
        public static Core Instance;
        public override void OnInitializeMelon()
        {
            Instance = this;
            // Register configuration settings
            TerritoryControl.RegisterConfig();
            MinimapUiPatch.RegisterConfig();
            
            foreach (MethodBase method in HarmonyInstance.GetPatchedMethods())
            {
                LoggerInstance.Msg($"successfully patched {method.ToString()}");
            }
            LoggerInstance.Msg("URM initalized!");

        }
    }
}
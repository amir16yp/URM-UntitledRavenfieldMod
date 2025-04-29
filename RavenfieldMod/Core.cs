using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(BasicMinimapMod.Core), "Basic Minimap Territory Control", "1.1.0", "gaming", null)]
[assembly: MelonGame(null, null)]

namespace BasicMinimapMod
{
    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            foreach (MethodBase method in HarmonyInstance.GetPatchedMethods())
            {
                LoggerInstance.Msg($"successfully patched {method.ToString()}");
            }
            LoggerInstance.Msg("URM initalized!");

        }
    }
}
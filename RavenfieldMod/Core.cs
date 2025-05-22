using System.Reflection;
using MelonLoader;
using UnityEngine;
using System.IO;

[assembly: MelonInfo(typeof(URM.Core), "URM", "1.1.0", "gaming", null)]
[assembly: MelonGame(null, null)]

namespace URM
{
    public class Core : MelonMod
    {
        public static Core Instance;
        
        // Config categories
        private static MelonPreferences_Category minimapCategory;
        
        // Config entries
        private static MelonPreferences_Entry<bool> enableMinimapCloneEntry;
        private static MelonPreferences_Entry<float> minimapCloneScaleEntry;
        
        // Properties to access config values
        public bool EnableMinimapClone => enableMinimapCloneEntry.Value;
        public float MinimapCloneScale => minimapCloneScaleEntry.Value;
        
        public override void OnInitializeMelon()
        {
            Instance = this;
            
            // Register configuration settings
            RegisterConfig();
            
            // Register TerritoryControl settings
            TerritoryControl.RegisterConfig();
            
            // Register Minimap patch
            MinimapUiPatch.RegisterConfig();

            CapturePointPatch.RegisterConfig();
            
            foreach (MethodBase method in HarmonyInstance.GetPatchedMethods())
            {
                LoggerInstance.Msg($"successfully patched {method.ToString()}");
            }
            LoggerInstance.Msg("URM initalized!");
        }
        
        private void RegisterConfig()
        {
            // Create category for minimap settings
            minimapCategory = MelonPreferences.CreateCategory("Minimap");
            
            // Register minimap settings
            enableMinimapCloneEntry = minimapCategory.CreateEntry("EnableAlwaysOnMinimap", false, "Enable Always-On Minimap",
                "Shows a persistent minimap on the screen");
            minimapCloneScaleEntry = minimapCategory.CreateEntry("MinimapScale", 0.7f, "Minimap Scale",
                "Scale of the always-on minimap (0.1-1.0)");
            
            // Load and save category
            minimapCategory.LoadFromFile();
            minimapCategory.SaveToFile();
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            // if (AlwaysVisibleMinimap.Instance != null && EnableMinimapClone)
            // {
            //     AlwaysVisibleMinimap.Instance.Update();
            // }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (buildIndex > 0) // Skip menu/title scenes
            {
                LoggerInstance.Msg($"Scene loaded: {sceneName}");
                
                // Initialize TerritoryControl
                TerritoryControl.Initialize();
                
                // Initialize AlwaysVisibleMinimap when a game scene is loaded
                // if (EnableMinimapClone && MinimapCamera.MINIMAP_RENDER_TEXTURE != null)
                // {
                //     AlwaysVisibleMinimap.Initialize(MinimapCloneScale);
                // }
                
                LoggerInstance.Msg("Scene initialization complete");
            }
        }
    }
}
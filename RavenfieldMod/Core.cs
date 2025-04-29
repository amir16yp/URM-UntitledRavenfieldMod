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
        
        
        // Minimap clone settings
        public bool EnableMinimapClone = true;
        public float MinimapCloneScale = 0.7f;
        public bool ShowTerritories = true;
        
        // Config categories
        
        public override void OnInitializeMelon()
        {
            Instance = this;
            
            // Register configuration settings
            
            // Register TerritoryControl settings
            TerritoryControl.RegisterConfig();
            
            // Register Minimap patch
            MinimapUiPatch.RegisterConfig();
            
            foreach (MethodBase method in HarmonyInstance.GetPatchedMethods())
            {
                LoggerInstance.Msg($"successfully patched {method.ToString()}");
            }
            LoggerInstance.Msg("URM initalized!");
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (AlwaysVisibleMinimap.Instance != null)
            {
                AlwaysVisibleMinimap.Instance.Update();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (buildIndex > 0) // Skip menu/title scenes
            {
                LoggerInstance.Msg($"Scene loaded: {sceneName}");
                
                // Initialize TerritoryControl
                TerritoryControl.Initialize();
                
                // Initialize AlwaysVisibleMinimap when a game scene is loaded
                if (EnableMinimapClone && MinimapCamera.MINIMAP_RENDER_TEXTURE != null)
                {
                    AlwaysVisibleMinimap.Initialize(MinimapCloneScale);
                    
                }
                
                LoggerInstance.Msg("Scene initialization complete");
            }
        }
    }
}
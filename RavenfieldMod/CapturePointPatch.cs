using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using MelonLoader;
using System.Reflection;

namespace URM
{
    [HarmonyPatch(typeof(CapturePoint))]
    public static class CapturePointPatch
    {
        // Configuration
        private static MelonPreferences_Category capturePointCategory;
        private static MelonPreferences_Entry<float> captureInfluenceRadiusEntry;
        private static MelonPreferences_Entry<float> captureInfluenceStrengthEntry;
        
        // Properties to access config values
        public static float captureInfluenceRadius => captureInfluenceRadiusEntry.Value;
        public static float captureInfluenceStrength => captureInfluenceStrengthEntry.Value;
        
        // Register configuration settings
        public static void RegisterConfig()
        {
            // Create category for capture point influence settings
            capturePointCategory = MelonPreferences.CreateCategory("CapturePoints");
            
            // Register capture point territory settings
            captureInfluenceRadiusEntry = capturePointCategory.CreateEntry("TerritoryRadius", 0.15f, "Capture Point Territory Radius", 
                "Radius of territory control when a capture point is captured (0-1 in map coordinates)");
            captureInfluenceStrengthEntry = capturePointCategory.CreateEntry("TerritoryStrength", 0.95f, "Capture Point Territory Strength",
                "Strength of territory control when a capture point is captured (higher = more solid control)");

            // Load and save category
            capturePointCategory.LoadFromFile();
            capturePointCategory.SaveToFile();
            
            MelonLogger.Msg("CapturePointPatch configuration loaded");
        }
        
        [HarmonyPatch("SetOwner")]
        [HarmonyPostfix]
        public static void SetOwnerPostfix(CapturePoint __instance, int team, bool initialOwner)
        {
            // Make sure TerritoryControl is initialized
            if (TerritoryControl.InfluenceMap == null)
            {
                TerritoryControl.Initialize();
            }
            
            // Register configuration if not already done
            if (capturePointCategory == null)
            {
                RegisterConfig();
            }
            
            // Skip if this is initial ownership setting or if the capture point is neutral
            if (initialOwner || team == -1)
            {
                return;
            }
            
            // Create territory around the capture point
            CreateCapturePointTerritory(__instance, team);
            
            MelonLogger.Msg($"Capture point {__instance.name} captured by team {team}. Adding territory control.");
        }
        
        private static void CreateCapturePointTerritory(CapturePoint capturePoint, int team)
        {
            // Get the capture point's world position
            Vector3 worldPosition = capturePoint.transform.position;
            
            // Convert world position to minimap coordinates using the same method as MinimapUiPatch
            Matrix4x4 minimapMatrix = GetMinimapMatrix();
            Vector3 screenPos = minimapMatrix.MultiplyPoint(worldPosition);
            screenPos = ClampViewportPosition(screenPos);
            
            Vector2 mapPos = new Vector2(screenPos.x, screenPos.y);
            
            // Calculate territory radius based on capture range
            float captureRange = capturePoint.captureRange / 1024.0f; // Convert to normalized coordinates
            float radius = Mathf.Max(captureInfluenceRadius, captureRange * 1.5f); // Use larger of config value or scaled capture range
            
            // Create territory points in a circle around the capture point
            CreateCircularTerritory(mapPos, team, radius);
            
            MelonLogger.Msg($"Created territory at position {mapPos.x}, {mapPos.y} with radius {radius}");
        }
        
        private static void CreateCircularTerritory(Vector2 center, int team, float radius)
        {
            // Create territory points directly (no influence)
            List<Vector2> territoryPoints = new List<Vector2>();
            
            // Generate points in a circle
            int numPoints = 16; // Number of points to create
            for (int i = 0; i < numPoints; i++)
            {
                float angle = (i * 2 * Mathf.PI) / numPoints;
                float x = center.x + radius * Mathf.Cos(angle);
                float y = center.y + radius * Mathf.Sin(angle);
                
                // Clamp to valid map coordinates
                x = Mathf.Clamp01(x);
                y = Mathf.Clamp01(y);
                
                territoryPoints.Add(new Vector2(x, y));
            }
            
            // Set the territory points for the team
            if (!TerritoryControl.TeamTerritories.ContainsKey(team))
            {
                TerritoryControl.TeamTerritories[team] = new List<Vector2>();
            }
            
            // Add to existing territory (don't replace it)
            foreach (Vector2 point in territoryPoints)
            {
                TerritoryControl.TeamTerritories[team].Add(point);
            }
            
            // Also set grid cells in this region to be controlled by the team
            int gridResolution = TerritoryControl.gridResolution;
            int minX = Mathf.Max(0, Mathf.FloorToInt((center.x - radius) * gridResolution));
            int maxX = Mathf.Min(gridResolution - 1, Mathf.CeilToInt((center.x + radius) * gridResolution));
            int minY = Mathf.Max(0, Mathf.FloorToInt((center.y - radius) * gridResolution));
            int maxY = Mathf.Min(gridResolution - 1, Mathf.CeilToInt((center.y + radius) * gridResolution));
            
            // Set control map cells within the circle
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    // Calculate cell center in map coordinates
                    float cellX = (x + 0.5f) / gridResolution;
                    float cellY = (y + 0.5f) / gridResolution;
                    
                    // Calculate distance from center to cell
                    float distance = Vector2.Distance(center, new Vector2(cellX, cellY));
                    
                    if (distance <= radius)
                    {
                        float controlValue = captureInfluenceStrength * (1.0f - distance / radius);
                        
                        // Set direct control in the ControlMap
                        if (team == 0) // Blue team
                        {
                            TerritoryControl.ControlMap[x, y] = 0.0f; // Full blue control
                            TerritoryControl.InfluenceMap[x, y] = -controlValue; // Negative for blue
                        }
                        else // Red team
                        {
                            TerritoryControl.ControlMap[x, y] = 1.0f; // Full red control
                            TerritoryControl.InfluenceMap[x, y] = controlValue; // Positive for red
                        }
                    }
                }
            }
        }
        
        // Use the same methods as MinimapUiPatch to get world-to-minimap transformations
        private static Matrix4x4 GetMinimapMatrix()
        {
            try
            {
                // Get matrix through reflection, similar to what MinimapUiPatch does
                Matrix4x4 viewportMatrix = (Matrix4x4)typeof(MinimapUi)
                    .GetField("VIEWPORT_MATRIX", BindingFlags.NonPublic | BindingFlags.Static)
                    .GetValue(null);
                    
                return viewportMatrix * MinimapCamera.instance.camera.projectionMatrix * 
                       MinimapCamera.instance.camera.worldToCameraMatrix;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error getting minimap matrix: {ex.Message}");
                // Return identity matrix as fallback
                return Matrix4x4.identity;
            }
        }
        
        private static Vector3 ClampViewportPosition(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, 0.01f, 0.99f),
                Mathf.Clamp(position.y, 0.01f, 0.99f),
                position.z
            );
        }
    }
} 
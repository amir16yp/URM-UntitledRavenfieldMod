using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using MelonLoader;

namespace URM
{
    [HarmonyPatch(typeof(MinimapUi))]
    public static class MinimapUiPatch
    {
        // Material for drawing units
        private static Material unitMaterial;
        
        // Material for drawing territory
        private static Material territoryMaterial;
        
        // Render texture for unit visualization
        public static RenderTexture unitRenderTexture;
        
        // Render texture for territory visualization
        public static RenderTexture territoryRenderTexture;
        
        // Image to display units on the minimap
        private static UnityEngine.UI.RawImage unitOverlayImage;
        
        // Image to display territories on the minimap
        private static UnityEngine.UI.RawImage territoryOverlayImage;
        
        // Config category
        private static MelonPreferences_Category minimapCategory;
        
        // Config entries
        private static MelonPreferences_Entry<float> unitUpdateIntervalEntry;
        private static MelonPreferences_Entry<float> territoryUpdateIntervalEntry;
        private static MelonPreferences_Entry<float> unitSizeEntry;
        private static MelonPreferences_Entry<float> vehicleSizeEntry;
        private static MelonPreferences_Entry<float> updateTimerDelayEntry;
        private static MelonPreferences_Entry<float> infantryGroupRadiusEntry;
        private static MelonPreferences_Entry<int> minGroupSizeEntry;
        private static MelonPreferences_Entry<float> groupSizeMultiplierEntry;
        private static MelonPreferences_Entry<int> maxUnitsPerTeamEntry;
        
        // Properties to access config values
        private static float unitUpdateInterval => unitUpdateIntervalEntry.Value;
        private static float territoryUpdateInterval => territoryUpdateIntervalEntry.Value;
        private static float unitSize => unitSizeEntry.Value;
        private static float vehicleSize => vehicleSizeEntry.Value;
        private static float updateTimerDelay => updateTimerDelayEntry.Value;
        private static float infantryGroupRadius => infantryGroupRadiusEntry.Value;
        private static int minGroupSize => minGroupSizeEntry.Value;
        private static float groupSizeMultiplier => groupSizeMultiplierEntry.Value;
        private static int maxUnitsPerTeam => maxUnitsPerTeamEntry.Value;
        
        // Track unit groups for visualization
        private static Dictionary<int, List<UnitGroup>> teamUnitGroups = new Dictionary<int, List<UnitGroup>>();
        
        // Structure to track groups of units
        private class UnitGroup
        {
            public Vector2 position; // Average position of the group
            public int count; // Number of units in the group
            public bool isVehicle; // Whether this is a vehicle group
            public string vehicleName; // Name of the vehicle if applicable
            
            public float GetDisplaySize()
            {
                if (isVehicle)
                    return 0.015f + (Mathf.Min(count - 1, 10) * 0.003f); // Larger base size for vehicles
                else
                    return 0.01f + (Mathf.Min(count - 1, 20) * 0.002f); // Smaller base size for infantry
            }
            
            public float GetLineWidth()
            {
                if (isVehicle)
                    return 0.002f + (Mathf.Min(count - 1, 10) * 0.0005f); // Thicker lines for vehicles
                else
                    return 0.001f + (Mathf.Min(count - 1, 20) * 0.0003f); // Thinner lines for infantry
            }
        }
        
        // Register configuration settings
        public static void RegisterConfig()
        {
            // Create category for minimap settings
            minimapCategory = MelonPreferences.CreateCategory("MinimapUI");
            
            // Register minimap settings
            unitUpdateIntervalEntry = minimapCategory.CreateEntry("UnitUpdateInterval", 0.05f, "Unit Update Interval", 
                "How frequently unit positions are updated on the minimap");
            territoryUpdateIntervalEntry = minimapCategory.CreateEntry("TerritoryUpdateInterval", 0.05f, "Territory Update Interval",
                "How frequently territory is updated on the minimap");
            unitSizeEntry = minimapCategory.CreateEntry("UnitSize", 0.008f, "Unit Size",
                "Base size of infantry units on the minimap");
            vehicleSizeEntry = minimapCategory.CreateEntry("VehicleSize", 0.012f, "Vehicle Size",
                "Base size of vehicles on the minimap");
            updateTimerDelayEntry = minimapCategory.CreateEntry("UpdateTimerDelay", 0.5f, "Update Timer Delay",
                "Initial delay before starting minimap updates");
            infantryGroupRadiusEntry = minimapCategory.CreateEntry("InfantryGroupRadius", 0.03f, "Infantry Group Radius",
                "How close infantry must be to be grouped together");
            minGroupSizeEntry = minimapCategory.CreateEntry("MinGroupSize", 3, "Minimum Group Size",
                "Minimum number of units to form a group");
            groupSizeMultiplierEntry = minimapCategory.CreateEntry("GroupSizeMultiplier", 0.5f, "Group Size Multiplier",
                "How much larger groups appear on the minimap");
            maxUnitsPerTeamEntry = minimapCategory.CreateEntry("MaxUnitsPerTeam", 100, "Max Units Per Team",
                "Maximum number of units to draw per team for performance");

            // Load and save category
            minimapCategory.LoadFromFile();
            minimapCategory.SaveToFile();
        }

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void AwakePostfix(MinimapUi __instance)
        {
            // Register configuration if not already done
            if (minimapCategory == null)
            {
                RegisterConfig();
            }
            
            // Create materials for rendering
            unitMaterial = new Material(Shader.Find("UI/Default"));
            unitMaterial.SetFloat("_UseUIAlphaClip", 1);
            
            territoryMaterial = new Material(Shader.Find("UI/Default"));
            territoryMaterial.SetFloat("_UseUIAlphaClip", 1);
            
            // Initialize territory control system
            TerritoryControl.Initialize();

            // Set textures
            __instance.minimap.texture = MinimapUi.instance.minimap.texture;
            __instance.playerBlipsRT = MinimapUi.instance.playerBlipsRT;
            __instance.actorBlipsRTFront = MinimapUi.instance.actorBlipsRTFront;
        }

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void StartPostfix(MinimapUi __instance)
        {
            InitializeVisualization(__instance);
            // Start update coroutines
            __instance.StartCoroutine(UpdateUnitsCoroutine());
            __instance.StartCoroutine(UpdateTerritoriesCoroutine());

        }

            
        

        private static void InitializeVisualization(MinimapUi minimapUi)
        {
            // Get minimap resolution
            int resolution = MinimapCamera.instance.resolution;

            // Create render texture for territories (render first, below units)
            territoryRenderTexture = new RenderTexture(new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGB32, 0));
            territoryRenderTexture.Create();
            
            // Create territory image
            GameObject territoryObj = new GameObject("TerritoryOverlay");
            territoryObj.transform.SetParent(minimapUi.minimap.transform, false);
            territoryOverlayImage = territoryObj.AddComponent<UnityEngine.UI.RawImage>();
            territoryOverlayImage.texture = territoryRenderTexture;
            territoryOverlayImage.rectTransform.anchorMin = Vector2.zero;
            territoryOverlayImage.rectTransform.anchorMax = Vector2.one;
            territoryOverlayImage.rectTransform.offsetMin = Vector2.zero;
            territoryOverlayImage.rectTransform.offsetMax = Vector2.zero;
            
            // Create render texture for units
            unitRenderTexture = new RenderTexture(new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGB32, 0));
            unitRenderTexture.Create();
            
            // Create unit image
            GameObject unitObj = new GameObject("UnitOverlay");
            unitObj.transform.SetParent(minimapUi.minimap.transform, false);
            unitOverlayImage = unitObj.AddComponent<UnityEngine.UI.RawImage>();
            unitOverlayImage.texture = unitRenderTexture;
            unitOverlayImage.rectTransform.anchorMin = Vector2.zero;
            unitOverlayImage.rectTransform.anchorMax = Vector2.one;
            unitOverlayImage.rectTransform.offsetMin = Vector2.zero;
            unitOverlayImage.rectTransform.offsetMax = Vector2.zero;
            
            // Set draw order - territory below units
            territoryOverlayImage.transform.SetSiblingIndex(1);
            unitOverlayImage.transform.SetSiblingIndex(2);
        }

        private static System.Collections.IEnumerator UpdateUnitsCoroutine()
        {
            // Wait for the game to fully initialize
            yield return new WaitForSeconds(updateTimerDelay);
            
            while (true)
            {
                // Update unit visualization
                UpdateUnitVisualization();
                
                // Update territory data based on unit positions
                UpdateTerritoryData();
                
                // Check if game is paused
                if (GameManager.IsPaused())
                {
                    yield return new WaitUntil(() => !GameManager.IsPaused());
                }
                else
                {
                    // Update at regular intervals
                    yield return new WaitForSeconds(unitUpdateInterval);
                }
            }
        }
        
        private static System.Collections.IEnumerator UpdateTerritoriesCoroutine()
        {
            // Wait for the game to fully initialize
            yield return new WaitForSeconds(updateTimerDelay);
            
            int frameCounter = 0;
            
            while (true)
            {
                // Update territory visualization every frame
                UpdateTerritoryVisualization();
                
                frameCounter++;
                
                // Process influence decay less frequently for better performance
                if (frameCounter >= 10)
                {
                    TerritoryControl.ApplyInfluenceDecay();
                    TerritoryControl.ExtractTerritoryPointsFromInfluenceMap();
                    frameCounter = 0;
                }
                
                // Check if game is paused
                if (GameManager.IsPaused())
                {
                    yield return new WaitUntil(() => !GameManager.IsPaused());
                }
                else
                {
                    // Update at regular intervals (less frequent than units)
                    yield return new WaitForSeconds(territoryUpdateInterval);
                }
            }
        }

        private static void UpdateUnitVisualization()
        {
            // Skip if render texture is not ready
            if (unitRenderTexture == null)
                return;
            
            // Get minimap transformation matrix
            Matrix4x4 minimapMatrix = GetMinimapMatrix();
            
            // Set active render texture
            RenderTexture.active = unitRenderTexture;
            GL.Clear(false, true, Color.clear);
            
            // Draw units
            DrawUnits(minimapMatrix);
            
            // Reset active texture
            RenderTexture.active = null;
        }
        
        private static void UpdateTerritoryVisualization()
        {
            // Skip if render texture is not ready
            if (territoryRenderTexture == null)
                return;
            
            // Get minimap transformation matrix
            Matrix4x4 minimapMatrix = GetMinimapMatrix();
            
            // Set active render texture
            RenderTexture.active = territoryRenderTexture;
            GL.Clear(false, true, Color.clear);
            
            // Draw territories
            DrawTerritories(minimapMatrix);
            
            // Reset active texture
            RenderTexture.active = null;
        }
        
        private static void UpdateTerritoryData()
        {
            // Get minimap matrix for position conversion
            Matrix4x4 minimapMatrix = GetMinimapMatrix();
            
            // Update territory based on actor positions
            List<Actor> actors = ActorManager.instance.actors;
            
            foreach (Actor actor in actors)
            {
                ActorData actorData = ActorManager.instance.actorData[actor.actorIndex];
                
                // Skip actors in aircraft or airborne vehicles
                bool isSeatedInVehicle = false;
                bool isInAir = false;
                
                if (actor.IsSeated())
                {
                    Vehicle vehicle = actor.seat.vehicle;
                    isSeatedInVehicle = true;
                    isInAir = vehicle.IsAirborne() || vehicle.IsAircraft();
                }
                
                // Only consider alive actors on a team who are on the ground
                if (!actorData.dead && actorData.team >= 0 && !actor.isDeactivated && (!isSeatedInVehicle || !isInAir))
                {
                    // Skip actors in aircraft
                    if (TerritoryControl.groundUnitsOnly && actor.IsSeated() && 
                        (actor.seat.vehicle.IsAircraft() || actor.seat.vehicle.IsAirborne()))
                        continue;

                    // Convert world position to map coordinates
                    Vector3 worldPos = actorData.position;
                    Vector3 screenPos = minimapMatrix.MultiplyPoint(worldPos);
                    screenPos = ClampViewportPosition(screenPos);
                    
                    // Add influence to team territory
                    TerritoryControl.AddInfluence(actorData.team, new Vector2(screenPos.x, screenPos.y), false);
                }
            }
            
            // Also consider vehicles
            foreach (Vehicle vehicle in ActorManager.instance.vehicles)
            {
                bool vehicleHasActor = false;
                foreach (Seat seat in vehicle.seats)
                {
                    if (seat.IsOccupied()) {
                        vehicleHasActor = true;
                        break;
                    };
                }
                
                // Skip aircraft and airborne vehicles when groundUnitsOnly is true
                if (TerritoryControl.groundUnitsOnly && (vehicle.IsAircraft() || vehicle.IsAirborne()))
                    continue;
                    
                if (!vehicle.dead && vehicle.ownerTeam >= 0 && !vehicle.IsAirborne() && !vehicle.IsAircraft() && vehicleHasActor)
                {
                    // Convert world position to map coordinates
                    Vector3 worldPos = vehicle.transform.position;
                    Vector3 screenPos = minimapMatrix.MultiplyPoint(worldPos);
                    screenPos = ClampViewportPosition(screenPos);
                    
                    // Add vehicle influence to team territory (stronger than infantry)
                    TerritoryControl.AddInfluence(vehicle.ownerTeam, new Vector2(screenPos.x, screenPos.y), true);
                }
            }
            
            // Apply natural influence decay
            TerritoryControl.ApplyInfluenceDecay();
            
            // Update territory boundary points
            TerritoryControl.ExtractTerritoryPointsFromInfluenceMap();
        }
        
        private static Matrix4x4 GetMinimapMatrix()
        {
            // Get matrix through reflection
            Matrix4x4 viewportMatrix = (Matrix4x4)typeof(MinimapUi)
                .GetField("VIEWPORT_MATRIX", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
                
            return viewportMatrix * MinimapCamera.instance.camera.projectionMatrix *
                   MinimapCamera.instance.camera.worldToCameraMatrix;
        }
        
        private static void DrawTerritories(Matrix4x4 minimapMatrix)
        {
            // Set territory material
            territoryMaterial.SetPass(0);
            
            GL.PushMatrix();
            GL.LoadOrtho();
            
            // 1. Draw influence heat map
            GL.Begin(GL.QUADS);
            
            float cellSize = 1.0f / TerritoryControl.gridResolution;
            
            for (int x = 0; x < TerritoryControl.gridResolution; x++)
            {
                for (int y = 0; y < TerritoryControl.gridResolution; y++)
                {
                    float control = TerritoryControl.ControlMap[x, y];
                    
                    // Calculate cell corners
                    float x0 = (float)x / TerritoryControl.gridResolution;
                    float y0 = (float)y / TerritoryControl.gridResolution;
                    float x1 = x0 + cellSize;
                    float y1 = y0 + cellSize;
                    
                    // Set color based on control
                    if (control >= 0) // Has control
                    {
                        float strength = control; // Use control value directly for alpha
                        if (TerritoryControl.InfluenceMap[x, y] < 0) // Blue control
                        {
                            Color blueInfluence = new Color(0.0f, 0.0f, 0.8f, strength * 0.5f); // Scale alpha by 0.5 for better visibility
                            GL.Color(blueInfluence);
                        }
                        else // Red control
                        {
                            Color redInfluence = new Color(0.8f, 0.0f, 0.0f, strength * 0.5f); // Scale alpha by 0.5 for better visibility
                            GL.Color(redInfluence);
                        }
                    }
                    else // Neutral
                    {
                        continue; // Skip drawing neutral cells
                    }
                    
                    // Draw cell as quad
                    GL.Vertex3(x0, y0, 0);
                    GL.Vertex3(x1, y0, 0);
                    GL.Vertex3(x1, y1, 0);
                    GL.Vertex3(x0, y1, 0);
                }
            }
            
            GL.End();
            
            // 2. Create separate territory outlines for each team
            Dictionary<int, List<Vector2>> teamTerritoryPoints = new Dictionary<int, List<Vector2>>();
            
            // Collect territory points by team
            for (int x = 0; x < TerritoryControl.gridResolution; x++)
            {
                for (int y = 0; y < TerritoryControl.gridResolution; y++)
                {
                    float control = TerritoryControl.ControlMap[x, y];
                    if (control > 0) // Only consider cells with some control
                    {
                        // Determine team based on influence
                        int team = TerritoryControl.InfluenceMap[x, y] < 0 ? 0 : 1; // 0 for blue, 1 for red
                        
                        // Add the corner points to the team's territory
                        if (!teamTerritoryPoints.ContainsKey(team))
                            teamTerritoryPoints[team] = new List<Vector2>();
                            
                        float x0 = (float)x / TerritoryControl.gridResolution;
                        float y0 = (float)y / TerritoryControl.gridResolution;
                        
                        teamTerritoryPoints[team].Add(new Vector2(x0, y0));
                    }
                }
            }
            
            // Draw territory outline for each team separately
            foreach (var teamEntry in teamTerritoryPoints)
            {
                int team = teamEntry.Key;
                List<Vector2> points = teamEntry.Value;
                
                // Skip if not enough points
                if (points.Count < 3)
                    continue;
                
                // Get convex hull for territory polygon (team-specific)
                List<Vector2> hullPoints = TerritoryControl.GetConvexHull(points);
                
                // Skip if hull creation failed
                if (hullPoints.Count < 3)
                    continue;
                
                // Draw territory outline
                GL.Begin(GL.LINES);
                
                // Get team color
                Color teamColor = ColorScheme.TeamColor(team);
                Color outlineColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.9f);
                GL.Color(outlineColor);
                
                for (int i = 0; i < hullPoints.Count; i++)
                {
                    Vector2 current = hullPoints[i];
                    Vector2 next = hullPoints[(i + 1) % hullPoints.Count];
                    
                    GL.Vertex3(current.x, current.y, 0);
                    GL.Vertex3(next.x, next.y, 0);
                }
                
                GL.End();
            }
            
            GL.PopMatrix();
        }
        
        private static void DrawUnits(Matrix4x4 minimapMatrix)
        {
            // Set unit material
            unitMaterial.SetPass(0);
            
            GL.PushMatrix();
            GL.LoadOrtho();
            
            // Create unit groups
            CreateUnitGroups(minimapMatrix);
            
            // Draw unit groups for each team
            foreach (var teamEntry in teamUnitGroups)
            {
                int team = teamEntry.Key;
                List<UnitGroup> groups = teamEntry.Value;
                
                // Get team color
                Color teamColor = ColorScheme.TeamColor(team);
                
                // Draw each group
                foreach (UnitGroup group in groups)
                {
                    float size = group.GetDisplaySize();
                    float lineWidth = group.GetLineWidth();
                    
                    // Draw group as polygon outline
                    GL.Begin(GL.LINES);
                    
                    // Set color based on group type
                    if (group.isVehicle)
                    {
                        // Vehicle group - more solid color
                        Color vehicleColor = new Color(teamColor.r, teamColor.g, teamColor.b, 0.9f);
                        GL.Color(vehicleColor);
                    }
                    else
                    {
                        // Infantry group - adjust brightness based on size
                        float alphaAdjust = Mathf.Min(0.6f + (group.count * 0.03f), 0.9f);
                        Color groupColor = new Color(teamColor.r, teamColor.g, teamColor.b, alphaAdjust);
                        GL.Color(groupColor);
                    }
                    
                    // Draw polygon outline with line width
                    int segments = 8; // Number of sides in the polygon
                    for (int i = 0; i < segments; i++)
                    {
                        float angle1 = (i * 2 * Mathf.PI) / segments;
                        float angle2 = ((i + 1) * 2 * Mathf.PI) / segments;
                        
                        // Draw line with width
                        for (float w = -lineWidth/2; w <= lineWidth/2; w += lineWidth/4)
                        {
                            float x1 = group.position.x + (size + w) * Mathf.Cos(angle1);
                            float y1 = group.position.y + (size + w) * Mathf.Sin(angle1);
                            float x2 = group.position.x + (size + w) * Mathf.Cos(angle2);
                            float y2 = group.position.y + (size + w) * Mathf.Sin(angle2);
                            
                            GL.Vertex3(x1, y1, 0);
                            GL.Vertex3(x2, y2, 0);
                        }
                    }
                    
                    GL.End();
                }
            }
            
            GL.PopMatrix();
        }
        
        private static void CreateUnitGroups(Matrix4x4 minimapMatrix)
        {
            // Clear existing groups
            teamUnitGroups.Clear();
            teamUnitGroups[0] = new List<UnitGroup>(); // Blue team
            teamUnitGroups[1] = new List<UnitGroup>(); // Red team
            
            // Dictionary to track units by vehicle
            Dictionary<Vehicle, List<Actor>> vehicleOccupants = new Dictionary<Vehicle, List<Actor>>();
            // List to track actors not in vehicles
            List<Actor> infantryActors = new List<Actor>();
            
            // First pass: categorize actors as vehicle occupants or infantry
            foreach (Actor actor in ActorManager.instance.actors)
            {
                ActorData actorData = ActorManager.instance.actorData[actor.actorIndex];
                
                // Skip dead or neutral actors
                if (actorData.dead || actorData.team < 0 || actor.isDeactivated)
                    continue;
                
                // Group by vehicle if seated
                if (actor.IsSeated())
                {
                    Vehicle vehicle = actor.seat.vehicle;
                    
                    // Skip actors in aircraft if we're only tracking ground units
                    if (TerritoryControl.groundUnitsOnly && (vehicle.IsAircraft() || vehicle.IsAirborne()))
                        continue;
                    
                    if (!vehicleOccupants.ContainsKey(vehicle))
                        vehicleOccupants[vehicle] = new List<Actor>();
                        
                    vehicleOccupants[vehicle].Add(actor);
                }
                else
                {
                    // Add to infantry list
                    infantryActors.Add(actor);
                }
            }
            
            // Second pass: Create vehicle groups
            foreach (Vehicle vehicle in vehicleOccupants.Keys)
            {
                // Skip dead or neutral vehicles
                if (vehicle.dead || vehicle.ownerTeam < 0)
                    continue;
                    
                // Skip aircraft if we're only tracking ground units
                if (TerritoryControl.groundUnitsOnly && (vehicle.IsAircraft() || vehicle.IsAirborne()))
                    continue;
                
                // Get occupants
                List<Actor> occupants = vehicleOccupants[vehicle];
                if (occupants.Count == 0)
                    continue;
                
                // Create vehicle group
                Vector3 screenPos = minimapMatrix.MultiplyPoint(vehicle.transform.position);
                screenPos = ClampViewportPosition(screenPos);
                
                UnitGroup vehicleGroup = new UnitGroup()
                {
                    position = new Vector2(screenPos.x, screenPos.y),
                    count = occupants.Count,
                    isVehicle = true,
                    vehicleName = vehicle.name
                };
                
                // Add to team groups
                if (!teamUnitGroups.ContainsKey(vehicle.ownerTeam))
                    teamUnitGroups[vehicle.ownerTeam] = new List<UnitGroup>();
                    
                teamUnitGroups[vehicle.ownerTeam].Add(vehicleGroup);
            }
            
            // Third pass: Group infantry by proximity
            Dictionary<int, List<Vector2>> teamInfantryPositions = new Dictionary<int, List<Vector2>>();
            Dictionary<int, List<int>> teamInfantryIndices = new Dictionary<int, List<int>>();
            
            // Convert all infantry positions to screen space and organize by team
            for (int i = 0; i < infantryActors.Count; i++)
            {
                Actor actor = infantryActors[i];
                ActorData actorData = ActorManager.instance.actorData[actor.actorIndex];
                
                if (!teamInfantryPositions.ContainsKey(actorData.team))
                {
                    teamInfantryPositions[actorData.team] = new List<Vector2>();
                    teamInfantryIndices[actorData.team] = new List<int>();
                }
                
                Vector3 screenPos = minimapMatrix.MultiplyPoint(actorData.position);
                screenPos = ClampViewportPosition(screenPos);
                
                teamInfantryPositions[actorData.team].Add(new Vector2(screenPos.x, screenPos.y));
                teamInfantryIndices[actorData.team].Add(i);
            }
            
            // Process each team's infantry separately
            foreach (int team in teamInfantryPositions.Keys)
            {
                List<Vector2> positions = teamInfantryPositions[team];
                List<int> indices = teamInfantryIndices[team];
                bool[] processed = new bool[positions.Count];
                
                // Group infantry by proximity
                for (int i = 0; i < positions.Count; i++)
                {
                    if (processed[i])
                        continue;
                        
                    List<int> groupIndices = new List<int>();
                    groupIndices.Add(i);
                    processed[i] = true;
                    
                    // Find all nearby units
                    for (int j = 0; j < positions.Count; j++)
                    {
                        if (i == j || processed[j])
                            continue;
                            
                        if (Vector2.Distance(positions[i], positions[j]) <= infantryGroupRadius)
                        {
                            groupIndices.Add(j);
                            processed[j] = true;
                        }
                    }
                    
                    // Create group if enough units
                    if (groupIndices.Count >= minGroupSize)
                    {
                        // Calculate average position
                        Vector2 avgPos = Vector2.zero;
                        foreach (int idx in groupIndices)
                            avgPos += positions[idx];
                            
                        avgPos /= groupIndices.Count;
                        
                        // Create infantry group
                        UnitGroup infantryGroup = new UnitGroup()
                        {
                            position = avgPos,
                            count = groupIndices.Count,
                            isVehicle = false
                        };
                        
                        // Add to team groups
                        if (!teamUnitGroups.ContainsKey(team))
                            teamUnitGroups[team] = new List<UnitGroup>();
                            
                        teamUnitGroups[team].Add(infantryGroup);
                    }
                    else
                    {
                        // Add individual units
                        foreach (int idx in groupIndices)
                        {
                            UnitGroup singleUnit = new UnitGroup()
                            {
                                position = positions[idx],
                                count = 1,
                                isVehicle = false
                            };
                            
                            // Add to team groups
                            if (!teamUnitGroups.ContainsKey(team))
                                teamUnitGroups[team] = new List<UnitGroup>();
                                
                            teamUnitGroups[team].Add(singleUnit);
                        }
                    }
                }
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
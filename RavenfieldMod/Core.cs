using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using MelonLoader;
using System.Reflection;
using System.Linq;
using Lua.Wrapper;

[assembly: MelonInfo(typeof(BasicMinimapMod.Core), "Basic Minimap Territory Control", "1.1.0", "gaming", null)]
[assembly: MelonGame(null, null)]

namespace BasicMinimapMod
{
    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Basic Minimap Territory Control mod initialized!");
            HarmonyInstance.PatchAll();
        }
    }

    // Class to track territory control data
    public static class TerritoryControl
    {
        // Dictionary to store territory polygons for each team
        public static Dictionary<int, List<Vector2>> TeamTerritories = new Dictionary<int, List<Vector2>>();
        
        // Store detailed influence data across the map using a grid
        public static float[,] InfluenceMap;
        public static int[,] ControlMap; // -1 = neutral, 0 = blue, 1 = red
        
        // Grid resolution (higher = more detailed but slower)
        public static int gridResolution = 100;
        
        // Settings for territory expansion
        public static float expansionRadius = 25f;
        public static int maxPointsPerTeam = 200;
        public static float simplificationDistance = 0.05f; // Smaller value to get more points
        
        // Control point influence settings
        public static float infantryInfluence = 0.05f;  // How much influence a soldier exerts
        public static float vehicleInfluence = 0.07f;   // How much influence a vehicle exerts
        public static float influenceRadius = 0.1f;     // Radius of influence in map coordinates (0-1)
        public static float influenceFalloff = 2.0f;    // How quickly influence drops with distance
        public static float neutralDecayRate = 0.005f;  // How quickly unoccupied areas decay to neutral
        public static float frontlineThreshold = 0.2f;  // Threshold for determining frontlines
        
        // Flag to track ground-only control
        public static bool groundUnitsOnly = true;     // Only ground units influence territory
        
        // Initialize territories
        public static void Initialize()
        {
            TeamTerritories.Clear();
            // Add entries for blue and red teams
            TeamTerritories[0] = new List<Vector2>();
            TeamTerritories[1] = new List<Vector2>();
            
            // Initialize influence map grid
            InfluenceMap = new float[gridResolution, gridResolution];
            ControlMap = new int[gridResolution, gridResolution];
            
            // Initialize with neutral control (no initial territory)
            for (int x = 0; x < gridResolution; x++)
            {
                for (int y = 0; y < gridResolution; y++)
                {
                    ControlMap[x, y] = -1;
                    InfluenceMap[x, y] = 0f; // Start with no influence
                }
            }
            
            // No initial territory points - let them form naturally from unit movements
        }
        
        // Add influence from a unit to the influence map
        public static void AddInfluence(int team, Vector2 position, bool isVehicle)
        {
            float influenceValue = isVehicle ? vehicleInfluence : infantryInfluence;
            float radius = influenceRadius;
            
            // Red team = positive, Blue team = negative influence
            if (team == 0) // Blue team
                influenceValue = -influenceValue;
                
            // Slightly larger radius for vehicles
            if (isVehicle)
                radius *= 1.5f;
                
            // Calculate bounds of influence in grid coordinates
            int minX = Mathf.Max(0, Mathf.FloorToInt((position.x - radius) * gridResolution));
            int maxX = Mathf.Min(gridResolution - 1, Mathf.CeilToInt((position.x + radius) * gridResolution));
            int minY = Mathf.Max(0, Mathf.FloorToInt((position.y - radius) * gridResolution));
            int maxY = Mathf.Min(gridResolution - 1, Mathf.CeilToInt((position.y + radius) * gridResolution));
            
            // Apply influence to cells within radius
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    // Calculate cell center in world coordinates
                    float cellX = (x + 0.5f) / gridResolution;
                    float cellY = (y + 0.5f) / gridResolution;
                    
                    // Calculate distance from unit to cell
                    float distance = Vector2.Distance(position, new Vector2(cellX, cellY));
                    
                    if (distance <= radius)
                    {
                        // Calculate influence falloff with distance
                        float falloff = Mathf.Pow(1.0f - Mathf.Clamp01(distance / radius), influenceFalloff);
                        
                        // Apply influence to the cell
                        InfluenceMap[x, y] += influenceValue * falloff;
                        
                        // Update control map based on influence
                        if (InfluenceMap[x, y] < -frontlineThreshold)
                            ControlMap[x, y] = 0; // Blue control
                        else if (InfluenceMap[x, y] > frontlineThreshold)
                            ControlMap[x, y] = 1; // Red control
                        else
                            ControlMap[x, y] = -1; // Contested/neutral
                    }
                }
            }
        }
        
        // Apply natural decay to influence map (called periodically)
        public static void ApplyInfluenceDecay()
        {
            for (int x = 0; x < gridResolution; x++)
            {
                for (int y = 0; y < gridResolution; y++)
                {
                    // Decay influence toward neutral
                    if (InfluenceMap[x, y] > 0)
                        InfluenceMap[x, y] = Mathf.Max(0, InfluenceMap[x, y] - neutralDecayRate);
                    else if (InfluenceMap[x, y] < 0)
                        InfluenceMap[x, y] = Mathf.Min(0, InfluenceMap[x, y] + neutralDecayRate);
                        
                    // Update control if influence crossed thresholds
                    if (InfluenceMap[x, y] < -frontlineThreshold)
                        ControlMap[x, y] = 0; // Blue control
                    else if (InfluenceMap[x, y] > frontlineThreshold)
                        ControlMap[x, y] = 1; // Red control
                    else
                        ControlMap[x, y] = -1; // Contested/neutral
                }
            }
        }
        
        // Extract territory boundary points from the influence map
        public static void ExtractTerritoryPointsFromInfluenceMap()
        {
            // Clear existing territory points
            TeamTerritories[0].Clear();
            TeamTerritories[1].Clear();
            
            // Find boundary cells for each team
            List<Vector2> bluePoints = new List<Vector2>();
            List<Vector2> redPoints = new List<Vector2>();
            
            for (int x = 0; x < gridResolution; x++)
            {
                for (int y = 0; y < gridResolution; y++)
                {
                    // Check if this cell is a boundary cell for blue team
                    if (ControlMap[x, y] == 0 && IsBoundaryCell(x, y, 0))
                    {
                        float worldX = (x + 0.5f) / gridResolution;
                        float worldY = (y + 0.5f) / gridResolution;
                        bluePoints.Add(new Vector2(worldX, worldY));
                    }
                    
                    // Check if this cell is a boundary cell for red team
                    if (ControlMap[x, y] == 1 && IsBoundaryCell(x, y, 1))
                    {
                        float worldX = (x + 0.5f) / gridResolution;
                        float worldY = (y + 0.5f) / gridResolution;
                        redPoints.Add(new Vector2(worldX, worldY));
                    }
                }
            }
            
            // Simplify boundary points
            TeamTerritories[0] = SimplifyBoundaryPoints(bluePoints);
            TeamTerritories[1] = SimplifyBoundaryPoints(redPoints);
        }
        
        // Check if a cell is a boundary cell (has at least one adjacent cell of different control)
        private static bool IsBoundaryCell(int x, int y, int team)
        {
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
            
            for (int i = 0; i < dx.Length; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx >= 0 && nx < gridResolution && ny >= 0 && ny < gridResolution)
                {
                    if (ControlMap[nx, ny] != team)
                        return true;
                }
            }
            
            return false;
        }
        
        // Simplify boundary points to reduce the number of vertices
        private static List<Vector2> SimplifyBoundaryPoints(List<Vector2> points)
        {
            if (points.Count < 10)
                return points;
                
            // Use every Nth point to simplify
            List<Vector2> simplified = new List<Vector2>();
            int step = Mathf.Max(1, points.Count / maxPointsPerTeam);
            
            for (int i = 0; i < points.Count; i += step)
            {
                simplified.Add(points[i]);
            }
            
            return simplified;
        }
        
        // Add a point to a team's territory (old method, kept for compatibility)
        public static void AddTeamPoint(int team, Vector2 position)
        {
            // Use the influence system instead
            AddInfluence(team, position, false);
        }
        
        // Get frontline points where teams clash
        public static List<Vector2> GetFrontlinePoints()
        {
            List<Vector2> frontlinePoints = new List<Vector2>();
            
            for (int x = 0; x < gridResolution; x++)
            {
                for (int y = 0; y < gridResolution; y++)
                {
                    // Look for contested areas (near zero influence)
                    if (Mathf.Abs(InfluenceMap[x, y]) <= frontlineThreshold)
                    {
                        // Check if there's both blue and red influence nearby
                        bool hasBlue = false;
                        bool hasRed = false;
                        
                        // Check neighboring cells
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            for (int dy = -2; dy <= 2; dy++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                
                                if (nx >= 0 && nx < gridResolution && ny >= 0 && ny < gridResolution)
                                {
                                    if (InfluenceMap[nx, ny] < -frontlineThreshold)
                                        hasBlue = true;
                                    if (InfluenceMap[nx, ny] > frontlineThreshold)
                                        hasRed = true;
                                }
                            }
                        }
                        
                        // If area has both influences, it's a frontline
                        if (hasBlue && hasRed)
                        {
                            float worldX = (x + 0.5f) / gridResolution;
                            float worldY = (y + 0.5f) / gridResolution;
                            frontlinePoints.Add(new Vector2(worldX, worldY));
                        }
                    }
                }
            }
            
            return frontlinePoints;
        }
        
        // Old convex hull method, kept for compatibility
        public static List<Vector2> GetConvexHull(List<Vector2> points)
        {
            if (points.Count < 3)
                return points;
                
            // Simple gift wrapping algorithm for convex hull
            List<Vector2> hull = new List<Vector2>();
            
            // Find point with lowest y-coordinate
            int startIndex = 0;
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].y < points[startIndex].y || 
                   (points[i].y == points[startIndex].y && points[i].x < points[startIndex].x))
                {
                    startIndex = i;
                }
            }
            
            int currentPoint = startIndex;
            int nextPoint;
            
            do
            {
                hull.Add(points[currentPoint]);
                nextPoint = (currentPoint + 1) % points.Count;
                
                for (int i = 0; i < points.Count; i++)
                {
                    if (IsLeftTurn(points[currentPoint], points[i], points[nextPoint]))
                    {
                        nextPoint = i;
                    }
                }
                
                currentPoint = nextPoint;
                
            } while (currentPoint != startIndex && hull.Count < points.Count);
            
            return hull;
        }
        
        // Helper method for convex hull calculation
        private static bool IsLeftTurn(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return ((p2.x - p1.x) * (p3.y - p1.y) - (p2.y - p1.y) * (p3.x - p1.x)) > 0;
        }
    }

    [HarmonyPatch(typeof(MinimapUi))]
    public static class MinimapUiPatch
    {
        // Material for drawing units
        private static Material unitMaterial;
        
        // Material for drawing territory
        private static Material territoryMaterial;
        
        // Render texture for unit visualization
        private static RenderTexture unitRenderTexture;
        
        // Render texture for territory visualization
        private static RenderTexture territoryRenderTexture;
        
        // Image to display units on the minimap
        private static UnityEngine.UI.RawImage unitOverlayImage;
        
        // Image to display territories on the minimap
        private static UnityEngine.UI.RawImage territoryOverlayImage;
        
        // Configuration values
        private static float unitUpdateInterval = 0.05f;
        private static float territoryUpdateInterval = 0.05f;
        private static float unitSize = 0.008f;
        private static float vehicleSize = 0.012f;
        private static float updateTimerDelay = 0.5f;
        
        // Unit grouping settings
        private static float infantryGroupRadius = 0.03f; // How close infantry must be to be grouped
        private static int minGroupSize = 3; // Minimum number of units to form a group
        private static float groupSizeMultiplier = 0.5f; // How much larger groups appear
        
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
                    return vehicleSize + (Mathf.Min(count - 1, 5) * 0.002f);
                else
                    return unitSize + (Mathf.Min(count - 1, 10) * 0.001f);
            }
        }
        
        // Maximum number of units to draw per team for performance
        private static int maxUnitsPerTeam = 100;

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void AwakePostfix(MinimapUi __instance)
        {
            // Create materials for rendering
            unitMaterial = new Material(Shader.Find("UI/Default"));
            unitMaterial.SetFloat("_UseUIAlphaClip", 1);
            
            territoryMaterial = new Material(Shader.Find("UI/Default"));
            territoryMaterial.SetFloat("_UseUIAlphaClip", 1);
            
            // Initialize territory control system
            TerritoryControl.Initialize();
        }

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void StartPostfix(MinimapUi __instance)
        {
            // Initialize unit and territory visualization
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
                    float influence = TerritoryControl.InfluenceMap[x, y];
                    int control = TerritoryControl.ControlMap[x, y];
                    
                    // Calculate cell corners
                    float x0 = (float)x / TerritoryControl.gridResolution;
                    float y0 = (float)y / TerritoryControl.gridResolution;
                    float x1 = x0 + cellSize;
                    float y1 = y0 + cellSize;
                    
                    // Set color based on influence
                    if (influence < 0) // Blue influence
                    {
                        float strength = Mathf.Clamp01(-influence * 3.0f); // Amplify for visibility
                        Color blueInfluence = new Color(0.0f, 0.0f, 0.8f, 0.1f + strength * 0.4f);
                        GL.Color(blueInfluence);
                    }
                    else if (influence > 0) // Red influence
                    {
                        float strength = Mathf.Clamp01(influence * 3.0f); // Amplify for visibility
                        Color redInfluence = new Color(0.8f, 0.0f, 0.0f, 0.1f + strength * 0.4f);
                        GL.Color(redInfluence);
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
            
            // 2. Draw team territories with smooth borders
            foreach (var teamEntry in TerritoryControl.TeamTerritories)
            {
                int team = teamEntry.Key;
                List<Vector2> points = teamEntry.Value;
                
                // Skip if not enough points
                if (points.Count < 3)
                    continue;
                
                // Get convex hull for territory polygon
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
            
            // 3. Draw frontline with dashed battle line
            List<Vector2> frontlinePoints = TerritoryControl.GetFrontlinePoints();
            
            if (frontlinePoints.Count > 4)
            {
                // Sort frontline points to make a continuous line
                // (Simple approach - not perfect but good enough for visualization)
                List<Vector2> sortedFrontline = new List<Vector2>();
                sortedFrontline.Add(frontlinePoints[0]);
                frontlinePoints.RemoveAt(0);
                
                while (frontlinePoints.Count > 0)
                {
                    Vector2 last = sortedFrontline[sortedFrontline.Count - 1];
                    float closestDist = float.MaxValue;
                    int closestIndex = -1;
                    
                    for (int i = 0; i < frontlinePoints.Count; i++)
                    {
                        float dist = Vector2.Distance(last, frontlinePoints[i]);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestIndex = i;
                        }
                    }
                    
                    if (closestIndex != -1)
                    {
                        sortedFrontline.Add(frontlinePoints[closestIndex]);
                        frontlinePoints.RemoveAt(closestIndex);
                    }
                    else
                    {
                        break;
                    }
                }
                
                // Draw dashed battle line
                GL.Begin(GL.LINES);

                // Black frontline with dashes
                Color battleLineColor = Color.cyan;
                GL.Color(battleLineColor);
                
                bool dash = true;
                for (int i = 0; i < sortedFrontline.Count - 1; i++)
                {
                    // Toggle dash pattern
                    if (dash)
                    {
                        Vector2 current = sortedFrontline[i];
                        Vector2 next = sortedFrontline[i + 1];
                        
                        GL.Vertex3(current.x, current.y, 0);
                        GL.Vertex3(next.x, next.y, 0);
                    }
                    
                    dash = !dash;
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
            
            GL.Begin(GL.QUADS);
            
            // Create unit groups
            CreateUnitGroups(minimapMatrix);
            
            // Draw unit groups for each team
            foreach (var teamEntry in teamUnitGroups)
            {
                int team = teamEntry.Key;
                List<UnitGroup> groups = teamEntry.Value;
                
                // Get team color
                Color teamColor = ColorScheme.TeamColor(team);
                
                // Make sure color is visible
                Color brightColor = new Color(
                    Mathf.Min(1f, teamColor.r * 1.2f),
                    Mathf.Min(1f, teamColor.g * 1.2f),
                    Mathf.Min(1f, teamColor.b * 1.2f),
                    0.8f
                );
                
                // Draw each group
                foreach (UnitGroup group in groups)
                {
                    float size = group.GetDisplaySize();
                    
                    // Use different appearance for vehicles vs infantry groups
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
                        Color groupColor = new Color(brightColor.r, brightColor.g, brightColor.b, alphaAdjust);
                        GL.Color(groupColor);
                    }
                    
                    // Draw group as quad
                    GL.Vertex3(group.position.x - size, group.position.y - size, 0);
                    GL.Vertex3(group.position.x + size, group.position.y - size, 0);
                    GL.Vertex3(group.position.x + size, group.position.y + size, 0);
                    GL.Vertex3(group.position.x - size, group.position.y - size, 0);
                }
            }
            
            GL.End();
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
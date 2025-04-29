using UnityEngine;
using MelonLoader;
using System.Collections.Generic;

namespace URM
{
    // Class to track territory control data
    public static class TerritoryControl
    {
        // Dictionary to store territory polygons for each team
        public static Dictionary<int, List<Vector2>> TeamTerritories = new Dictionary<int, List<Vector2>>();
        
        // Store detailed influence data across the map using a grid
        public static float[,] InfluenceMap;
        public static int[,] ControlMap; // -1 = neutral, 0 = blue, 1 = red
        
        // Config categories
        private static MelonPreferences_Category territoryCategory;
        private static MelonPreferences_Category influenceCategory;
        
        // Config entries
        private static MelonPreferences_Entry<int> gridResolutionEntry;
        private static MelonPreferences_Entry<float> expansionRadiusEntry;
        private static MelonPreferences_Entry<int> maxPointsPerTeamEntry;
        private static MelonPreferences_Entry<float> simplificationDistanceEntry;
        private static MelonPreferences_Entry<float> infantryInfluenceEntry;
        private static MelonPreferences_Entry<float> vehicleInfluenceEntry;
        private static MelonPreferences_Entry<float> influenceRadiusEntry;
        private static MelonPreferences_Entry<float> influenceFalloffEntry;
        private static MelonPreferences_Entry<float> neutralDecayRateEntry;
        private static MelonPreferences_Entry<float> frontlineThresholdEntry;
        private static MelonPreferences_Entry<bool> groundUnitsOnlyEntry;
        
        // Properties to access config values
        public static int gridResolution => gridResolutionEntry.Value;
        public static float expansionRadius => expansionRadiusEntry.Value;
        public static int maxPointsPerTeam => maxPointsPerTeamEntry.Value;
        public static float simplificationDistance => simplificationDistanceEntry.Value;
        public static float infantryInfluence => infantryInfluenceEntry.Value;
        public static float vehicleInfluence => vehicleInfluenceEntry.Value;
        public static float influenceRadius => influenceRadiusEntry.Value;
        public static float influenceFalloff => influenceFalloffEntry.Value;
        public static float neutralDecayRate => neutralDecayRateEntry.Value;
        public static float frontlineThreshold => frontlineThresholdEntry.Value;
        public static bool groundUnitsOnly => groundUnitsOnlyEntry.Value;
        
        // Register configuration settings
        public static void RegisterConfig()
        {
            // Create category for territory settings
            territoryCategory = MelonPreferences.CreateCategory("TerritoryControl");
            
            // Create category for influence settings
            influenceCategory = MelonPreferences.CreateCategory("Influence");
            
            // Register territory settings
            gridResolutionEntry = territoryCategory.CreateEntry("GridResolution", 100, "Map Grid Resolution", 
                "Higher values provide more detailed territory but may impact performance");
            expansionRadiusEntry = territoryCategory.CreateEntry("ExpansionRadius", 10f, "Territory Expansion Radius",
                "Controls how far territory expands from controlled points");
            maxPointsPerTeamEntry = territoryCategory.CreateEntry("MaxPointsPerTeam", 200, "Max Territory Points", 
                "Maximum number of points used to draw territory boundaries");
            simplificationDistanceEntry = territoryCategory.CreateEntry("SimplificationDistance", 0.05f, "Simplification Distance",
                "Smaller values result in more detailed territory boundaries");
            
            // Register influence settings
            infantryInfluenceEntry = influenceCategory.CreateEntry("InfantryInfluence", 0.08f, "Infantry Influence", 
                "How much influence a soldier exerts on the map");
            vehicleInfluenceEntry = influenceCategory.CreateEntry("VehicleInfluence", 0.12f, "Vehicle Influence",
                "How much influence a vehicle exerts on the map");
            influenceRadiusEntry = influenceCategory.CreateEntry("InfluenceRadius", 0.15f, "Influence Radius",
                "Radius of influence in map coordinates (0-1)");
            influenceFalloffEntry = influenceCategory.CreateEntry("InfluenceFalloff", 1.5f, "Influence Falloff",
                "How quickly influence drops with distance");
            neutralDecayRateEntry = influenceCategory.CreateEntry("NeutralDecayRate", 0.008f, "Neutral Decay Rate",
                "How quickly unoccupied areas decay to neutral");
            frontlineThresholdEntry = influenceCategory.CreateEntry("FrontlineThreshold", 0.15f, "Frontline Threshold",
                "Threshold for determining frontlines");
            groundUnitsOnlyEntry = influenceCategory.CreateEntry("GroundUnitsOnly", true, "Ground Units Only",
                "If true, only ground units influence territory");

            // Load and save categories
            territoryCategory.LoadFromFile();
            influenceCategory.LoadFromFile();
            territoryCategory.SaveToFile();
            influenceCategory.SaveToFile();
        }
        
        // Initialize territories
        public static void Initialize()
        {
            // Register configuration if not already done
            if (territoryCategory == null)
            {
                RegisterConfig();
            }
            
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
}
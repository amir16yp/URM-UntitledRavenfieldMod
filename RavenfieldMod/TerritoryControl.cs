using UnityEngine;

namespace BasicMinimapMod
{
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
}
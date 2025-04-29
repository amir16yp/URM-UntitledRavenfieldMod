using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.Collections.Generic;

namespace URM
{
    public class AlwaysVisibleMinimap : MonoBehaviour
    {
        // Static instance for easy access
        public static AlwaysVisibleMinimap Instance { get; private set; }
        
        // UI components
        private RawImage minimapImage;
        private RectTransform minimapRect;
        private GameObject minimapContainer;
        private GameObject playerMarker;
        private RectTransform playerMarkerRect;
        
        // Render textures for visualization
        private RenderTexture unitRenderTexture;
        private RawImage unitOverlayImage;
        
        // Materials for rendering
        private Material unitMaterial;
        
        // Configuration
        private float scale = 1.0f;
        private Vector2 position = new Vector2(0.95f, 0.95f); // Default to top right
        private Vector2 size = new Vector2(400f, 400f);
        
        // Fixed zoom level
        private float zoomLevel = 2.5f;
        
        // Update intervals
        private float unitUpdateInterval = 0.05f;
        private float unitUpdateTimer = 0f;
        
        // Unit visualization settings
        private float unitSize = 0.008f;
        private float vehicleSize = 0.012f;
        private float infantryGroupRadius = 0.03f;
        private int minGroupSize = 3;
        private float groupSizeMultiplier = 0.5f;
        private int maxUnitsPerTeam = 100;
        
        // Tracking
        private Vector3 playerWorldPos;
        private Vector2 playerUVPosition = new Vector2(0.5f, 0.5f);
        private Dictionary<int, List<UnitGroup>> teamUnitGroups = new Dictionary<int, List<UnitGroup>>();
        
        // Minimap matrix for world to minimap conversion
        private Matrix4x4 minimapMatrix;
        
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
                    return AlwaysVisibleMinimap.Instance.vehicleSize + (Mathf.Min(count - 1, 5) * 0.002f);
                else
                    return AlwaysVisibleMinimap.Instance.unitSize + (Mathf.Min(count - 1, 10) * 0.001f);
            }
        }
        
        /// <summary>
        /// Initialize and create the always visible minimap
        /// </summary>
        /// <param name="scale">Scale of the minimap (relative to default size)</param>
        public static void Initialize(float scale = 0.7f)
        {
            // Don't create multiple instances
            if (Instance != null)
            {
                // Update scale if needed
                Instance.scale = scale;
                Instance.UpdateSize();
                return;
            }
            
            // Create container GameObject
            GameObject container = new GameObject("AlwaysVisibleMinimap");
            UnityEngine.Object.DontDestroyOnLoad(container);
            
            // Add the component
            AlwaysVisibleMinimap minimap = container.AddComponent<AlwaysVisibleMinimap>();
            minimap.scale = scale;
            
            // Setup UI components
            minimap.SetupUI();
            
            Instance = minimap;
        }
        
        private void SetupUI()
        {
            // Create UI canvas
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Ensure it's on top
            
            // Add canvas scaler for proper UI scaling
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            // Add Graphic Raycaster for input
            gameObject.AddComponent<GraphicRaycaster>();
            
            // Create container for the minimap
            minimapContainer = new GameObject("MinimapContainer");
            minimapContainer.transform.SetParent(transform, false);
            minimapRect = minimapContainer.AddComponent<RectTransform>();
            
            // Add background image
            Image background = minimapContainer.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.0f); // Reduced alpha from 0.7f
            
            // Calculate actual size
            UpdateSize();
            
            // Position the minimap (anchored to top right by default)
            minimapRect.anchorMin = position;
            minimapRect.anchorMax = position;
            minimapRect.pivot = new Vector2(1, 1); // Pivot at top right
            minimapRect.anchoredPosition = Vector2.zero;
            
            // Create materials for rendering
            unitMaterial = new Material(Shader.Find("UI/Default"));
            unitMaterial.SetFloat("_UseUIAlphaClip", 0);
            
            // Get minimap resolution
            int resolution = MinimapCamera.instance.resolution;
            
            // Create render texture for units
            unitRenderTexture = new RenderTexture(new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGB32, 0));
            unitRenderTexture.Create();
            
            // Create unit image
            GameObject unitObj = new GameObject("UnitOverlay");
            unitObj.transform.SetParent(minimapContainer.transform, false);
            unitOverlayImage = unitObj.AddComponent<RawImage>();
            unitOverlayImage.texture = unitRenderTexture;
            unitOverlayImage.rectTransform.anchorMin = Vector2.zero;
            unitOverlayImage.rectTransform.anchorMax = Vector2.one;
            unitOverlayImage.rectTransform.offsetMin = new Vector2(10, 10); // Same padding as minimap
            unitOverlayImage.rectTransform.offsetMax = new Vector2(-10, -10);
            
            // Create the minimap image
            GameObject imageObj = new GameObject("MinimapImage");
            imageObj.transform.SetParent(minimapContainer.transform, false);
            RectTransform imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0, 0);
            imageRect.anchorMax = new Vector2(1, 1);
            imageRect.offsetMin = new Vector2(10, 10); // 10px padding
            imageRect.offsetMax = new Vector2(-10, -10);
            
            // Add the minimap texture
            minimapImage = imageObj.AddComponent<RawImage>();
            minimapImage.texture = MinimapCamera.MINIMAP_RENDER_TEXTURE;
            
            // Set the initial UV rect to show properly zoomed view
            UpdateUVRect(new Vector2(0.5f, 0.5f));
            
            // Add player indicator
            AddPlayerIndicator();
            
            // Set draw order - units above minimap, player indicator on top
            imageObj.transform.SetSiblingIndex(0);
            unitOverlayImage.transform.SetSiblingIndex(1);
            playerMarker.transform.SetSiblingIndex(2);
        }
        
        private void AddPlayerIndicator()
        {
            // Create player indicator
            playerMarker = new GameObject("PlayerMarker");
            playerMarker.transform.SetParent(minimapContainer.transform, false);
            
            // Set up rect transform
            playerMarkerRect = playerMarker.AddComponent<RectTransform>();
            playerMarkerRect.sizeDelta = new Vector2(16, 16);
            playerMarkerRect.anchorMin = new Vector2(0.5f, 0.5f);
            playerMarkerRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerMarkerRect.anchoredPosition = Vector2.zero;
            
            // Add image component
            Image markerImage = playerMarker.AddComponent<Image>();
            markerImage.color = Color.yellow; // Make it more visible
            
            // Create a triangle shape for the marker
            markerImage.sprite = CreateTriangleSprite();
        }
        
        private Sprite CreateTriangleSprite()
        {
            // Create a simple triangle texture
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);
            Color white = Color.white;
            
            // Fill texture with transparent color first
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, transparent);
                }
            }
            
            // Draw triangle pointing upward
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    // Simple triangle shape
                    if (y >= x * 0.5f && y >= (texture.width - x) * 0.5f && y <= texture.height * 0.8f)
                    {
                        texture.SetPixel(x, y, white);
                    }
                }
            }
            
            texture.Apply();
            
            // Create sprite from texture
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        
        private void UpdateSize()
        {
            if (minimapRect != null)
            {
                // Scale the minimap based on the configured scale
                minimapRect.sizeDelta = size * scale;
            }
        }
        
        private void UpdateUVRect(Vector2 center)
        {
            // Calculate new UV rect size based on zoom level
            float rectSize = 1f / zoomLevel;
            
            // Make sure the UV rect stays within [0,1] range
            float halfWidth = rectSize * 0.5f;
            float halfHeight = rectSize * 0.5f;
            
            float minX = Mathf.Clamp(center.x - halfWidth, 0f, 1f - rectSize);
            float minY = Mathf.Clamp(center.y - halfHeight, 0f, 1f - rectSize);
            
            // Adjust center if needed
            if (minX != center.x - halfWidth)
            {
                center.x = minX + halfWidth;
            }
            if (minY != center.y - halfHeight)
            {
                center.y = minY + halfHeight;
            }
            
            // Set the new UV rect
            minimapImage.uvRect = new Rect(center.x - halfWidth, center.y - halfHeight, rectSize, rectSize);
            
            // Update overlay UV rects to match the minimap's
            if (unitOverlayImage != null)
            {
                unitOverlayImage.uvRect = minimapImage.uvRect;
            }
        }
        
        private void PositionPlayerMarker()
        {
            if (playerMarkerRect == null || minimapImage == null) 
                return;
            
            // Player marker stays in the center
            playerMarkerRect.anchoredPosition = Vector2.zero;
            
            // Rotate the marker to match player orientation
            if (Camera.main != null)
            {
                // Only rotate the player marker, not the entire map
                float angle = -Camera.main.transform.eulerAngles.y;
                playerMarkerRect.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
        
        // Get the minimap matrix just like in MinimapUIPatch
        private Matrix4x4 GetMinimapMatrix()
        {
            // Get matrix through reflection
            Matrix4x4 viewportMatrix = (Matrix4x4)typeof(MinimapUi)
                .GetField("VIEWPORT_MATRIX", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
                
            return viewportMatrix * MinimapCamera.instance.camera.projectionMatrix *
                   MinimapCamera.instance.camera.worldToCameraMatrix;
        }
        
        // Clamp position to viewport, just like in MinimapUIPatch
        private Vector3 ClampViewportPosition(Vector3 pos)
        {
            return new Vector3(
                Mathf.Clamp01(pos.x),
                Mathf.Clamp01(pos.y),
                pos.z
            );
        }
        
        private void UpdateUnitVisualization()
        {
            // Skip if render texture is not ready
            if (unitRenderTexture == null)
                return;
            
            // Get minimap transformation matrix
            minimapMatrix = GetMinimapMatrix();
            
            // Set active render texture
            RenderTexture.active = unitRenderTexture;
            GL.Clear(false, true, Color.clear);
            
            // Draw units
            DrawUnits();
            
            // Reset active texture
            RenderTexture.active = null;
        }
        
        private void DrawUnits()
        {
            // Set unit material
            unitMaterial.SetPass(0);
            
            GL.PushMatrix();
            GL.LoadOrtho();
            
            GL.Begin(GL.QUADS);
            
            // Create unit groups
            CreateUnitGroups();
            
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
        
        private void CreateUnitGroups()
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
        
        public void Update()
        {
            // Update unit visualization on a timer
            unitUpdateTimer += Time.deltaTime;
            if (unitUpdateTimer >= unitUpdateInterval)
            {
                UpdateUnitVisualization();
                unitUpdateTimer = 0f;
            }
            
            // Update player marker position and map focus
            if (Camera.main != null && MinimapCamera.instance != null)
            {
                if (FpsActorController.instance != null && FpsActorController.instance.actor != null)
                {
                    // Get player position from world space
                    playerWorldPos = FpsActorController.instance.actor.GetComponent<Actor>().Position();
                    
                    // Get normalized position for UV space
                    Vector3 normalizedPos = MinimapCamera.WorldToNormalizedPosition(playerWorldPos);
                    playerUVPosition = new Vector2(normalizedPos.x, normalizedPos.y);
                    
                    // Update the UV rect to center on player
                    UpdateUVRect(playerUVPosition);
                    
                    // Update player marker rotation
                    PositionPlayerMarker();
                }
                
                // The map itself does not rotate 
                minimapImage.rectTransform.rotation = Quaternion.identity;
            }
        }
    }
} 
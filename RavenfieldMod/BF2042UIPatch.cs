using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using TMPro;
using URM;

namespace URM
{
    [HarmonyPatch(typeof(IngameUI))]
    public class BF2042UIPatch
    {
        // Store original components for restoration if needed
        private static Dictionary<GameObject, Material> originalMaterials = new Dictionary<GameObject, Material>();
        private static Dictionary<Text, TextStyle> originalTextStyles = new Dictionary<Text, TextStyle>();
        
        // Track if we've applied UI changes
        private static bool uiModified = false;
        
        private struct TextStyle
        {
            public Font font;
            public FontStyle fontStyle;
            public int fontSize;
            public Color color;
        }
        
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(IngameUI __instance)
        {
            // Apply BF2042 styling once the UI is created
            MelonCoroutines.Start(ApplyBF2042Style(__instance));
        }
        
        private static System.Collections.IEnumerator ApplyBF2042Style(IngameUI ui)
        {
            // Wait a frame to ensure all UI elements are loaded
            yield return null;
            
            if (uiModified)
                yield break;
                
            uiModified = true;
            
            // Store all fonts for later potential reversion
            StoreOriginalStyles(ui);
            
            // Apply BF2042 style to UI components
            StyleHealthBars(ui);
            StyleWeaponInfo(ui);
            StyleVehicleInfo(ui);
            StyleSquadInfo(ui);
            StyleHitmarker(ui);
            StyleOverlayText(ui);
            
            // Log that the UI has been modified
            MelonLogger.Msg("Applied Battlefield 2042 UI style to Ravenfield");
        }
        
        private static void StoreOriginalStyles(IngameUI ui)
        {
            // Store text elements for potential restoration
            StoreTextStyle(ui.vehicleNameText);
            StoreTextStyle(ui.activeWeaponNameText);
            StoreTextStyle(ui.activeWeaponAmmoText);
            StoreTextStyle(ui.activeWeaponSpareAmmoText);
            StoreTextStyle(ui.activeScopeNameText);
            StoreTextStyle(ui.squadInfoText);
            StoreTextStyle(ui.overlayText);
        }
        
        private static void StoreTextStyle(Text text)
        {
            if (text != null && !originalTextStyles.ContainsKey(text))
            {
                originalTextStyles[text] = new TextStyle
                {
                    font = text.font,
                    fontStyle = text.fontStyle,
                    fontSize = text.fontSize,
                    color = text.color
                };
            }
        }
        
        private static void StyleHealthBars(IngameUI ui)
        {
            // Player health bar BF2042 style
            if (ui.playerHealthBar != null)
            {
                // Set colors
                ui.playerHealthBar.baseColor = BF2042Colors.Blue;
                ui.playerHealthBar.altColor = BF2042Colors.Red;
                
                // Add outline and glow
                AddOutlineToBar(ui.playerHealthBar.bar.gameObject, BF2042Colors.Blue, 2f);
                
                // Style health text
                if (ui.playerHealthBar.text != null)
                {
                    ui.playerHealthBar.text.fontSize += 4;
                    ui.playerHealthBar.text.fontStyle = FontStyle.Bold;
                    ui.playerHealthBar.text.color = BF2042Colors.White;
                    AddOutlineToText(ui.playerHealthBar.text, BF2042Colors.Black, 1f);
                }
                
                // Add segmented look
                AddSegmentedBarLook(ui.playerHealthBar.bar.gameObject, 5);
            }
            
            // Vehicle health bar
            if (ui.vehicleHealthBar != null)
            {
                ui.vehicleHealthBar.baseColor = BF2042Colors.Blue;
                ui.vehicleHealthBar.altColor = BF2042Colors.Red;
                
                AddOutlineToBar(ui.vehicleHealthBar.bar.gameObject, BF2042Colors.Blue, 2f);
                
                if (ui.vehicleHealthBar.text != null)
                {
                    ui.vehicleHealthBar.text.fontSize += 4;
                    ui.vehicleHealthBar.text.fontStyle = FontStyle.Bold;
                    ui.vehicleHealthBar.text.color = BF2042Colors.White;
                    AddOutlineToText(ui.vehicleHealthBar.text, BF2042Colors.Black, 1f);
                }
                
                AddSegmentedBarLook(ui.vehicleHealthBar.bar.gameObject, 5);
            }
            
            // Countermeasure bar
            if (ui.vehicleCountermeasureBar != null)
            {
                ui.vehicleCountermeasureBar.baseColor = BF2042Colors.Blue;
                ui.vehicleCountermeasureBar.altColor = BF2042Colors.Red;
                
                AddOutlineToBar(ui.vehicleCountermeasureBar.bar.gameObject, BF2042Colors.Blue, 1.5f);
                
                if (ui.vehicleCountermeasureBar.text != null)
                {
                    ui.vehicleCountermeasureBar.text.fontSize += 2;
                    ui.vehicleCountermeasureBar.text.fontStyle = FontStyle.Bold;
                    ui.vehicleCountermeasureBar.text.color = BF2042Colors.White;
                    AddOutlineToText(ui.vehicleCountermeasureBar.text, BF2042Colors.Black, 1f);
                }
            }
            
            // Reload bar 
            if (ui.reloadBar != null)
            {
                ui.reloadBar.baseColor = BF2042Colors.Blue;
                ui.reloadBar.altColor = BF2042Colors.Yellow;
                AddOutlineToBar(ui.reloadBar.bar.gameObject, BF2042Colors.Blue, 1.5f);
            }
        }
        
        private static void StyleWeaponInfo(IngameUI ui)
        {
            if (ui.loadoutElement != null)
            {
                // Set background panel style
                Image[] panelImages = ui.loadoutElement.GetComponentsInChildren<Image>(true);
                foreach (Image img in panelImages)
                {
                    // Skip the bar elements
                    if (img.transform.parent != null && img.transform.parent.name.Contains("Bar"))
                        continue;
                        
                    img.color = BF2042Colors.Background;
                }
                
                // Style weapon name
                if (ui.activeWeaponNameText != null)
                {
                    ui.activeWeaponNameText.fontSize += 3;
                    ui.activeWeaponNameText.fontStyle = FontStyle.Bold;
                    ui.activeWeaponNameText.color = BF2042Colors.White;
                    AddOutlineToText(ui.activeWeaponNameText, BF2042Colors.Black, 1f);
                }
                
                // Style ammo text - make it larger and modern looking
                if (ui.activeWeaponAmmoText != null)
                {
                    ui.activeWeaponAmmoText.fontSize += 6;
                    ui.activeWeaponAmmoText.fontStyle = FontStyle.Bold;
                    ui.activeWeaponAmmoText.color = BF2042Colors.Blue;
                    AddOutlineToText(ui.activeWeaponAmmoText, BF2042Colors.Black, 1.5f);
                }
                
                // Style spare ammo text
                if (ui.activeWeaponSpareAmmoText != null)
                {
                    ui.activeWeaponSpareAmmoText.fontSize += 3;
                    ui.activeWeaponSpareAmmoText.color = BF2042Colors.White;
                    AddOutlineToText(ui.activeWeaponSpareAmmoText, BF2042Colors.Black, 1f);
                }
                
                // Style scope name text
                if (ui.activeScopeNameText != null)
                {
                    ui.activeScopeNameText.fontSize += 2;
                    ui.activeScopeNameText.color = BF2042Colors.White;
                    ui.activeScopeNameText.fontStyle = FontStyle.Bold;
                    AddOutlineToText(ui.activeScopeNameText, BF2042Colors.Black, 0.8f);
                }
            }
            
            // Access weapon panels through the container
            if (ui.itemContainer != null)
            {
                // Find weapon panels in the container
                WeaponPanel[] weaponPanels = ui.itemContainer.GetComponentsInChildren<WeaponPanel>(true);
                
                foreach (WeaponPanel panel in weaponPanels)
                {
                    if (panel == null) continue;
                    
                    // Try to access the panel's text components
                    Text[] texts = panel.GetComponentsInChildren<Text>(true);
                    foreach (Text text in texts)
                    {
                        text.color = BF2042Colors.White;
                        text.fontStyle = FontStyle.Bold;
                        AddOutlineToText(text, BF2042Colors.Black, 0.8f);
                    }
                    
                    // Try to find the highlight image and change its color
                    Image[] images = panel.GetComponentsInChildren<Image>(true);
                    foreach (Image img in images)
                    {
                        if (img.name.Contains("Highlight") || img.name.Contains("highlight"))
                        {
                            img.color = BF2042Colors.Blue;
                        }
                        else
                        {
                            img.color = new Color(img.color.r, img.color.g, img.color.b, 0.8f);
                        }
                    }
                }
            }
        }
        
        private static void StyleVehicleInfo(IngameUI ui)
        {
            if (ui.vehicleContainer != null)
            {
                // Style vehicle name
                if (ui.vehicleNameText != null)
                {
                    ui.vehicleNameText.fontSize += 3;
                    ui.vehicleNameText.fontStyle = FontStyle.Bold;
                    ui.vehicleNameText.color = BF2042Colors.White;
                    AddOutlineToText(ui.vehicleNameText, BF2042Colors.Black, 1f);
                }
                
                // Style passenger count
                if (ui.vehiclePassengerCountText != null)
                {
                    ui.vehiclePassengerCountText.fontSize += 2;
                    ui.vehiclePassengerCountText.fontStyle = FontStyle.Bold;
                    ui.vehiclePassengerCountText.color = BF2042Colors.White;
                    AddOutlineToText(ui.vehiclePassengerCountText, BF2042Colors.Black, 1f);
                }
                
                // Style background panels
                Image[] images = ui.vehicleContainer.GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    // Skip the bar elements
                    if (img.transform.parent != null && img.transform.parent.name.Contains("Bar"))
                        continue;
                        
                    img.color = BF2042Colors.Background;
                }
            }
        }
        
        private static void StyleSquadInfo(IngameUI ui)
        {
            if (ui.squadInfoContainer != null)
            {
                // Style squad info text
                if (ui.squadInfoText != null)
                {
                    ui.squadInfoText.fontSize += 3;
                    ui.squadInfoText.fontStyle = FontStyle.Bold;
                    ui.squadInfoText.color = BF2042Colors.Blue;
                    AddOutlineToText(ui.squadInfoText, BF2042Colors.Black, 1f);
                }
                
                // Style background
                Image[] images = ui.squadInfoContainer.GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    img.color = BF2042Colors.Background;
                }
            }
            
            if (ui.squadPanelContainer != null)
            {
                // Find squad member panels in the container
                SquadMemberPanel[] squadPanels = ui.squadPanelContainer.GetComponentsInChildren<SquadMemberPanel>(true);
                
                foreach (SquadMemberPanel panel in squadPanels)
                {
                    if (panel == null) continue;
                    
                    // Try to access the panel's text components
                    Text[] texts = panel.GetComponentsInChildren<Text>(true);
                    foreach (Text text in texts)
                    {
                        text.color = BF2042Colors.White;
                        text.fontStyle = FontStyle.Bold;
                        AddOutlineToText(text, BF2042Colors.Black, 0.8f);
                    }
                    
                    // Style health bars in squad member panels
                    HealthBar[] healthbars = panel.GetComponentsInChildren<HealthBar>(true);
                    foreach (HealthBar hb in healthbars)
                    {
                        if (hb != null)
                        {
                            hb.baseColor = BF2042Colors.Blue;
                            hb.altColor = BF2042Colors.Red;
                            AddOutlineToBar(hb.bar.gameObject, BF2042Colors.Blue, 1.5f);
                        }
                    }
                    
                    // Style background panels
                    Image[] images = panel.GetComponentsInChildren<Image>(true);
                    foreach (Image img in images)
                    {
                        // Skip the bar elements
                        if (img.transform.parent != null && img.transform.parent.name.Contains("Bar"))
                            continue;
                            
                        img.color = BF2042Colors.Background;
                    }
                }
            }
        }
        
        private static void StyleHitmarker(IngameUI ui)
        {
            if (ui.hitmarker != null)
            {
                // Make hitmarker more BF2042-like (cross shape)
                ui.hitmarker.color = BF2042Colors.White;
                
                // Increase size slightly
                RectTransform rectTransform = ui.hitmarker.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    Vector2 size = rectTransform.sizeDelta;
                    rectTransform.sizeDelta = new Vector2(size.x * 1.2f, size.y * 1.2f);
                }
            }
        }
        
        private static void StyleOverlayText(IngameUI ui)
        {
            if (ui.overlayText != null)
            {
                ui.overlayText.fontSize += 4;
                ui.overlayText.fontStyle = FontStyle.Bold;
                ui.overlayText.color = BF2042Colors.White;
                AddOutlineToText(ui.overlayText, BF2042Colors.Black, 1.5f);
            }
            
            if (ui.flagCapturePanel != null)
            {
                // Style text elements in flag capture panel
                Text[] texts = ui.flagCapturePanel.GetComponentsInChildren<Text>(true);
                foreach (Text text in texts)
                {
                    text.fontSize += 2;
                    text.fontStyle = FontStyle.Bold;
                    text.color = BF2042Colors.White;
                    AddOutlineToText(text, BF2042Colors.Black, 1f);
                }
                
                // Style progress bar if any
                HealthBar[] bars = ui.flagCapturePanel.GetComponentsInChildren<HealthBar>(true);
                foreach (HealthBar bar in bars)
                {
                    if (bar != null)
                    {
                        bar.baseColor = BF2042Colors.Blue;
                        bar.altColor = BF2042Colors.Red;
                        AddOutlineToBar(bar.bar.gameObject, BF2042Colors.Blue, 1.5f);
                    }
                }
            }
        }
        
        private static void AddOutlineToText(Text text, Color outlineColor, float thickness)
        {
            if (text == null)
                return;
                
            // Check if outline already exists
            Outline outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }
            
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(thickness, thickness);
        }
        
        private static void AddOutlineToBar(GameObject barObject, Color glowColor, float thickness)
        {
            if (barObject == null)
                return;
                
            Image barImage = barObject.GetComponent<Image>();
            if (barImage == null)
                return;
                
            // Add outline component
            Outline outline = barObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = barObject.AddComponent<Outline>();
            }
            
            outline.effectColor = glowColor;
            outline.effectDistance = new Vector2(thickness, thickness);
        }
        
        private static void AddSegmentedBarLook(GameObject barObject, int segments)
        {
            if (barObject == null)
                return;
                
            // Check if already segmented
            if (barObject.transform.childCount > 0 && barObject.transform.GetChild(0).name == "SegmentOverlay")
                return;
                
            // Create segment dividers
            GameObject overlay = new GameObject("SegmentOverlay");
            overlay.transform.SetParent(barObject.transform, false);
            
            RectTransform rectTransform = overlay.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add horizontal layout
            HorizontalLayoutGroup layout = overlay.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            
            // Add segments
            for (int i = 1; i < segments; i++)
            {
                GameObject divider = new GameObject($"Divider_{i}");
                divider.transform.SetParent(overlay.transform, false);
                
                RectTransform dividerRect = divider.AddComponent<RectTransform>();
                dividerRect.sizeDelta = new Vector2(1, 0);
                
                Image dividerImg = divider.AddComponent<Image>();
                dividerImg.color = new Color(0.9f, 0.9f, 0.9f, 0.3f);
            }
        }
        
        [HarmonyPatch("PollPlayerHealth")]
        [HarmonyPostfix]
        public static void PollPlayerHealth_Postfix(IngameUI __instance, Actor player)
        {
            // Add damage pulse effect when health changes
            if (player.health < 30)
            {
                // Pulse the health bar for low health
                PulseHealthBar(__instance.playerHealthBar, BF2042Colors.Red);
            }
        }
        
        private static System.Collections.IEnumerator PulseEffect(GameObject target, Color startColor, Color endColor)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                yield break;
                
            float duration = 0.6f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.PingPong(elapsed * 2f, 1f);
                
                image.color = Color.Lerp(startColor, endColor, t);
                
                yield return null;
            }
            
            image.color = startColor;
        }
        
        private static void PulseHealthBar(HealthBar healthBar, Color pulseColor)
        {
            if (healthBar == null || healthBar.barGraphic == null)
                return;
                
            MelonCoroutines.Start(PulseEffect(healthBar.barGraphic.gameObject, healthBar.barGraphic.color, pulseColor));
        }
    }
} 
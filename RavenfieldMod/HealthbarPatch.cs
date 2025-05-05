using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

namespace URM
{
    [HarmonyPatch(typeof(HealthBar))]
    public class HealthbarPatch
    {
        // Track all created healthbars to apply style updates later
        private static List<HealthBar> activeHealthbars = new List<HealthBar>();
        
        // BF2042-inspired colors
        private static readonly Color bf2042HealthColor = new Color(0.05f, 0.85f, 0.95f, 1f); // Cyan blue
        private static readonly Color bf2042DamageColor = new Color(1f, 0.3f, 0.3f, 1f); // Red
        private static readonly Color bf2042BackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f); // Dark background
        
        // Additional UI elements for BF2042 style
        private static Dictionary<HealthBar, GameObject> segmentOverlays = new Dictionary<HealthBar, GameObject>();
        private static Dictionary<HealthBar, GameObject> damageIndicators = new Dictionary<HealthBar, GameObject>();
        private static Dictionary<HealthBar, float> previousHealth = new Dictionary<HealthBar, float>();
        
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(HealthBar __instance)
        {
            // Track this healthbar
            if (!activeHealthbars.Contains(__instance))
            {
                activeHealthbars.Add(__instance);
            }
            
            // Override default colors
            __instance.baseColor = bf2042HealthColor;
            __instance.altColor = bf2042DamageColor;
            
            // Set background color
            if (__instance.GetComponent<Image>() != null)
            {
                __instance.GetComponent<Image>().color = bf2042BackgroundColor;
            }
            
            // Set the bar color
            __instance.SetBarColor(false, 0f);
            
            // Add the segmented overlay effect
            AddSegmentOverlay(__instance);
            
            // Add damage indicator
            AddDamageIndicator(__instance);
            
            // Initialize health tracker
            previousHealth[__instance] = 1.0f;
            
            // Apply modern font style
            if (__instance.text != null)
            {
                __instance.text.fontSize += 2;
                __instance.text.fontStyle = FontStyle.Bold;
            }
        }
        
        [HarmonyPatch("SetBarProgress")]
        [HarmonyPrefix]
        public static bool SetBarProgress_Prefix(HealthBar __instance, float progress)
        {
            // Create damage animation effect
            if (previousHealth.ContainsKey(__instance) && progress < previousHealth[__instance])
            {
                // Show damage effect
                ShowDamageEffect(__instance, previousHealth[__instance], progress);
            }
            
            // Store current health
            previousHealth[__instance] = progress;
            
            // Let original method run
            return true;
        }
        
        private static void AddSegmentOverlay(HealthBar healthBar)
        {
            // Create a new overlay GameObject
            GameObject overlay = new GameObject("SegmentOverlay");
            overlay.transform.SetParent(healthBar.transform, false);
            
            // Add RectTransform with same size as healthbar
            RectTransform rectTransform = overlay.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add horizontal layout group
            HorizontalLayoutGroup layoutGroup = overlay.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 2f;
            layoutGroup.padding = new RectOffset(1, 1, 1, 1);
            
            // Add 5 segments (BF2042 style)
            for (int i = 0; i < 5; i++)
            {
                GameObject segment = new GameObject($"Segment_{i}");
                segment.transform.SetParent(overlay.transform, false);
                
                Image segmentImage = segment.AddComponent<Image>();
                segmentImage.color = new Color(1f, 1f, 1f, 0.2f);
            }
            
            segmentOverlays[healthBar] = overlay;
        }
        
        private static void AddDamageIndicator(HealthBar healthBar)
        {
            // Create damage indicator (the red bar that shows lost health)
            GameObject damageIndicator = new GameObject("DamageIndicator");
            damageIndicator.transform.SetParent(healthBar.transform, false);
            damageIndicator.transform.SetSiblingIndex(0); // Behind the main health bar
            
            // Set up RectTransform
            RectTransform rectTransform = damageIndicator.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add image component
            Image damageImage = damageIndicator.AddComponent<Image>();
            damageImage.color = bf2042DamageColor;
            
            // Store reference
            damageIndicators[healthBar] = damageIndicator;
        }
        
        private static void ShowDamageEffect(HealthBar healthBar, float previousProgress, float newProgress)
        {
            if (!damageIndicators.ContainsKey(healthBar))
                return;
                
            // Get the damage indicator
            GameObject damageObj = damageIndicators[healthBar];
            RectTransform damageTransform = damageObj.GetComponent<RectTransform>();
            
            // Set the damage indicator width to show how much health was lost
            damageTransform.anchorMin = new Vector2(newProgress, 0);
            damageTransform.anchorMax = new Vector2(previousProgress, 1);
            
            // Make sure it's visible
            if (damageObj.GetComponent<Image>() != null)
            {
                Image damageImage = damageObj.GetComponent<Image>();
                damageImage.color = bf2042DamageColor;
                
                // Fade out damage indicator over time
                MelonCoroutines.Start(FadeDamageIndicator(damageImage));
            }
        }
        
        private static System.Collections.IEnumerator FadeDamageIndicator(Image damageImage)
        {
            float duration = 0.8f;
            float elapsed = 0f;
            Color initialColor = damageImage.color;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                damageImage.color = new Color(initialColor.r, initialColor.g, initialColor.b, Mathf.Lerp(initialColor.a, 0f, t));
                yield return null;
            }
            
            // Ensure fully transparent at end
            damageImage.color = new Color(initialColor.r, initialColor.g, initialColor.b, 0f);
        }
    }
}

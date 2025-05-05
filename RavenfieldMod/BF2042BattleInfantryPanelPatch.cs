using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

namespace URM
{
    /// <summary>
    /// HarmonyPatch to reskin the BattleInfantryPanel with BF2042 styling
    /// </summary>
    [HarmonyPatch(typeof(BattleInfantryPanel))]
    public static class BF2042BattleInfantryPanelPatch
    {
        // Apply BF2042 styling when SetActive is called
        [HarmonyPatch("SetActive")]
        [HarmonyPostfix]
        public static void SetActive_Postfix(BattleInfantryPanel __instance)
        {
            ApplyBF2042Styling(__instance);
        }

        // Modify the ticket count text with BF2042 styling
        [HarmonyPatch("SetTicketCount")]
        [HarmonyPrefix]
        public static bool SetTicketCount_Prefix(BattleInfantryPanel __instance, int count)
        {
            __instance.label.text = count.ToString();
            __instance.label.color = BF2042Colors.White;
            __instance.label.fontSize += 2; // Slightly larger font
            __instance.label.fontStyle = FontStyle.Bold;
            return false; // Skip original method
        }

        // Modify the flashing behavior to match BF2042
        [HarmonyPatch("Flash")]
        [HarmonyPrefix]
        public static bool Flash_Prefix(BattleInfantryPanel __instance, ref IEnumerator __result)
        {
            __result = BF2042FlashCoroutine(__instance);
            return false; // Skip original method
        }

        // Custom flashing coroutine with BF2042 style
        private static IEnumerator BF2042FlashCoroutine(BattleInfantryPanel instance)
        {
            while (true)
            {
                // Pulse to BF2042 warning color
                CrossfadeColors(instance, BF2042Colors.Orange, 0.4f);
                yield return new WaitForSecondsRealtime(0.4f);
                
                // Back to normal
                CrossfadeColors(instance, Color.white, 0.4f);
                yield return new WaitForSecondsRealtime(1.2f);
            }
        }

        // Apply BF2042 styling to the panel
        private static void ApplyBF2042Styling(BattleInfantryPanel instance)
        {
            // Style the background
            if (instance.background != null)
            {
                instance.background.color = BF2042Colors.Background;
                
                // Add outline if needed
                var outline = instance.background.gameObject.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = instance.background.gameObject.AddComponent<Outline>();
                }
                outline.effectColor = BF2042Colors.Blue;
                outline.effectDistance = new Vector2(1.5f, 1.5f);
            }

            // Style the icon
            if (instance.icon != null)
            {
                instance.icon.color = BF2042Colors.White;
            }

            // Style the label
            if (instance.label != null)
            {
                instance.label.color = BF2042Colors.White;
                instance.label.fontStyle = FontStyle.Bold;
                
                // Increase size slightly
                instance.label.fontSize += 2;
            }

            // Style the dead object indicator
            if (instance.deadObject != null)
            {
                var deadImages = instance.deadObject.GetComponentsInChildren<Image>();
                foreach (var img in deadImages)
                {
                    img.color = BF2042Colors.Red;
                }
                
                var deadTexts = instance.deadObject.GetComponentsInChildren<Text>();
                foreach (var txt in deadTexts)
                {
                    txt.color = BF2042Colors.Red;
                    txt.fontStyle = FontStyle.Bold;
                }
            }
        }

        // Modified crossfade for colors instead of just alpha
        private static void CrossfadeColors(BattleInfantryPanel instance, Color targetColor, float duration)
        {
            if (instance.icon != null)
            {
                instance.icon.CrossFadeColor(targetColor, duration, true, true);
            }
            
            if (instance.label != null)
            {
                instance.label.CrossFadeColor(targetColor, duration, true, true);
            }
        }

        // Override the original CrossfadeAlpha to add BF2042 styling
        [HarmonyPatch("CrossfadeAlpha")]
        [HarmonyPrefix]
        public static bool CrossfadeAlpha_Prefix(BattleInfantryPanel __instance, float targetAlpha, float duration)
        {
            // Apply custom color tinting based on alpha
            Color iconColor = targetAlpha < 0.5f ? 
                BF2042Colors.Gray.WithAlpha(targetAlpha) : 
                BF2042Colors.White.WithAlpha(targetAlpha);
                
            __instance.icon.CrossFadeColor(iconColor, duration, true, true);
            __instance.label.CrossFadeColor(iconColor, duration, true, true);
            
            return false; // Skip original method
        }

        // Override the Die method to add BF2042 styling
        [HarmonyPatch("Die")]
        [HarmonyPrefix]
        public static bool Die_Prefix(BattleInfantryPanel __instance)
        {
            __instance.icon.enabled = false;
            __instance.label.enabled = false;
            
            // Apply BF2042 styling to the dead indicator
            if (__instance.deadObject != null)
            {
                __instance.deadObject.SetActive(true);
                
                // Find any Images in the dead object and set them to BF2042 red
                var images = __instance.deadObject.GetComponentsInChildren<Image>();
                foreach (var img in images)
                {
                    img.color = BF2042Colors.Red;
                }
                
                // Find any Text in the dead object and style it
                var texts = __instance.deadObject.GetComponentsInChildren<Text>();
                foreach (var txt in texts)
                {
                    txt.color = BF2042Colors.Red;
                    txt.fontStyle = FontStyle.Bold;
                }
            }
            
            __instance.StopFlashing();
            return false; // Skip original method
        }
    }
} 
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;
using TMPro;
using System.Reflection;

namespace URM
{
    /// <summary>
    /// Harmony patch to transform the vanilla Ravenfield Main Menu UI into a Battlefield 2042 style design
    /// </summary>
    [HarmonyPatch(typeof(MainMenu))]
    public class MainMenuPatch
    {
        // UI elements that we create and need to reference
        private static GameObject bf2042Logo;
        private static GameObject bf2042Background;
        private static GameObject bf2042TitleBar;
        private static List<GameObject> bf2042MenuButtons = new List<GameObject>();
        
        // Custom audio source for BF2042 theme (if available)
        private static AudioSource bf2042ThemeMusic;
        
        // Cache of original bg color for transitions
        private static Color originalBgColor;
        
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(MainMenu __instance)
        {
            try
            {
                Debug.Log("[URM] Applying BF2042 style to Main Menu");
                
                // Store reference to the background for future styling
                if (__instance.background != null)
                {
                    Image bgImage = __instance.background.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        originalBgColor = bgImage.color;
                        bgImage.color = BF2042Colors.Background.WithAlpha(0.9f);
                    }
                }
                
                // Create a title bar at the top of the screen
                CreateTitleBar(__instance);
                
                // Style each menu page
                StyleMainMenuPage(__instance.mainMenu);
                StylePlayPage(__instance.play);
                StyleInstantActionPage(__instance.instantAction);
                StyleCampaignPage(__instance.campaign);
                StyleModsPage(__instance.mods);
                
                // Apply style to back button
                if (__instance.backButton != null)
                {
                    Button backBtn = __instance.backButton.GetComponentInChildren<Button>();
                    if (backBtn != null)
                    {
                        ColorBlock colors = backBtn.colors;
                        colors.normalColor = BF2042Colors.DarkBlue;
                        colors.highlightedColor = BF2042Colors.Blue;
                        colors.pressedColor = BF2042Colors.Blue.Darker();
                        colors.selectedColor = BF2042Colors.Blue;
                        backBtn.colors = colors;
                        
                        // Style text if it exists
                        Text text = backBtn.GetComponentInChildren<Text>();
                        if (text != null)
                        {
                            ConvertTextToTMP(text, BF2042Colors.White, 18, FontStyles.Bold);
                        }
                    }
                }
                
                Debug.Log("[URM] BF2042 Main Menu styling completed");
            }
            catch (Exception e)
            {
                Debug.LogError($"[URM] Error applying BF2042 style to Main Menu: {e.Message}");
                Debug.LogException(e);
            }
        }
        
        private static void CreateTitleBar(MainMenu mainMenu)
        {
            // Create title bar at the top of the screen
            Canvas canvas = mainMenu.GetComponentInChildren<Canvas>();
            if (canvas == null) return;
            
            bf2042TitleBar = new GameObject("BF2042TitleBar");
            bf2042TitleBar.transform.SetParent(canvas.transform, false);
            
            // Add image component for background
            Image barBg = bf2042TitleBar.AddComponent<Image>();
            barBg.color = BF2042Colors.Black.WithAlpha(0.7f);
            
            // Position the bar at the top
            RectTransform rectTransform = bf2042TitleBar.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.sizeDelta = new Vector2(0, 80);
            
            // Create title text
            GameObject titleText = new GameObject("BF2042Title");
            titleText.transform.SetParent(bf2042TitleBar.transform, false);
            
            TextMeshProUGUI tmpText = titleText.AddComponent<TextMeshProUGUI>();
            tmpText.text = "RAVENFIELD";
            tmpText.color = BF2042Colors.White;
            tmpText.fontSize = 40;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontStyle = FontStyles.Bold;
            
            // Apply BF2042 style to the text (all caps, slightly spaced)
            tmpText.characterSpacing = 10;
            
            // Position the text
            RectTransform textRect = titleText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(0, 0);
            textRect.offsetMax = new Vector2(0, 0);
            
            // Create decorative blue line under the title bar
            GameObject blueLine = new GameObject("BF2042BlueLine");
            blueLine.transform.SetParent(bf2042TitleBar.transform, false);
            
            Image lineImage = blueLine.AddComponent<Image>();
            lineImage.color = BF2042Colors.Blue;
            
            RectTransform lineRect = blueLine.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0, 0);
            lineRect.anchorMax = new Vector2(1, 0);
            lineRect.pivot = new Vector2(0.5f, 0);
            lineRect.sizeDelta = new Vector2(0, 2);
        }
        
        private static void StyleMainMenuPage(GameObject mainMenuPage)
        {
            if (mainMenuPage == null) return;
            
            // Find all buttons in the main menu and style them
            Button[] buttons = mainMenuPage.GetComponentsInChildren<Button>(true);
            
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                GameObject buttonObj = button.gameObject;
                
                // Create a new BF2042 style button
                GameObject bf2042Button = CreateBF2042Button(button, i);
                bf2042Button.transform.SetParent(buttonObj.transform.parent, false);
                
                // Match the original button's position and size
                RectTransform originalRect = buttonObj.GetComponent<RectTransform>();
                RectTransform newRect = bf2042Button.GetComponent<RectTransform>();
                
                if (originalRect != null && newRect != null)
                {
                    newRect.anchorMin = originalRect.anchorMin;
                    newRect.anchorMax = originalRect.anchorMax;
                    newRect.pivot = originalRect.pivot;
                    newRect.sizeDelta = originalRect.sizeDelta;
                    newRect.anchoredPosition = originalRect.anchoredPosition;
                    
                    // Make the button slightly larger for BF2042 style
                    newRect.sizeDelta = new Vector2(newRect.sizeDelta.x + 20, newRect.sizeDelta.y + 10);
                }
                
                // Copy the button's onClick
                Button newButton = bf2042Button.GetComponent<Button>();
                newButton.onClick.AddListener(() => button.onClick.Invoke());
                
                // Store reference to our custom button
                bf2042MenuButtons.Add(bf2042Button);
                
                // Disable the original button
                button.gameObject.SetActive(false);
            }
        }
        
        private static void StylePlayPage(GameObject playPage)
        {
            if (playPage == null) return;
            
            // Similar approach to main menu - find buttons and replace with BF2042 style
            Button[] buttons = playPage.GetComponentsInChildren<Button>(true);
            
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                GameObject buttonObj = button.gameObject;
                
                // Create a new BF2042 style button
                GameObject bf2042Button = CreateBF2042Button(button, i);
                bf2042Button.transform.SetParent(buttonObj.transform.parent, false);
                
                // Match position and size
                RectTransform originalRect = buttonObj.GetComponent<RectTransform>();
                RectTransform newRect = bf2042Button.GetComponent<RectTransform>();
                
                if (originalRect != null && newRect != null)
                {
                    newRect.anchorMin = originalRect.anchorMin;
                    newRect.anchorMax = originalRect.anchorMax;
                    newRect.pivot = originalRect.pivot;
                    newRect.sizeDelta = originalRect.sizeDelta;
                    newRect.anchoredPosition = originalRect.anchoredPosition;
                    newRect.sizeDelta = new Vector2(newRect.sizeDelta.x + 20, newRect.sizeDelta.y + 10);
                }
                
                // Copy the button's onClick
                Button newButton = bf2042Button.GetComponent<Button>();
                newButton.onClick.AddListener(() => button.onClick.Invoke());
                
                // Store reference to our custom button
                bf2042MenuButtons.Add(bf2042Button);
                
                // Disable the original button
                button.gameObject.SetActive(false);
            }
        }
        
        private static void StyleInstantActionPage(GameObject instantActionPage)
        {
            // Style the instant action page with BF2042 look
            if (instantActionPage == null) return;
            
            // Add a semi-transparent panel background
            GameObject panelBg = new GameObject("BF2042PanelBg");
            panelBg.transform.SetParent(instantActionPage.transform, false);
            panelBg.transform.SetAsFirstSibling();
            
            Image bgImage = panelBg.AddComponent<Image>();
            bgImage.color = BF2042Colors.Background.WithAlpha(0.8f);
            
            RectTransform bgRect = panelBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.1f, 0.1f);
            bgRect.anchorMax = new Vector2(0.9f, 0.9f);
            bgRect.offsetMin = new Vector2(0, 0);
            bgRect.offsetMax = new Vector2(0, 0);
            
            // Style buttons with BF2042 theme
            Button[] buttons = instantActionPage.GetComponentsInChildren<Button>(true);
            
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                
                // Skip if it's a toggle button or special control
                if (button.gameObject.GetComponent<Toggle>() != null) continue;
                
                ColorBlock colors = button.colors;
                colors.normalColor = BF2042Colors.DarkBlue;
                colors.highlightedColor = BF2042Colors.Blue;
                colors.pressedColor = BF2042Colors.Blue.Darker();
                colors.selectedColor = BF2042Colors.Blue;
                button.colors = colors;
                
                // Style text if it exists
                Text text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    ConvertTextToTMP(text, BF2042Colors.White, 16, FontStyles.Bold);
                }
            }
            
            // Style the dropdowns and sliders
            Dropdown[] dropdowns = instantActionPage.GetComponentsInChildren<Dropdown>(true);
            foreach (Dropdown dropdown in dropdowns)
            {
                // Style the dropdown to match BF2042
                ColorBlock colors = dropdown.colors;
                colors.normalColor = BF2042Colors.DarkBlue;
                colors.highlightedColor = BF2042Colors.Blue;
                colors.pressedColor = BF2042Colors.Blue.Darker();
                colors.selectedColor = BF2042Colors.Blue;
                dropdown.colors = colors;
                
                // Style text
                Text text = dropdown.captionText;
                if (text != null)
                {
                    ConvertTextToTMP(text, BF2042Colors.White, 14, FontStyles.Normal);
                }
            }
            
            // Style sliders
            Slider[] sliders = instantActionPage.GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                // Get the fill area and style it
                Image fillImage = slider.fillRect?.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = BF2042Colors.Blue;
                }
                
                // Style the handle
                Image handleImage = slider.handleRect?.GetComponent<Image>();
                if (handleImage != null)
                {
                    handleImage.color = BF2042Colors.White;
                }
                
                // Style the background
                Image bgSliderImage = slider.GetComponent<Image>();
                if (bgSliderImage != null)
                {
                    bgSliderImage.color = BF2042Colors.Background.Darker();
                }
            }
        }
        
        private static void StyleCampaignPage(GameObject campaignPage)
        {
            // Style the campaign page with BF2042 look
            if (campaignPage == null) return;
            
            // Add a semi-transparent panel background
            GameObject panelBg = new GameObject("BF2042PanelBg");
            panelBg.transform.SetParent(campaignPage.transform, false);
            panelBg.transform.SetAsFirstSibling();
            
            Image bgImage = panelBg.AddComponent<Image>();
            bgImage.color = BF2042Colors.Background.WithAlpha(0.8f);
            
            RectTransform bgRect = panelBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.1f, 0.1f);
            bgRect.anchorMax = new Vector2(0.9f, 0.9f);
            bgRect.offsetMin = new Vector2(0, 0);
            bgRect.offsetMax = new Vector2(0, 0);
            
            // Style buttons
            Button[] buttons = campaignPage.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = BF2042Colors.DarkBlue;
                colors.highlightedColor = BF2042Colors.Blue;
                colors.pressedColor = BF2042Colors.Blue.Darker();
                colors.selectedColor = BF2042Colors.TeamBlue;
                button.colors = colors;
                
                // Style text
                Text text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    ConvertTextToTMP(text, BF2042Colors.White, 16, FontStyles.Bold);
                }
            }
            
            // Add BF2042 style headers
            Text[] texts = campaignPage.GetComponentsInChildren<Text>(true);
            foreach (Text text in texts)
            {
                // Skip button texts that were already processed
                if (text.GetComponentInParent<Button>() != null) continue;
                
                // Convert to TextMeshPro
                ConvertTextToTMP(text, BF2042Colors.White, text.fontSize * 1.2f, FontStyles.Bold);
            }
        }
        
        private static void StyleModsPage(GameObject modsPage)
        {
            // Style the mods page with BF2042 look
            if (modsPage == null) return;
            
            // Add a semi-transparent panel background
            GameObject panelBg = new GameObject("BF2042PanelBg");
            panelBg.transform.SetParent(modsPage.transform, false);
            panelBg.transform.SetAsFirstSibling();
            
            Image bgImage = panelBg.AddComponent<Image>();
            bgImage.color = BF2042Colors.Background.WithAlpha(0.8f);
            
            RectTransform bgRect = panelBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.1f, 0.1f);
            bgRect.anchorMax = new Vector2(0.9f, 0.9f);
            bgRect.offsetMin = new Vector2(0, 0);
            bgRect.offsetMax = new Vector2(0, 0);
            
            // Style buttons
            Button[] buttons = modsPage.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.gameObject.GetComponent<Toggle>() != null) continue;
                
                ColorBlock colors = button.colors;
                colors.normalColor = BF2042Colors.DarkBlue;
                colors.highlightedColor = BF2042Colors.Blue;
                colors.pressedColor = BF2042Colors.Blue.Darker();
                colors.selectedColor = BF2042Colors.Blue;
                button.colors = colors;
                
                // Style text
                Text text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    ConvertTextToTMP(text, BF2042Colors.White, 16, FontStyles.Bold);
                }
            }
            
            // Style toggles
            Toggle[] toggles = modsPage.GetComponentsInChildren<Toggle>(true);
            foreach (Toggle toggle in toggles)
            {
                ColorBlock colors = toggle.colors;
                colors.normalColor = BF2042Colors.DarkBlue;
                colors.highlightedColor = BF2042Colors.Blue;
                colors.pressedColor = BF2042Colors.Blue.Darker();
                colors.selectedColor = BF2042Colors.Blue;
                toggle.colors = colors;
                
                // Style the checkmark if possible
                Transform checkmark = toggle.graphic?.transform;
                if (checkmark != null)
                {
                    Image checkImage = checkmark.GetComponent<Image>();
                    if (checkImage != null)
                    {
                        checkImage.color = BF2042Colors.Green;
                    }
                }
                
                // Style the background
                Image bgToggleImage = toggle.GetComponent<Image>();
                if (bgToggleImage != null)
                {
                    bgToggleImage.color = BF2042Colors.Background.Darker();
                }
                
                // Style text
                Text text = toggle.GetComponentInChildren<Text>();
                if (text != null)
                {
                    ConvertTextToTMP(text, BF2042Colors.White, 14, FontStyles.Normal);
                }
            }
        }
        
        private static GameObject CreateBF2042Button(Button originalButton, int index)
        {
            // Get the button text
            Text originalText = originalButton.GetComponentInChildren<Text>();
            string buttonText = originalText != null ? originalText.text.ToUpper() : "BUTTON";
            
            // Create new button game object
            GameObject buttonObj = new GameObject("BF2042Button_" + index);
            
            // Add necessary components
            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            Image buttonImage = buttonObj.AddComponent<Image>();
            Button button = buttonObj.AddComponent<Button>();
            
            // Style the button
            buttonImage.color = BF2042Colors.DarkBlue;
            
            // Create the button's text
            GameObject textObj = new GameObject("ButtonText");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = buttonText;
            tmpText.color = BF2042Colors.White;
            tmpText.fontSize = 18;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontStyle = FontStyles.Bold;
            
            // Add a blue accent line to the left side of button
            GameObject accentLine = new GameObject("AccentLine");
            accentLine.transform.SetParent(buttonObj.transform, false);
            
            Image accentImage = accentLine.AddComponent<Image>();
            accentImage.color = BF2042Colors.Blue;
            
            RectTransform accentRect = accentLine.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0);
            accentRect.anchorMax = new Vector2(0, 1);
            accentRect.pivot = new Vector2(0, 0.5f);
            accentRect.sizeDelta = new Vector2(3, 0);
            
            // Set up the text's rect transform
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(10, 0);  // Add some padding
            textRect.offsetMax = new Vector2(-10, 0);
            
            // Style the button's color transitions
            ColorBlock colors = button.colors;
            colors.normalColor = BF2042Colors.DarkBlue;
            colors.highlightedColor = BF2042Colors.Blue;
            colors.pressedColor = BF2042Colors.Blue.Darker();
            colors.selectedColor = BF2042Colors.Blue;
            colors.fadeDuration = 0.1f;  // Quick transitions
            button.colors = colors;
            
            return buttonObj;
        }
        
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update_Postfix(MainMenu __instance)
        {
            // Add some dynamic effects to our BF2042 UI elements
            if (bf2042TitleBar != null)
            {
                // Find the blue line and pulse it
                Transform blueLine = bf2042TitleBar.transform.Find("BF2042BlueLine");
                if (blueLine != null)
                {
                    Image lineImage = blueLine.GetComponent<Image>();
                    if (lineImage != null)
                    {
                        float pulse = Mathf.PingPong(Time.time * 0.5f, 0.3f) + 0.7f;
                        lineImage.color = BF2042Colors.Blue.WithAlpha(pulse);
                    }
                }
            }
            
            // Pulse the accent lines on buttons when hovered
            foreach (GameObject buttonObj in bf2042MenuButtons)
            {
                Button button = buttonObj.GetComponent<Button>();
                if (button == null) continue;
                
                Transform accentLine = buttonObj.transform.Find("AccentLine");
                if (accentLine == null) continue;
                
                Image accentImage = accentLine.GetComponent<Image>();
                if (accentImage == null) continue;
                
                // Check if this button is highlighted
                if (button.isActiveAndEnabled && UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == buttonObj)
                {
                    // Button is highlighted, make accent line pulse
                    float pulse = Mathf.PingPong(Time.time * 2f, 0.5f) + 0.5f;
                    accentImage.color = Color.Lerp(BF2042Colors.Blue, BF2042Colors.White, pulse);
                    
                    // Make accent line wider
                    RectTransform accentRect = accentLine.GetComponent<RectTransform>();
                    accentRect.sizeDelta = new Vector2(5, 0);
                }
                else
                {
                    // Reset to default
                    accentImage.color = BF2042Colors.Blue;
                    
                    // Reset width
                    RectTransform accentRect = accentLine.GetComponent<RectTransform>();
                    accentRect.sizeDelta = new Vector2(3, 0);
                }
            }
        }
        
        [HarmonyPatch("PlayMusic")]
        [HarmonyPrefix]
        public static bool PlayMusic_Prefix(MainMenu __instance)
        {
            // Replace with BF2042 music if available, otherwise use original
            if (bf2042ThemeMusic != null)
            {
                bf2042ThemeMusic.Play();
                return false; // Skip original method
            }
            
            return true; // Run original method
        }
        
        // Helper method to convert Unity UI Text to TextMeshPro
        private static TextMeshProUGUI ConvertTextToTMP(Text originalText, Color color, float fontSize, FontStyles fontStyle)
        {
            if (originalText == null) return null;
            
            GameObject textObj = originalText.gameObject;
            
            // Cache original properties
            string originalString = originalText.text;
            TextAnchor originalAlignment = originalText.alignment;
            RectTransform rectTransform = textObj.GetComponent<RectTransform>();
            Vector2 anchorMin = rectTransform.anchorMin;
            Vector2 anchorMax = rectTransform.anchorMax;
            Vector2 offsetMin = rectTransform.offsetMin;
            Vector2 offsetMax = rectTransform.offsetMax;
            
            // Destroy original Text component
            UnityEngine.Object.Destroy(originalText);
            
            // Add TextMeshPro component
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = originalString;
            tmpText.color = color;
            tmpText.fontSize = fontSize;
            tmpText.fontStyle = fontStyle;
            
            // Convert alignment
            switch (originalAlignment)
            {
                case TextAnchor.UpperLeft:
                    tmpText.alignment = TextAlignmentOptions.TopLeft;
                    break;
                case TextAnchor.UpperCenter:
                    tmpText.alignment = TextAlignmentOptions.Top;
                    break;
                case TextAnchor.UpperRight:
                    tmpText.alignment = TextAlignmentOptions.TopRight;
                    break;
                case TextAnchor.MiddleLeft:
                    tmpText.alignment = TextAlignmentOptions.MidlineLeft;
                    break;
                case TextAnchor.MiddleCenter:
                    tmpText.alignment = TextAlignmentOptions.Center;
                    break;
                case TextAnchor.MiddleRight:
                    tmpText.alignment = TextAlignmentOptions.MidlineRight;
                    break;
                case TextAnchor.LowerLeft:
                    tmpText.alignment = TextAlignmentOptions.BottomLeft;
                    break;
                case TextAnchor.LowerCenter:
                    tmpText.alignment = TextAlignmentOptions.Bottom;
                    break;
                case TextAnchor.LowerRight:
                    tmpText.alignment = TextAlignmentOptions.BottomRight;
                    break;
            }
            
            // Restore the rect transform properties
            rectTransform = tmpText.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            
            return tmpText;
        }
    }
} 
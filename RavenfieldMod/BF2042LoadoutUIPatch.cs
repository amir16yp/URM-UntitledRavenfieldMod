using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using UnityEngine.EventSystems;

namespace URM
{
    [HarmonyPatch(typeof(LoadoutUi))]
    public class BF2042LoadoutUIPatch
    {
        // Track if we've applied UI changes
        private static bool uiModified = false;
        
        // Store original styles for reversion if needed
        private static Dictionary<Text, TextStyle> originalTextStyles = new Dictionary<Text, TextStyle>();
        
        private struct TextStyle
        {
            public Font font;
            public FontStyle fontStyle;
            public int fontSize;
            public Color color;
        }
        
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(LoadoutUi __instance)
        {
            // Apply BF2042 styling after the UI has been initialized
            MelonCoroutines.Start(ApplyBF2042Style(__instance));
        }
        
        private static System.Collections.IEnumerator ApplyBF2042Style(LoadoutUi ui)
        {
            // Wait a frame to ensure all UI elements are loaded
            yield return null;
            
            if (uiModified)
                yield break;
                
            uiModified = true;
            
            MelonLogger.Msg("Applying Battlefield 2042 style to Loadout UI");
            
            // Style the main UI components
            StyleLoadoutContainer(ui);
            StyleWeaponButtons(ui);
            StyleDeployButton(ui);
            StyleTacticsButton(ui);
            StyleWeaponSelectionUI(ui);
            
            // Log completion
            MelonLogger.Msg("Battlefield 2042 Loadout UI styling complete");
        }
        
        private static void StyleLoadoutContainer(LoadoutUi ui)
        {
            if (ui.loadoutContainer != null)
            {
                // Style the container background
                Image[] containerImages = ui.loadoutContainer.GetComponentsInChildren<Image>(true);
                foreach (Image img in containerImages)
                {
                    // Skip buttons and weapon images
                    if (img.transform.parent != null && 
                        (img.transform.parent.name.Contains("Button") || img.name == "Image"))
                        continue;
                        
                    // Apply dark background to panels
                    img.color = BF2042Colors.Background;
                }
                
                // Add a stylish header
                AddBF2042Header(ui.loadoutContainer.gameObject, "LOADOUT");
            }
            
            // Style minimap container if it exists
            if (ui.minimapContainer != null)
            {
                Image[] minimapImages = ui.minimapContainer.GetComponentsInChildren<Image>(true);
                foreach (Image img in minimapImages)
                {
                    // Only style the container background, not the actual map
                    if (img.transform.name.Contains("Container") || img.transform.name.Contains("Background"))
                    {
                        img.color = BF2042Colors.Background;
                    }
                }
                
                // Add a stylish header to the minimap
                AddBF2042Header(ui.minimapContainer.gameObject, "DEPLOYMENT");
            }
        }
        
        private static void StyleWeaponButtons(LoadoutUi ui)
        {
            // Style all weapon selection buttons
            StyleWeaponButton(ui.primaryButton);
            StyleWeaponButton(ui.secondaryButton);
            StyleWeaponButton(ui.gear1Button);
            StyleWeaponButton(ui.gear2Button);
            StyleWeaponButton(ui.gear3Button);
            StyleWeaponButton(ui.largeGear2Button);
        }
        
        private static void StyleWeaponButton(RectTransform buttonRect)
        {
            if (buttonRect == null)
                return;
                
            // Style the button itself
            Button button = buttonRect.GetComponent<Button>();
            if (button != null)
            {
                // Change the button colors
                ColorBlock colors = button.colors;
                colors.normalColor = BF2042Colors.Background;
                colors.highlightedColor = BF2042Colors.DarkBlue;
                colors.pressedColor = BF2042Colors.Blue;
                colors.selectedColor = BF2042Colors.DarkBlue;
                button.colors = colors;
                
                // Add a glow effect when highlighted
                AddButtonHoverEffect(button.gameObject);
            }
            
            // Style the weapon name text
            Text weaponText = buttonRect.GetComponentInChildren<Text>();
            if (weaponText != null)
            {
                StoreTextStyle(weaponText);
                
                weaponText.fontSize += 2;
                weaponText.fontStyle = FontStyle.Bold;
                weaponText.color = BF2042Colors.White;
                
                // Add outline to text
                Outline outline = weaponText.gameObject.GetComponent<Outline>() ?? weaponText.gameObject.AddComponent<Outline>();
                outline.effectColor = BF2042Colors.Black;
                outline.effectDistance = new Vector2(1f, 1f);
            }
            
            // Style weapon image
            Transform imageTransform = buttonRect.Find("Image");
            if (imageTransform != null)
            {
                Image weaponImage = imageTransform.GetComponent<Image>();
                if (weaponImage != null)
                {
                    // Add a slight blue tint to weapon images
                    weaponImage.color = new Color(0.9f, 0.95f, 1f, 1f);
                    
                    // Add outline to weapon image
                    Outline outline = weaponImage.gameObject.GetComponent<Outline>() ?? weaponImage.gameObject.AddComponent<Outline>();
                    outline.effectColor = BF2042Colors.Blue;
                    outline.effectDistance = new Vector2(2f, 2f);
                }
            }
            
            // Add a segmented background
            AddSegmentedBackground(buttonRect.gameObject);
        }
        
        private static void StyleDeployButton(LoadoutUi ui)
        {
            if (ui.deployButton != null)
            {
                // Style the deploy button
                ColorBlock colors = ui.deployButton.colors;
                colors.normalColor = BF2042Colors.DarkBlue;
                colors.highlightedColor = BF2042Colors.Blue;
                colors.pressedColor = Color.white;
                ui.deployButton.colors = colors;
                
                // Add a glow effect to the deploy button
                AddButtonGlowEffect(ui.deployButton.gameObject, BF2042Colors.Blue);
                
                // Style the text
                if (ui.deployText != null)
                {
                    StoreTextStyle(ui.deployText);
                    
                    ui.deployText.fontSize += 4;
                    ui.deployText.fontStyle = FontStyle.Bold;
                    ui.deployText.color = BF2042Colors.White;
                    
                    // Add outline for better visibility
                    Outline outline = ui.deployText.gameObject.GetComponent<Outline>() ?? ui.deployText.gameObject.AddComponent<Outline>();
                    outline.effectColor = BF2042Colors.Black;
                    outline.effectDistance = new Vector2(1.5f, 1.5f);
                }
                
                // Modify the button appearance
                Image buttonImage = ui.deployButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = BF2042Colors.DarkBlue;
                }
            }
        }
        
        private static void StyleTacticsButton(LoadoutUi ui)
        {
            if (ui.tacticsButton != null)
            {
                // Style the tactics button
                ColorBlock colors = ui.tacticsButton.colors;
                colors.normalColor = BF2042Colors.DarkBlue;
                colors.highlightedColor = BF2042Colors.Blue;
                colors.pressedColor = Color.white;
                ui.tacticsButton.colors = colors;
                
                // Add a glow effect
                AddButtonGlowEffect(ui.tacticsButton.gameObject, BF2042Colors.Blue);
                
                // Style the button text
                Text tacticsText = ui.tacticsButton.GetComponentInChildren<Text>();
                if (tacticsText != null)
                {
                    StoreTextStyle(tacticsText);
                    
                    tacticsText.fontSize += 4;
                    tacticsText.fontStyle = FontStyle.Bold;
                    tacticsText.color = BF2042Colors.White;
                    
                    // Add outline for better visibility
                    Outline outline = tacticsText.gameObject.GetComponent<Outline>() ?? tacticsText.gameObject.AddComponent<Outline>();
                    outline.effectColor = BF2042Colors.Black;
                    outline.effectDistance = new Vector2(1.5f, 1.5f);
                }
                
                // Modify the button appearance
                Image buttonImage = ui.tacticsButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = BF2042Colors.DarkBlue;
                }
            }
        }
        
        private static void StyleWeaponSelectionUI(LoadoutUi ui)
        {
            // Style the weapon selection panels
            StyleWeaponSelectionPanel(ui.primaryWeaponSelection);
            StyleWeaponSelectionPanel(ui.secondaryWeaponSelection);
            StyleWeaponSelectionPanel(ui.gearWeaponSelection);
        }
        
        private static void StyleWeaponSelectionPanel(WeaponSelectionUi selectionUi)
        {
            if (selectionUi == null)
                return;
                
            // Find and style the panel background
            Image[] panelImages = selectionUi.GetComponentsInChildren<Image>(true);
            foreach (Image img in panelImages)
            {
                // Skip weapon images and buttons
                if (img.transform.parent != null && 
                    (img.transform.parent.name.Contains("Button") || img.name == "WeaponImage"))
                    continue;
                    
                // Apply dark background to panels
                img.color = BF2042Colors.Background;
            }
            
            // Style weapon item buttons
            Button[] weaponButtons = selectionUi.GetComponentsInChildren<Button>(true);
            foreach (Button button in weaponButtons)
            {
                // Skip the cancel button
                if (button.name.Contains("Cancel") || button.name.Contains("Close"))
                    continue;
                    
                // Style the button
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.15f, 0.2f, 0.25f, 0.8f);
                colors.highlightedColor = BF2042Colors.DarkBlue;
                colors.pressedColor = BF2042Colors.Blue;
                colors.selectedColor = BF2042Colors.Blue;
                button.colors = colors;
                
                // Style texts in the button
                Text[] texts = button.GetComponentsInChildren<Text>(true);
                foreach (Text text in texts)
                {
                    StoreTextStyle(text);
                    
                    text.fontSize += 2;
                    text.fontStyle = FontStyle.Bold;
                    text.color = BF2042Colors.White;
                    
                    // Add outline for better visibility
                    Outline outline = text.gameObject.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
                    outline.effectColor = BF2042Colors.Black;
                    outline.effectDistance = new Vector2(1f, 1f);
                }
                
                // Add hover effect
                AddButtonHoverEffect(button.gameObject);
            }
            
            // Style the cancel button
            Button cancelButton = selectionUi.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name.Contains("Cancel") || b.name.Contains("Close"));
                
            if (cancelButton != null)
            {
                // Style the button
                ColorBlock colors = cancelButton.colors;
                colors.normalColor = BF2042Colors.Red;
                colors.highlightedColor = BF2042Colors.Red.Lighter(0.2f);
                colors.pressedColor = Color.white;
                cancelButton.colors = colors;
                
                // Style text
                Text cancelText = cancelButton.GetComponentInChildren<Text>();
                if (cancelText != null)
                {
                    StoreTextStyle(cancelText);
                    
                    cancelText.fontSize += 2;
                    cancelText.fontStyle = FontStyle.Bold;
                    cancelText.color = BF2042Colors.White;
                    
                    // Add outline
                    Outline outline = cancelText.gameObject.GetComponent<Outline>() ?? cancelText.gameObject.AddComponent<Outline>();
                    outline.effectColor = BF2042Colors.Black;
                    outline.effectDistance = new Vector2(1f, 1f);
                }
            }
            
            // Add a stylish header
            AddBF2042Header(selectionUi.gameObject, "SELECT WEAPON");
        }
        
        // Helper method to store original text style for potential restoration
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
        
        // Create a BF2042-style header
        private static void AddBF2042Header(GameObject parent, string headerText)
        {
            // Check if the header already exists
            if (parent.transform.Find("BF2042Header") != null)
                return;
                
            // Create the header GameObject
            GameObject headerObj = new GameObject("BF2042Header");
            headerObj.transform.SetParent(parent.transform, false);
            
            // Add RectTransform
            RectTransform rectTransform = headerObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.sizeDelta = new Vector2(0, 30);
            rectTransform.anchoredPosition = new Vector2(0, 0);
            
            // Add background image
            Image bgImage = headerObj.AddComponent<Image>();
            bgImage.color = BF2042Colors.DarkBlue;
            
            // Add header text
            GameObject textObj = new GameObject("HeaderText");
            textObj.transform.SetParent(headerObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);
            
            Text text = textObj.AddComponent<Text>();
            text.text = headerText;
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.color = BF2042Colors.White;
            text.alignment = TextAnchor.MiddleLeft;
            
            // Add outline
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = BF2042Colors.Black;
            outline.effectDistance = new Vector2(1, 1);
            
            // Add a blue line accent
            GameObject lineObj = new GameObject("AccentLine");
            lineObj.transform.SetParent(headerObj.transform, false);
            
            RectTransform lineRect = lineObj.AddComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0, 0);
            lineRect.anchorMax = new Vector2(1, 0);
            lineRect.pivot = new Vector2(0.5f, 0);
            lineRect.sizeDelta = new Vector2(0, 2);
            lineRect.anchoredPosition = Vector2.zero;
            
            Image lineImage = lineObj.AddComponent<Image>();
            lineImage.color = BF2042Colors.Blue;
        }
        
        // Add a segmented background to buttons
        private static void AddSegmentedBackground(GameObject buttonObj)
        {
            // Check if already has segments
            if (buttonObj.transform.Find("SegmentBackground") != null)
                return;
                
            // Create segment container
            GameObject segmentObj = new GameObject("SegmentBackground");
            segmentObj.transform.SetParent(buttonObj.transform, false);
            segmentObj.transform.SetSiblingIndex(0);  // Put at the back
            
            RectTransform rectTransform = segmentObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Add segments
            for (int i = 0; i < 3; i++)
            {
                GameObject line = new GameObject($"Segment_{i}");
                line.transform.SetParent(segmentObj.transform, false);
                
                RectTransform lineRect = line.AddComponent<RectTransform>();
                float width = 1f / 3f;
                lineRect.anchorMin = new Vector2(i * width, 0);
                lineRect.anchorMax = new Vector2((i + 1) * width, 1);
                lineRect.offsetMin = new Vector2(1, 1);
                lineRect.offsetMax = new Vector2(-1, -1);
                
                Image lineImage = line.AddComponent<Image>();
                lineImage.color = BF2042Colors.SegmentColor;
            }
        }
        
        // Add hover effect to buttons
        private static void AddButtonHoverEffect(GameObject buttonObj)
        {
            // Add a custom hover behavior via MonoBehaviour
            ButtonHoverEffect hoverEffect = buttonObj.GetComponent<ButtonHoverEffect>();
            if (hoverEffect == null)
            {
                hoverEffect = buttonObj.AddComponent<ButtonHoverEffect>();
                hoverEffect.hoverColor = BF2042Colors.Blue;
                hoverEffect.normalColor = BF2042Colors.Background;
                hoverEffect.outlineWidth = 2f;
            }
        }
        
        // Add glow effect to important buttons
        private static void AddButtonGlowEffect(GameObject buttonObj, Color glowColor)
        {
            // Add outline for glow effect
            Outline outline = buttonObj.GetComponent<Outline>();
            if (outline == null)
            {
                outline = buttonObj.AddComponent<Outline>();
            }
            
            outline.effectColor = glowColor;
            outline.effectDistance = new Vector2(2f, 2f);
            
            // Add pulse effect
            ButtonPulseEffect pulseEffect = buttonObj.GetComponent<ButtonPulseEffect>();
            if (pulseEffect == null)
            {
                pulseEffect = buttonObj.AddComponent<ButtonPulseEffect>();
                pulseEffect.glowColor = glowColor;
                pulseEffect.pulseSpeed = 1.5f;
                pulseEffect.minGlow = 0.5f;
                pulseEffect.maxGlow = 1.0f;
            }
        }
    }
    
    // Custom component for button hover effects
    public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Color hoverColor = BF2042Colors.Blue;
        public Color normalColor = BF2042Colors.Background;
        public float outlineWidth = 2f;
        
        private Button button;
        private Outline outline;
        
        private void Awake()
        {
            button = GetComponent<Button>();
            outline = GetComponent<Outline>();
            
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
                outline.effectColor = hoverColor;
                outline.effectDistance = new Vector2(0, 0); // Start invisible
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (outline != null)
            {
                outline.effectDistance = new Vector2(outlineWidth, outlineWidth);
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            if (outline != null)
            {
                outline.effectDistance = Vector2.zero;
            }
        }
    }
    
    // Custom component for button pulse effects
    public class ButtonPulseEffect : MonoBehaviour
    {
        public Color glowColor = BF2042Colors.Blue;
        public float pulseSpeed = 1.5f;
        public float minGlow = 0.5f;
        public float maxGlow = 1.0f;
        
        private Outline outline;
        private float time = 0f;
        
        private void Awake()
        {
            outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
                outline.effectColor = glowColor;
            }
        }
        
        private void Update()
        {
            if (outline != null)
            {
                time += Time.deltaTime * pulseSpeed;
                float pulse = Mathf.Lerp(minGlow, maxGlow, (Mathf.Sin(time) + 1f) / 2f);
                
                Color newColor = new Color(
                    glowColor.r, 
                    glowColor.g, 
                    glowColor.b, 
                    pulse * glowColor.a
                );
                
                outline.effectColor = newColor;
            }
        }
    }
} 
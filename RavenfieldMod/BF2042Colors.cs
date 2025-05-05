using UnityEngine;

namespace URM
{
    /// <summary>
    /// Shared color palette for Battlefield 2042 UI styling
    /// Used across multiple UI patch components to ensure consistency
    /// </summary>
    public static class BF2042Colors
    {
        // Primary colors
        public static readonly Color Blue = new Color(0.05f, 0.85f, 0.95f, 1f);            // Cyan blue for highlights and accents
        public static readonly Color Red = new Color(1f, 0.2f, 0.2f, 1f);                  // Red for warnings and damage
        public static readonly Color White = new Color(0.9f, 0.95f, 1f, 1f);               // Off-white for text
        
        // Background colors
        public static readonly Color Background = new Color(0.12f, 0.15f, 0.18f, 0.85f);   // Dark background for panels
        public static readonly Color DarkBlue = new Color(0.1f, 0.3f, 0.4f, 0.8f);         // Dark blue for UI elements
        
        // Accent colors
        public static readonly Color Green = new Color(0.2f, 0.9f, 0.7f, 1f);              // Accent green for healing/positive elements
        public static readonly Color Yellow = new Color(1f, 0.9f, 0.2f, 1f);               // Accent yellow for warnings/notifications
        public static readonly Color Orange = new Color(1f, 0.5f, 0.1f, 1f);               // Accent orange for moderate warnings
        
        // Utility colors
        public static readonly Color Black = new Color(0.05f, 0.05f, 0.05f, 1f);           // Deep black for outlines
        public static readonly Color Gray = new Color(0.7f, 0.7f, 0.75f, 1f);              // Neutral gray
        
        // Segment colors
        public static readonly Color SegmentColor = new Color(0.2f, 0.25f, 0.3f, 0.3f);    // Color for UI segmentation
        
        // Team colors
        public static readonly Color TeamBlue = new Color(0.2f, 0.4f, 0.9f, 1f);           // Blue team color
        public static readonly Color TeamRed = new Color(0.9f, 0.25f, 0.2f, 1f);           // Red team color
        
        /// <summary>
        /// Create a transparent version of a color with specified alpha
        /// </summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
        
        /// <summary>
        /// Create a darker version of a color
        /// </summary>
        public static Color Darker(this Color color, float amount = 0.2f)
        {
            return new Color(
                Mathf.Max(0, color.r - amount),
                Mathf.Max(0, color.g - amount),
                Mathf.Max(0, color.b - amount),
                color.a
            );
        }
        
        /// <summary>
        /// Create a lighter version of a color
        /// </summary>
        public static Color Lighter(this Color color, float amount = 0.2f)
        {
            return new Color(
                Mathf.Min(1, color.r + amount),
                Mathf.Min(1, color.g + amount),
                Mathf.Min(1, color.b + amount),
                color.a
            );
        }
    }
} 
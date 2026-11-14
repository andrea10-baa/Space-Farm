using UnityEngine;

namespace SpaceFarm
{
    // Space Farm art bible: warm Martian soil, cool technology and one mint action accent.
    public static class SpaceFarmPalette
    {
        public static readonly Color Background = Hex("#111923");
        public static readonly Color Panel = Hex("#1C2833");
        public static readonly Color PanelRaised = Hex("#273846");
        public static readonly Color Soil = Hex("#8C5544");
        public static readonly Color SoilLight = Hex("#9B624A");
        public static readonly Color Fog = Hex("#202B35");
        public static readonly Color FogOverlay = Hex("#17212DF2");
        public static readonly Color Iron = Hex("#E6A46A");
        public static readonly Color Ice = Hex("#8ED6E6");
        public static readonly Color Rover = Hex("#E8E0C9");
        public static readonly Color Oxygen = Hex("#77D7AF");
        public static readonly Color Greenhouse = Hex("#8CCB7B");
        public static readonly Color Danger = Hex("#EF806C");
        public static readonly Color DamagedTint = Hex("#FF9C8B");
        public static readonly Color Text = Hex("#F3EEE2");
        public static readonly Color MutedText = Hex("#B5C3C6");
        public static readonly Color Button = Hex("#2F7D74");
        public static readonly Color ButtonHover = Hex("#3F9D90");

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out Color color);
            return color;
        }
    }
}

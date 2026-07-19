using UnityEngine;

namespace EndangeredAR.UI
{
    public static class SensenDesignTokens
    {
        public static readonly Color Forest950 = Hex("061411");
        public static readonly Color Forest900 = Hex("071D16");
        public static readonly Color Forest800 = Hex("0D2A20");
        public static readonly Color Moss650 = Hex("2E5C40");
        public static readonly Color Leaf500 = Hex("5EB873");
        public static readonly Color Leaf300 = Hex("C7E6C7");
        public static readonly Color Cream100 = Hex("EBF2DB");
        public static readonly Color Cream50 = Hex("F5F8E9");
        public static readonly Color Gold500 = Hex("FFE06B");
        public static readonly Color Sky500 = Hex("458ED4");
        public static readonly Color DangerSoft = Hex("D96B5D");

        public const int Display56 = 56;
        public const int Hero54 = 54;
        public const int Section46 = 46;
        public const int Body30 = 30;
        public const int Caption24 = 24;
        public const int Meta21 = 21;

        public const float Space8 = 8f;
        public const float Space16 = 16f;
        public const float Space24 = 24f;
        public const float Space32 = 32f;
        public const float Space48 = 48f;
        public const float ScreenMargin64 = 64f;

        public const float Radius8 = 8f;
        public const float Radius12 = 12f;
        public const float Radius20 = 20f;

        public const float ButtonHeight74 = 74f;
        public const float ButtonHeight82 = 82f;
        public const float ButtonHeight88 = 88f;
        public const float PrimaryButtonHeight96 = 96f;
        public const float CardWidth820 = 820f;

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString($"#{hex}", out var color))
            {
                return color;
            }

            return Color.magenta;
        }
    }
}

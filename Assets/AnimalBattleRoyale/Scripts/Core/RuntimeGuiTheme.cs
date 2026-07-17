using UnityEngine;

namespace AnimalBattleRoyale
{
    public static class RuntimeGuiTheme
    {
        private static Texture2D roundedTexture;
        private static GUIStyle roundedStyle;
        private static Texture2D circleTexture;
        private static Texture2D ringTexture;

        public static Texture2D CircleTexture
        {
            get
            {
                if (circleTexture == null) circleTexture = CreateCircleTexture(false);
                return circleTexture;
            }
        }

        public static Texture2D RingTexture
        {
            get
            {
                if (ringTexture == null) ringTexture = CreateCircleTexture(true);
                return ringTexture;
            }
        }

        public static void Ensure()
        {
            if (roundedTexture != null && roundedStyle != null) return;
            roundedTexture = CreateRoundedTexture();
            roundedStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = roundedTexture },
                border = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }

        public static void DrawRoundedRect(Rect rect, Color color)
        {
            Ensure();
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, roundedStyle);
            GUI.color = previous;
        }

        public static void DrawPanel(Rect rect, Color fill, Color border, float borderSize = 1f, bool shadow = true)
        {
            if (shadow)
            {
                DrawRoundedRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, fill.a * 0.34f));
            }
            DrawRoundedRect(rect, border);
            DrawRoundedRect(new Rect(rect.x + borderSize, rect.y + borderSize,
                rect.width - borderSize * 2f, rect.height - borderSize * 2f), fill);
        }

        /// <summary>Filled circle (or thin ring, when <paramref name="ring"/> is true) tinted via GUI.color.</summary>
        public static void DrawCircle(Rect rect, Color color, bool ring = false)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, ring ? RingTexture : CircleTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static Texture2D CreateCircleTexture(bool ring)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "RuntimeGuiRing" : "RuntimeGuiCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(normalized - 0.92f) * 24f)
                        : Mathf.Clamp01((1f - normalized) * 12f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateRoundedTexture()
        {
            const int size = 32;
            const float radius = 8f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeGuiRoundedRect",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            Vector2 halfSize = Vector2.one * (size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f) - halfSize;
                    Vector2 corner = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - (halfSize - Vector2.one * radius);
                    float outside = new Vector2(Mathf.Max(corner.x, 0f), Mathf.Max(corner.y, 0f)).magnitude - radius;
                    float alpha = Mathf.Clamp01(0.75f - outside);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

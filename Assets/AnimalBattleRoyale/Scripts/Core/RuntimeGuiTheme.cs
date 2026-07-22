using UnityEngine;

namespace AnimalBattleRoyale
{
    public static class RuntimeGuiTheme
    {
        private static Texture2D roundedTexture;
        private static GUIStyle roundedStyle;
        private static Texture2D circleTexture;
        private static Texture2D ringTexture;
        private static Texture2D pentagonTexture;
        private static Texture2D pentagonRingTexture;
        private static Texture2D heartTexture;
        private static Texture2D heartRingTexture;

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

        /// <summary>Filled, point-up regular pentagon — matches the badge shape used for the
        /// ability icon art, for spots (ammo/ability-2 placeholder) that need the same look
        /// without a dedicated image.</summary>
        public static Texture2D PentagonTexture
        {
            get
            {
                if (pentagonTexture == null) pentagonTexture = CreatePentagonTexture(false);
                return pentagonTexture;
            }
        }

        public static Texture2D PentagonRingTexture
        {
            get
            {
                if (pentagonRingTexture == null) pentagonRingTexture = CreatePentagonTexture(true);
                return pentagonRingTexture;
            }
        }

        /// <summary>Filled heart (or thin outline, when <paramref name="ring"/> is true) —
        /// used for the visual lives indicator, tint via GUI.color.</summary>
        public static Texture2D HeartTexture
        {
            get
            {
                if (heartTexture == null) heartTexture = CreateHeartTexture(false);
                return heartTexture;
            }
        }

        public static Texture2D HeartRingTexture
        {
            get
            {
                if (heartRingTexture == null) heartRingTexture = CreateHeartTexture(true);
                return heartRingTexture;
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

        // Point-up regular pentagon via a signed distance field (max of 5 outward half-plane
        // distances) — same construction style as CreateCircleTexture above, just with a
        // 5-sided distance function instead of a radial one.
        private static Texture2D CreatePentagonTexture(bool ring)
        {
            const int size = 96;
            const int sides = 5;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "RuntimeGuiPentagonRing" : "RuntimeGuiPentagon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float radius = size * 0.47f;
            float apothem = radius * Mathf.Cos(Mathf.PI / sides);
            const float startAngle = Mathf.PI / 2f; // vertex points up
            Vector2[] normals = new Vector2[sides];
            for (int k = 0; k < sides; k++)
            {
                float edgeAngle = startAngle + Mathf.PI / sides + k * (2f * Mathf.PI / sides);
                normals[k] = new Vector2(Mathf.Cos(edgeAngle), Mathf.Sin(edgeAngle));
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture row 0 is the bottom in Unity's convention but the top when drawn
                    // via GUI.DrawTexture — flip Y so the vertex reads as "up" on screen.
                    Vector2 p = new Vector2(x - center.x, center.y - y);
                    float distance = float.NegativeInfinity;
                    for (int k = 0; k < sides; k++)
                        distance = Mathf.Max(distance, Vector2.Dot(p, normals[k]) - apothem);

                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(distance + apothem * 0.09f) * 0.4f)
                        : Mathf.Clamp01(0.5f - distance * 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Heart shape built from two circle SDFs (the upper lobes) unioned with a triangle
        // SDF (the lower point) via a min() — same half-plane technique as the pentagon for
        // the triangle, just with 3 explicit vertices instead of N evenly-spaced ones since a
        // heart's point isn't a regular polygon.
        private static Texture2D CreateHeartTexture(bool ring)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = ring ? "RuntimeGuiHeartRing" : "RuntimeGuiHeart",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float unit = size * 0.5f;

            Vector2 lobeLeft = new Vector2(-0.26f, -0.2f) * unit;
            Vector2 lobeRight = new Vector2(0.26f, -0.2f) * unit;
            float lobeRadius = 0.34f * unit;

            Vector2 triLeft = new Vector2(-0.5f, -0.2f) * unit;
            Vector2 triApex = new Vector2(0f, 0.55f) * unit;
            Vector2 triRight = new Vector2(0.5f, -0.2f) * unit;
            Vector2[] triVertices = { triRight, triApex, triLeft };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture row 0 is the bottom in Unity's convention but the top when drawn
                    // via GUI.DrawTexture — flip Y so the point reads as "down" on screen.
                    Vector2 p = new Vector2(x - center.x, center.y - y);

                    float distance = Vector2.Distance(p, lobeLeft) - lobeRadius;
                    distance = Mathf.Min(distance, Vector2.Distance(p, lobeRight) - lobeRadius);
                    distance = Mathf.Min(distance, ConvexPolygonSdf(p, triVertices));

                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(distance + unit * 0.09f) * 0.4f)
                        : Mathf.Clamp01(0.5f - distance * 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Signed distance to a convex polygon given in counter-clockwise winding order — for
        // a CCW edge vector (dx,dy), (dy,-dx) is the outward normal, so the max of each edge's
        // signed distance along its own outward normal gives the polygon's SDF.
        private static float ConvexPolygonSdf(Vector2 p, Vector2[] verticesCcw)
        {
            float distance = float.NegativeInfinity;
            int count = verticesCcw.Length;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = verticesCcw[i];
                Vector2 b = verticesCcw[(i + 1) % count];
                Vector2 edge = b - a;
                Vector2 outwardNormal = new Vector2(edge.y, -edge.x).normalized;
                distance = Mathf.Max(distance, Vector2.Dot(p - a, outwardNormal));
            }
            return distance;
        }
    }
}

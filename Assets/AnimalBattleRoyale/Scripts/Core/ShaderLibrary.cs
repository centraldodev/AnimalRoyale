using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>
    /// Caches <see cref="Shader.Find"/> results. Shader.Find walks every loaded
    /// shader on each call, so the procedural spawners (pickups, VFX, damage
    /// popups) that build materials at runtime were paying it repeatedly. The
    /// named accessors also centralise the URP-with-built-in fallback pattern,
    /// so there is a single place to keep the render pipeline in sync.
    /// </summary>
    public static class ShaderLibrary
    {
        private static readonly Dictionary<string, Shader> cache = new Dictionary<string, Shader>();

        public static Shader Find(string name)
        {
            if (!cache.TryGetValue(name, out Shader shader) || shader == null)
            {
                shader = Shader.Find(name);
                cache[name] = shader;
            }
            return shader;
        }

        /// <summary>Opaque lit surface: URP Lit, falling back to Built-in Standard.</summary>
        public static Shader Lit => Find("Universal Render Pipeline/Lit") ?? Find("Standard");

        /// <summary>Unlit/sprite surface for markers and lines: Sprites/Default, falling back to URP Unlit.</summary>
        public static Shader Sprite => Find("Sprites/Default") ?? Find("Universal Render Pipeline/Unlit");
    }
}

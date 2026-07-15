using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    /// <summary>Procedural cartoon flashes so melee attacks and abilities are readable without external assets.</summary>
    public sealed class AttackVfx : MonoBehaviour
    {
        private const int MaxPoolSize = 48;
        private static readonly Stack<AttackVfx> pool = new Stack<AttackVfx>();
        private static Material sharedLineMaterial;
        private LineRenderer line;
        private float startedAt;
        private float expiresAt;
        private float startWidth;
        private bool expanding;
        private Vector3 center;
        private float radius;
        private Color color;

        public static void CreateSlash(Vector3 position, Vector3 direction, Color color, float size)
        {
            AttackVfx vfx = CreateEffect("Slash", color, 0.26f);
            vfx.transform.position = position;
            vfx.ConfigureArc(position, direction, color, size);
        }

        public static void CreateBurst(Vector3 position, Color color, float radius)
        {
            AttackVfx vfx = CreateEffect("Burst", color, 0.38f);
            vfx.ConfigureRing(position, color, radius, true);
        }

        public static void CreateHitSpark(Vector3 position, Color color)
        {
            CreateBurst(position, color, 0.85f);
            CreateSlash(position, Vector3.forward, Color.white, 0.45f);
        }

        public static void CreatePower(AnimalType type, int slot, Vector3 position, Vector3 direction)
        {
            switch (type)
            {
                case AnimalType.Tiger:
                    CreateSlash(position + Vector3.up, direction, new Color(1f, 0.22f, 0.03f), 3.4f);
                    break;
                case AnimalType.Deer:
                    CreateSlash(position + Vector3.up, direction, new Color(0.9f, 0.68f, 0.3f), 3.1f);
                    break;
                case AnimalType.Horse:
                    CreateBurst(position, new Color(0.76f, 0.48f, 0.22f), 4.2f);
                    break;
                case AnimalType.Chicken:
                    CreateBurst(position + Vector3.up, new Color(1f, 0.84f, 0.24f), 4f);
                    break;
                case AnimalType.Dog:
                    CreateBurst(position, new Color(0.35f, 0.8f, 1f), 3.6f);
                    break;
                case AnimalType.Cat:
                    CreateSlash(position + Vector3.up, direction, new Color(0.75f, 0.42f, 1f), 2.8f);
                    break;
                case AnimalType.Penguin:
                    CreateSlash(position + Vector3.up * 0.5f, direction, new Color(0.42f, 0.9f, 1f), 3.4f);
                    break;
            }
        }

        private static AttackVfx CreateEffect(string effectName, Color color, float duration)
        {
            AttackVfx pending = null;
            while (pool.Count > 0 && pending == null) pending = pool.Pop();
            if (pending == null)
            {
                GameObject effect = new GameObject(effectName + "Vfx");
                pending = effect.AddComponent<AttackVfx>();
            }
            else
            {
                pending.gameObject.name = effectName + "Vfx";
                pending.gameObject.SetActive(true);
            }

            pending.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            pending.startedAt = Time.time;
            pending.expiresAt = Time.time + duration;
            pending.color = color;
            pending.expanding = false;
            if (pending.line != null) pending.line.enabled = true;
            return pending;
        }

        private void ConfigureArc(Vector3 position, Vector3 direction, Color newColor, float size)
        {
            color = newColor;
            line = CreateLine(10, newColor, 0.16f);
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, direction).normalized;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = Mathf.Lerp(-72f, 72f, i / (float)(line.positionCount - 1)) * Mathf.Deg2Rad;
                Vector3 point = position + direction * (Mathf.Cos(angle) * size) + right * (Mathf.Sin(angle) * size) + up * (Mathf.Sin(angle * 1.7f) * 0.32f + 0.18f);
                line.SetPosition(i, point);
            }
            startWidth = line.widthMultiplier;
        }

        private void ConfigureRing(Vector3 position, Color newColor, float newRadius, bool shouldExpand)
        {
            center = position;
            radius = newRadius;
            expanding = shouldExpand;
            color = newColor;
            line = CreateLine(28, newColor, shouldExpand ? 0.15f : 0.1f);
            startWidth = line.widthMultiplier;
            DrawRing(shouldExpand ? radius * 0.2f : radius);
        }

        private LineRenderer CreateLine(int count, Color newColor, float width)
        {
            LineRenderer newLine = line != null ? line : gameObject.AddComponent<LineRenderer>();
            line = newLine;
            newLine.useWorldSpace = true;
            newLine.loop = true;
            newLine.positionCount = count;
            newLine.widthMultiplier = width;
            newLine.numCapVertices = 3;
            newLine.numCornerVertices = 3;
            newLine.sharedMaterial = GetSharedLineMaterial();
            newLine.startColor = newColor;
            newLine.endColor = newColor;
            newLine.enabled = true;
            return newLine;
        }

        private void Update()
        {
            if (line == null) return;
            float progress = Mathf.InverseLerp(startedAt, expiresAt, Time.time);
            if (expanding) DrawRing(Mathf.Lerp(radius * 0.2f, radius, progress));
            Color faded = color;
            faded.a = Mathf.Lerp(1f, 0f, progress);
            line.startColor = faded;
            line.endColor = faded;
            line.widthMultiplier = Mathf.Lerp(startWidth, 0f, progress);
            if (Time.time >= expiresAt) Release();
        }

        private void DrawRing(float currentRadius)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * currentRadius, 0.08f, Mathf.Sin(angle) * currentRadius));
            }
        }

        private void Release()
        {
            if (line != null) line.enabled = false;
            if (pool.Count >= MaxPoolSize)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
            pool.Push(this);
        }

        private static Material GetSharedLineMaterial()
        {
            if (sharedLineMaterial != null) return sharedLineMaterial;
            Shader shader = ShaderLibrary.Sprite;
            sharedLineMaterial = new Material(shader)
            {
                name = "SharedCartoonAttackVfx",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedLineMaterial;
        }
    }
}

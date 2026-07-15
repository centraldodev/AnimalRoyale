using UnityEngine;

namespace AnimalBattleRoyale
{
    public readonly struct AnimalStats
    {
        public readonly string DisplayName;
        public readonly string[] AbilityNames;
        public readonly float MaxHealth;
        public readonly float MoveSpeed;
        public readonly float SprintSpeed;
        public readonly float JumpForce;
        public readonly float AttackDamage;
        public readonly float AttackRange;
        public readonly float AttackCooldown;
        public readonly float[] AbilityCooldowns;
        public readonly float ControllerRadius;
        public readonly float ControllerHeight;
        public readonly Vector3 VisualScale;
        public readonly Color MainColor;

        public AnimalStats(
            string displayName,
            string[] abilityNames,
            float maxHealth,
            float moveSpeed,
            float sprintSpeed,
            float jumpForce,
            float attackDamage,
            float attackRange,
            float attackCooldown,
            float[] abilityCooldowns,
            float controllerRadius,
            float controllerHeight,
            Vector3 visualScale,
            Color mainColor)
        {
            DisplayName = displayName;
            AbilityNames = abilityNames;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            SprintSpeed = sprintSpeed;
            JumpForce = jumpForce;
            AttackDamage = attackDamage;
            AttackRange = attackRange;
            AttackCooldown = attackCooldown;
            AbilityCooldowns = abilityCooldowns;
            ControllerRadius = controllerRadius;
            ControllerHeight = controllerHeight;
            VisualScale = visualScale;
            MainColor = mainColor;
        }
    }

    public static class AnimalDefinition
    {
        public static AnimalStats Get(AnimalType type)
        {
            return type switch
            {
                AnimalType.Tiger => new AnimalStats(
                    "Tigre", new[] { "Salto Predador", "DESATIVADO", "DESATIVADO" },
                    190f, 6.1f, 9.0f, 6.2f, 22.5f, 2.0f, 0.9f, new[] { 7f, 0f, 0f }, 0.62f, 1.45f,
                    Vector3.one * 0.78f, new Color(0.95f, 0.42f, 0.08f)),

                AnimalType.Deer => new AnimalStats(
                    "Cervo", new[] { "Investida de Chifres", "DESATIVADO", "DESATIVADO" },
                    175f, 6.35f, 9.2f, 7.0f, 19f, 1.9f, 0.78f, new[] { 8f, 0f, 0f }, 0.55f, 1.8f,
                    Vector3.one * 0.78f, new Color(0.62f, 0.36f, 0.16f)),

                AnimalType.Horse => new AnimalStats(
                    "Cavalo", new[] { "Galope Imparável", "DESATIVADO", "DESATIVADO" },
                    205f, 6.0f, 9.6f, 6.4f, 21f, 2.05f, 0.88f, new[] { 9f, 0f, 0f }, 0.64f, 1.65f,
                    Vector3.one * 0.95f, new Color(0.46f, 0.27f, 0.13f)),

                AnimalType.Chicken => new AnimalStats(
                    "Galinha", new[] { "Rajada de Penas", "DESATIVADO", "DESATIVADO" },
                    150f, 6.55f, 8.65f, 8.2f, 15.5f, 1.45f, 0.62f, new[] { 7f, 0f, 0f }, 0.38f, 1.05f,
                    Vector3.one * 2.05f, new Color(0.92f, 0.54f, 0.16f)),

                AnimalType.Dog => new AnimalStats(
                    "Cachorro", new[] { "Latido Protetor", "DESATIVADO", "DESATIVADO" },
                    180f, 6.45f, 8.9f, 7.2f, 18.5f, 1.75f, 0.72f, new[] { 8f, 0f, 0f }, 0.48f, 1.15f,
                    Vector3.one * 1.38f, new Color(0.66f, 0.45f, 0.24f)),

                AnimalType.Cat => new AnimalStats(
                    "Gato", new[] { "Sete Vidas", "DESATIVADO", "DESATIVADO" },
                    155f, 6.8f, 9.25f, 8.8f, 17f, 1.6f, 0.64f, new[] { 11f, 0f, 0f }, 0.4f, 1.05f,
                    Vector3.one * 2.45f, new Color(0.34f, 0.36f, 0.42f)),

                AnimalType.Penguin => new AnimalStats(
                    "Pinguim", new[] { "Deslize Glacial", "DESATIVADO", "DESATIVADO" },
                    185f, 5.85f, 8.25f, 6.3f, 20f, 1.7f, 0.8f, new[] { 8.5f, 0f, 0f }, 0.52f, 1.35f,
                    Vector3.one * 0.72f, new Color(0.14f, 0.24f, 0.32f)),

                _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}

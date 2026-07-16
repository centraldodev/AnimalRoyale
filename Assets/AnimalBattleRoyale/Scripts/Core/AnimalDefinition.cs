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
                    "Tigre", new[] { "Pulo Longo", "DESATIVADO", "DESATIVADO" },
                    100f, 6.1f, 9.0f, 6.2f, 22.5f, 2.0f, 0.9f, new[] { 7f, 0f, 0f }, 0.62f, 1.45f,
                    Vector3.one * 0.78f, new Color(0.95f, 0.42f, 0.08f)),

                AnimalType.Ant => new AnimalStats(
                    "Formiga", new[] { "Túnel ou Arremesso", "DESATIVADO", "DESATIVADO" },
                    100f, 6.7f, 9.4f, 7.4f, 16f, 1.6f, 0.7f, new[] { 8f, 0f, 0f }, 0.42f, 1.0f,
                    Vector3.one * 0.72f, new Color(0.86f, 0.36f, 0.14f)),

                AnimalType.Eagle => new AnimalStats(
                    "Águia", new[] { "Salto Planado", "DESATIVADO", "DESATIVADO" },
                    100f, 6.2f, 9.5f, 8.6f, 18f, 1.85f, 0.82f, new[] { 8f, 0f, 0f }, 0.46f, 1.3f,
                    Vector3.one * 0.8f, new Color(0.74f, 0.68f, 0.58f)),

                AnimalType.Monkey => new AnimalStats(
                    "Macaco", new[] { "Sequência de Cipós", "DESATIVADO", "DESATIVADO" },
                    100f, 6.5f, 9.2f, 8.0f, 19f, 1.75f, 0.76f, new[] { 8f, 0f, 0f }, 0.46f, 1.2f,
                    Vector3.one * 0.76f, new Color(0.55f, 0.36f, 0.2f)),

                _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}

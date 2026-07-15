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
                AnimalType.Ant => new AnimalStats(
                    "Formiga", new[] { "Arremesso da Colônia", "DESATIVADO", "DESATIVADO" },
                    180f, 6.0f, 8.0f, 6.0f, 18f, 1.55f, 0.72f, new[] { 12f, 14f, 13f }, 0.38f, 0.95f,
                    new Vector3(0.72f, 0.72f, 0.72f), new Color(0.34f, 0.12f, 0.06f)),

                AnimalType.Monkey => new AnimalStats(
                    "Macaco", new[] { "Cipó da Selva", "DESATIVADO", "DESATIVADO" },
                    175f, 6.15f, 8.4f, 7.6f, 19f, 1.7f, 0.75f, new[] { 0.45f, 8f, 13f }, 0.48f, 1.65f,
                    new Vector3(0.9f, 0.9f, 0.9f), new Color(0.35f, 0.22f, 0.12f)),

                AnimalType.Tiger => new AnimalStats(
                    "Tigre", new[] { "Salto Predador", "DESATIVADO", "DESATIVADO" },
                    190f, 6.1f, 9.0f, 6.2f, 22.5f, 2.0f, 0.9f, new[] { 7f, 6f, 12f }, 0.62f, 1.35f,
                    new Vector3(1.15f, 1.15f, 1.15f), new Color(0.95f, 0.42f, 0.08f)),

                AnimalType.Eagle => new AnimalStats(
                    "Águia", new[] { "Razante de Garras", "DESATIVADO", "DESATIVADO" },
                    165f, 6.45f, 8.5f, 6.7f, 16.5f, 1.8f, 0.66f, new[] { 6f, 9f, 3f }, 0.42f, 1.05f,
                    new Vector3(0.85f, 0.85f, 0.85f), new Color(0.42f, 0.30f, 0.18f)),

                _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}

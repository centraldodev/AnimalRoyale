using System;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class Health : MonoBehaviour
    {
        public event Action<Health, ThirdPersonAnimalController> Died;
        public event Action<float, float> HealthChanged;

        public float MaxHealth { get; private set; } = 100f;
        public float CurrentHealth { get; private set; } = 100f;
        public bool IsDead { get; private set; }
        public ThirdPersonAnimalController Owner { get; private set; }

        public void Initialize(float maxHealth, ThirdPersonAnimalController owner)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            Owner = owner;
            IsDead = false;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void ReconfigureMaxHealth(float maxHealth, bool refill)
        {
            float normalized = MaxHealth > 0f ? CurrentHealth / MaxHealth : 1f;
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = refill ? MaxHealth : Mathf.Clamp(MaxHealth * normalized, 1f, MaxHealth);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void TakeDamage(float amount, ThirdPersonAnimalController attacker = null)
        {
            if (IsDead || amount <= 0f) return;

            if (Owner != null) amount = Owner.ModifyIncomingDamage(amount);

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f)
            {
                Die(attacker);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private void Die(ThirdPersonAnimalController attacker)
        {
            if (IsDead) return;
            IsDead = true;
            Died?.Invoke(this, attacker);

            if (Owner != null)
            {
                Owner.SetDefeated();
            }
        }
    }
}

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
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            if (online != null && online.IsClientOnly && Owner != null && !Owner.IsLocalPlayer) return;

            if (Owner != null) amount = Owner.ModifyIncomingDamage(amount);
            if (amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f)
            {
                CombatFeedback.PlayPlayerDeath(transform.position);
                Die(attacker);
            }
            else if (attacker != null) CombatFeedback.PlayAnimalHit(transform.position);
            else CombatFeedback.PlayPlayerHit(transform.position);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void ApplyReplicatedState(float currentHealth)
        {
            CurrentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
            IsDead = CurrentHealth <= 0f;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// Lowers health to an environmental ceiling without combat resistance or hit feedback.
        /// This lets continuous hazards animate the health bar smoothly without creating one
        /// impact effect per frame. It never restores health.
        /// </summary>
        public void ApplyEnvironmentalHealthCeiling(float healthCeiling)
        {
            if (IsDead) return;
            if (BattleRoyaleManager.Instance != null && !BattleRoyaleManager.Instance.CombatEnabled) return;

            float targetHealth = Mathf.Clamp(healthCeiling, 0f, MaxHealth);
            if (targetHealth >= CurrentHealth) return;

            CurrentHealth = targetHealth;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth > 0f) return;

            CombatFeedback.PlayPlayerDeath(transform.position);
            Die(null);
        }

        private void Die(ThirdPersonAnimalController attacker)
        {
            if (IsDead) return;
            IsDead = true;
            Died?.Invoke(this, attacker);

            // The BattleRoyaleManager decides whether this death spends a life (respawn)
            // or eliminates the fighter. Only fall back to immediate removal when no
            // manager is present (e.g. isolated test scenes).
            if (Owner != null && BattleRoyaleManager.Instance == null)
            {
                Owner.SetDefeated();
            }
        }
    }
}

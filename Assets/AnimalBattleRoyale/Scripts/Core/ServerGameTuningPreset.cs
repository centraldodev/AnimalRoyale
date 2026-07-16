using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class ServerGameTuningPreset : ScriptableObject
    {
        public float tigerLeapDuration = ServerGameTuning.DefaultTigerLeapDuration;
        public float tigerLeapSpeed = ServerGameTuning.DefaultTigerLeapSpeed;
        public float tigerLeapUpSpeed = ServerGameTuning.DefaultTigerLeapUpSpeed;
        public float tigerLeapHitRadius = ServerGameTuning.DefaultTigerLeapHitRadius;
        public float tigerLeapDamage = ServerGameTuning.DefaultTigerLeapDamage;
        public float tigerLeapKnockback = ServerGameTuning.DefaultTigerLeapKnockback;

        public float projectileSpeed = ServerGameTuning.DefaultProjectileSpeed;
        public float projectileRangeSeconds = ServerGameTuning.DefaultProjectileRangeSeconds;
        public float projectileGravityMultiplier = ServerGameTuning.DefaultProjectileGravityMultiplier;
        public float projectileLiftMultiplier = ServerGameTuning.DefaultProjectileLiftMultiplier;
        public float projectileDamageMultiplier = ServerGameTuning.DefaultProjectileDamageMultiplier;
        public float projectileRadiusMultiplier = ServerGameTuning.DefaultProjectileRadiusMultiplier;
        public float rangedShotsPerSecond = ServerGameTuning.DefaultRangedShotsPerSecond;
        public float rangedReloadSeconds = ServerGameTuning.DefaultRangedReloadSeconds;

        public float safeZoneWaitBeforeShrink = ServerGameTuning.DefaultSafeZoneWaitBeforeShrink;
        public float safeZoneShrinkSpeed = ServerGameTuning.DefaultSafeZoneShrinkSpeed;
        public float safeZoneDamagePerSecond = ServerGameTuning.DefaultSafeZoneDamagePerSecond;

        public float jumpGravityMultiplier = ServerGameTuning.DefaultJumpGravityMultiplier;
        public float eagleFlightDuration = ServerGameTuning.DefaultEagleFlightDuration;
        public float eagleJumpSpeed = ServerGameTuning.DefaultEagleJumpSpeed;
        public float eagleFlySpeedBonus = ServerGameTuning.DefaultEagleFlySpeedBonus;
        public float eagleGlideGravityMultiplier = ServerGameTuning.DefaultEagleGlideGravityMultiplier;
        public float eagleMaximumFallSpeed = ServerGameTuning.DefaultEagleMaximumFallSpeed;

        public void CaptureCurrent()
        {
            tigerLeapDuration = ServerGameTuning.TigerLeapDuration;
            tigerLeapSpeed = ServerGameTuning.TigerLeapSpeed;
            tigerLeapUpSpeed = ServerGameTuning.TigerLeapUpSpeed;
            tigerLeapHitRadius = ServerGameTuning.TigerLeapHitRadius;
            tigerLeapDamage = ServerGameTuning.TigerLeapDamage;
            tigerLeapKnockback = ServerGameTuning.TigerLeapKnockback;

            projectileSpeed = ServerGameTuning.ProjectileSpeed;
            projectileRangeSeconds = ServerGameTuning.ProjectileRangeSeconds;
            projectileGravityMultiplier = ServerGameTuning.ProjectileGravityMultiplier;
            projectileLiftMultiplier = ServerGameTuning.ProjectileLiftMultiplier;
            projectileDamageMultiplier = ServerGameTuning.ProjectileDamageMultiplier;
            projectileRadiusMultiplier = ServerGameTuning.ProjectileRadiusMultiplier;
            rangedShotsPerSecond = ServerGameTuning.RangedShotsPerSecond;
            rangedReloadSeconds = ServerGameTuning.RangedReloadSeconds;

            safeZoneWaitBeforeShrink = ServerGameTuning.SafeZoneWaitBeforeShrink;
            safeZoneShrinkSpeed = ServerGameTuning.SafeZoneShrinkSpeed;
            safeZoneDamagePerSecond = ServerGameTuning.SafeZoneDamagePerSecond;

            jumpGravityMultiplier = ServerGameTuning.JumpGravityMultiplier;
            eagleFlightDuration = ServerGameTuning.EagleFlightDuration;
            eagleJumpSpeed = ServerGameTuning.EagleJumpSpeed;
            eagleFlySpeedBonus = ServerGameTuning.EagleFlySpeedBonus;
            eagleGlideGravityMultiplier = ServerGameTuning.EagleGlideGravityMultiplier;
            eagleMaximumFallSpeed = ServerGameTuning.EagleMaximumFallSpeed;
        }
    }
}

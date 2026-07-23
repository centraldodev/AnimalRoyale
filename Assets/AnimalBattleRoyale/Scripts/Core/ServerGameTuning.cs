using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AnimalBattleRoyale
{
    public static class ServerGameTuning
    {
        private const string PresetResourcePath = "ServerGameTuning";
#if UNITY_EDITOR
        private const string PresetAssetPath = "Assets/AnimalBattleRoyale/Resources/ServerGameTuning.asset";
#endif

        public const float DefaultTigerLeapDuration = 0.9f;
        public const float DefaultTigerLeapSpeed = 25f;
        public const float DefaultTigerLeapUpSpeed = 9.2f;
        public const float DefaultTigerLeapHitRadius = 1.25f;
        public const float DefaultTigerLeapDamage = 20f;
        public const float DefaultTigerLeapKnockback = 12f;

        public const float DefaultProjectileSpeed = 33f;
        public const float DefaultProjectileRangeSeconds = 4.5f;
        public const float DefaultProjectileGravityMultiplier = 1f;
        public const float DefaultProjectileLiftMultiplier = 1f;
        public const float DefaultProjectileDamageMultiplier = 1f;
        public const float DefaultProjectileRadiusMultiplier = 1f;
        public const float DefaultRangedShotsPerSecond = 7f;
        public const float DefaultRangedReloadSeconds = 2f;

        public const float DefaultSafeZoneWaitBeforeShrink = 35f;
        public const float DefaultSafeZoneShrinkSpeed = 0.62f;
        public const float DefaultSafeZoneDamagePerSecond = 12f;

        public const float DefaultJumpGravityMultiplier = 1f;
        public const float DefaultEagleFlightDuration = 5f;
        public const float DefaultEagleJumpSpeed = 8.2f;
        public const float DefaultEagleFlySpeedBonus = 1.35f;
        public const float DefaultEagleGlideGravityMultiplier = 0.18f;
        public const float DefaultEagleMaximumFallSpeed = -2.4f;

        public static float TigerLeapDuration = DefaultTigerLeapDuration;
        public static float TigerLeapSpeed = DefaultTigerLeapSpeed;
        public static float TigerLeapUpSpeed = DefaultTigerLeapUpSpeed;
        public static float TigerLeapHitRadius = DefaultTigerLeapHitRadius;
        public static float TigerLeapDamage = DefaultTigerLeapDamage;
        public static float TigerLeapKnockback = DefaultTigerLeapKnockback;

        public static float ProjectileSpeed = DefaultProjectileSpeed;
        public static float ProjectileRangeSeconds = DefaultProjectileRangeSeconds;
        public static float ProjectileGravityMultiplier = DefaultProjectileGravityMultiplier;
        public static float ProjectileLiftMultiplier = DefaultProjectileLiftMultiplier;
        public static float ProjectileDamageMultiplier = DefaultProjectileDamageMultiplier;
        public static float ProjectileRadiusMultiplier = DefaultProjectileRadiusMultiplier;
        public static float RangedShotsPerSecond = DefaultRangedShotsPerSecond;
        public static float RangedReloadSeconds = DefaultRangedReloadSeconds;

        public static float SafeZoneWaitBeforeShrink = DefaultSafeZoneWaitBeforeShrink;
        public static float SafeZoneShrinkSpeed = DefaultSafeZoneShrinkSpeed;
        public static float SafeZoneDamagePerSecond = DefaultSafeZoneDamagePerSecond;

        public static float JumpGravityMultiplier = DefaultJumpGravityMultiplier;
        public static float EagleFlightDuration = DefaultEagleFlightDuration;
        public static float EagleJumpSpeed = DefaultEagleJumpSpeed;
        public static float EagleFlySpeedBonus = DefaultEagleFlySpeedBonus;
        public static float EagleGlideGravityMultiplier = DefaultEagleGlideGravityMultiplier;
        public static float EagleMaximumFallSpeed = DefaultEagleMaximumFallSpeed;

        static ServerGameTuning()
        {
            LoadProjectDefaults();
        }

        public static void LoadProjectDefaults()
        {
            ServerGameTuningPreset preset = Resources.Load<ServerGameTuningPreset>(PresetResourcePath);
            if (preset != null) ApplyPreset(preset);
        }

        public static void RestoreDefaults()
        {
            RestoreBuiltInDefaults();
        }

        public static void RestoreBuiltInDefaults()
        {
            TigerLeapDuration = DefaultTigerLeapDuration;
            TigerLeapSpeed = DefaultTigerLeapSpeed;
            TigerLeapUpSpeed = DefaultTigerLeapUpSpeed;
            TigerLeapHitRadius = DefaultTigerLeapHitRadius;
            TigerLeapDamage = DefaultTigerLeapDamage;
            TigerLeapKnockback = DefaultTigerLeapKnockback;

            ProjectileSpeed = DefaultProjectileSpeed;
            ProjectileRangeSeconds = DefaultProjectileRangeSeconds;
            ProjectileGravityMultiplier = DefaultProjectileGravityMultiplier;
            ProjectileLiftMultiplier = DefaultProjectileLiftMultiplier;
            ProjectileDamageMultiplier = DefaultProjectileDamageMultiplier;
            ProjectileRadiusMultiplier = DefaultProjectileRadiusMultiplier;
            RangedShotsPerSecond = DefaultRangedShotsPerSecond;
            RangedReloadSeconds = DefaultRangedReloadSeconds;

            SafeZoneWaitBeforeShrink = DefaultSafeZoneWaitBeforeShrink;
            SafeZoneShrinkSpeed = DefaultSafeZoneShrinkSpeed;
            SafeZoneDamagePerSecond = DefaultSafeZoneDamagePerSecond;

            JumpGravityMultiplier = DefaultJumpGravityMultiplier;
            EagleFlightDuration = DefaultEagleFlightDuration;
            EagleJumpSpeed = DefaultEagleJumpSpeed;
            EagleFlySpeedBonus = DefaultEagleFlySpeedBonus;
            EagleGlideGravityMultiplier = DefaultEagleGlideGravityMultiplier;
            EagleMaximumFallSpeed = DefaultEagleMaximumFallSpeed;
        }

        private static void ApplyPreset(ServerGameTuningPreset preset)
        {
            TigerLeapDuration = preset.tigerLeapDuration;
            TigerLeapSpeed = preset.tigerLeapSpeed;
            TigerLeapUpSpeed = preset.tigerLeapUpSpeed;
            TigerLeapHitRadius = preset.tigerLeapHitRadius;
            TigerLeapDamage = preset.tigerLeapDamage;
            TigerLeapKnockback = preset.tigerLeapKnockback;

            ProjectileSpeed = preset.projectileSpeed;
            ProjectileRangeSeconds = preset.projectileRangeSeconds;
            ProjectileGravityMultiplier = preset.projectileGravityMultiplier;
            ProjectileLiftMultiplier = preset.projectileLiftMultiplier;
            ProjectileDamageMultiplier = preset.projectileDamageMultiplier;
            ProjectileRadiusMultiplier = preset.projectileRadiusMultiplier;
            RangedShotsPerSecond = preset.rangedShotsPerSecond;
            RangedReloadSeconds = preset.rangedReloadSeconds;

            SafeZoneWaitBeforeShrink = preset.safeZoneWaitBeforeShrink;
            SafeZoneShrinkSpeed = preset.safeZoneShrinkSpeed;
            SafeZoneDamagePerSecond = preset.safeZoneDamagePerSecond;

            JumpGravityMultiplier = preset.jumpGravityMultiplier;
            EagleFlightDuration = preset.eagleFlightDuration;
            EagleJumpSpeed = preset.eagleJumpSpeed;
            EagleFlySpeedBonus = preset.eagleFlySpeedBonus;
            EagleGlideGravityMultiplier = preset.eagleGlideGravityMultiplier;
            EagleMaximumFallSpeed = preset.eagleMaximumFallSpeed;
        }

        public static void SaveCurrentAsProjectDefault()
        {
#if UNITY_EDITOR
            ServerGameTuningPreset preset = Resources.Load<ServerGameTuningPreset>(PresetResourcePath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<ServerGameTuningPreset>();
                AssetDatabase.CreateAsset(preset, PresetAssetPath);
            }

            preset.CaptureCurrent();
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
#endif
        }

        public static float ClampPositive(float value, float minimum, float maximum)
        {
            return Mathf.Clamp(value, minimum, maximum);
        }
    }
}

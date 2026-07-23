using UnityEngine;

namespace AnimalBattleRoyale
{
    public enum CharacterScreenSide
    {
        Left = 0,
        Right = 1
    }

    public enum RangedFireMode
    {
        SingleShot = 0,
        Automatic = 1
    }

    public static class GameSettings
    {
        private const string MouseSensitivityKey = "Settings_MouseSensitivity";
        private const string AimMouseSensitivityKey = "Settings_AimMouseSensitivity";
        private const string CharacterSideKey = "Settings_CharacterScreenSide";
        private const string AutomaticSprintKey = "Settings_AutomaticSprint";
        private const string RangedFireModeKey = "Settings_RangedFireMode";
        private const string MasterVolumeKey = "Settings_MasterVolume";
        private const string EffectsAmbientVolumeKey = "Settings_EffectsAmbientVolume";

        public const float MinMouseSensitivity = 0.35f;
        public const float MaxMouseSensitivity = 2.5f;
        public const float DefaultMouseSensitivity = 1f;
        public const float DefaultAimMouseSensitivity = 0.55f;
        public const CharacterScreenSide DefaultCharacterSide = CharacterScreenSide.Left;
        public const bool DefaultAutomaticSprint = true;
        public const RangedFireMode DefaultRangedFireMode = RangedFireMode.SingleShot;
        public const float DefaultMasterVolume = 0.85f;
        public const float DefaultEffectsAmbientVolume = 0.9f;

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
            set
            {
                PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                ApplyAudioVolumes();
            }
        }

        public static float EffectsAmbientVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsAmbientVolumeKey, DefaultEffectsAmbientVolume));
            set
            {
                PlayerPrefs.SetFloat(EffectsAmbientVolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                ApplyAudioVolumes();
            }
        }

        public static void ApplyAudioVolumes()
        {
            // All audio currently in the game belongs to the effects/ambience bus.
            // Keeping the second gain here makes the setting immediately effective
            // for existing and future AudioSources without rewriting their authored mix.
            AudioListener.volume = MasterVolume * EffectsAmbientVolume;
        }

        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity),
                MinMouseSensitivity, MaxMouseSensitivity);
            set
            {
                PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity));
                PlayerPrefs.Save();
            }
        }

        public static float AimMouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(AimMouseSensitivityKey, DefaultAimMouseSensitivity),
                MinMouseSensitivity, MaxMouseSensitivity);
            set
            {
                PlayerPrefs.SetFloat(AimMouseSensitivityKey,
                    Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity));
                PlayerPrefs.Save();
            }
        }

        public static CharacterScreenSide CharacterSide
        {
            get => (CharacterScreenSide)Mathf.Clamp(PlayerPrefs.GetInt(CharacterSideKey, (int)DefaultCharacterSide), 0, 1);
            set
            {
                PlayerPrefs.SetInt(CharacterSideKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static bool AutomaticSprint
        {
            get => PlayerPrefs.GetInt(AutomaticSprintKey, DefaultAutomaticSprint ? 1 : 0) != 0;
            set
            {
                PlayerPrefs.SetInt(AutomaticSprintKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static RangedFireMode RangedFireMode
        {
            get => (RangedFireMode)Mathf.Clamp(PlayerPrefs.GetInt(RangedFireModeKey,
                (int)DefaultRangedFireMode), 0, 1);
            set
            {
                PlayerPrefs.SetInt(RangedFireModeKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static void RestoreDefaults()
        {
            MasterVolume = DefaultMasterVolume;
            EffectsAmbientVolume = DefaultEffectsAmbientVolume;
            MouseSensitivity = DefaultMouseSensitivity;
            AimMouseSensitivity = DefaultAimMouseSensitivity;
            CharacterSide = DefaultCharacterSide;
            AutomaticSprint = DefaultAutomaticSprint;
            RangedFireMode = DefaultRangedFireMode;
        }
    }
}

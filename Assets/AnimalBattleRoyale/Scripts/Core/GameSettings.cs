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
        private const string CharacterSideKey = "Settings_CharacterScreenSide";
        private const string AutomaticSprintKey = "Settings_AutomaticSprint";
        private const string RangedFireModeKey = "Settings_RangedFireMode";

        public const float MinMouseSensitivity = 0.35f;
        public const float MaxMouseSensitivity = 2.5f;
        public const float DefaultMouseSensitivity = 1f;
        public const CharacterScreenSide DefaultCharacterSide = CharacterScreenSide.Left;
        public const bool DefaultAutomaticSprint = false;
        public const RangedFireMode DefaultRangedFireMode = RangedFireMode.SingleShot;

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
            MouseSensitivity = DefaultMouseSensitivity;
            CharacterSide = DefaultCharacterSide;
            AutomaticSprint = DefaultAutomaticSprint;
            RangedFireMode = DefaultRangedFireMode;
        }
    }
}

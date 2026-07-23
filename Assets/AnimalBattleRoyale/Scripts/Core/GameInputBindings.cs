using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public enum GameInputAction
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Jump,
        Sprint,
        RangedAttack,
        MeleeAttack,
        Ability,
        Consume,
        Descend,
        Aim,
        WeaponPrimary,
        WeaponSecondary,
        WeaponThird,
        Reload
    }

    public readonly struct GameInputBindingDefinition
    {
        public readonly GameInputAction Action;
        public readonly string Label;
        public readonly KeyCode DefaultKey;

        public GameInputBindingDefinition(GameInputAction action, string label, KeyCode defaultKey)
        {
            Action = action;
            Label = label;
            DefaultKey = defaultKey;
        }
    }

    public static class GameInputBindings
    {
        private const string PlayerPrefsPrefix = "AnimalBattleRoyale.Input.";

        private static readonly GameInputBindingDefinition[] definitions =
        {
            new GameInputBindingDefinition(GameInputAction.MoveForward, "ANDAR PARA FRENTE", KeyCode.W),
            new GameInputBindingDefinition(GameInputAction.MoveBackward, "ANDAR PARA TRÁS", KeyCode.S),
            new GameInputBindingDefinition(GameInputAction.MoveLeft, "ANDAR PARA ESQUERDA", KeyCode.A),
            new GameInputBindingDefinition(GameInputAction.MoveRight, "ANDAR PARA DIREITA", KeyCode.D),
            new GameInputBindingDefinition(GameInputAction.Jump, "PULAR", KeyCode.Space),
            new GameInputBindingDefinition(GameInputAction.Sprint, "CORRER", KeyCode.LeftShift),
            new GameInputBindingDefinition(GameInputAction.RangedAttack, "ATIRAR", KeyCode.Mouse0),
            new GameInputBindingDefinition(GameInputAction.Reload, "RECARREGAR", KeyCode.R),
            new GameInputBindingDefinition(GameInputAction.WeaponPrimary, "MUNIÇÃO PRIMÁRIA", KeyCode.Alpha1),
            new GameInputBindingDefinition(GameInputAction.WeaponSecondary, "MUNIÇÃO SECUNDÁRIA", KeyCode.Alpha2),
            new GameInputBindingDefinition(GameInputAction.WeaponThird, "TERCEIRA MUNIÇÃO", KeyCode.Alpha3),
            // Right mouse button moved from melee to aim (precision zoom for the sniper/nozes
            // ammo) — melee moved to V so the two don't fight over the same button.
            new GameInputBindingDefinition(GameInputAction.MeleeAttack, "BATER", KeyCode.V),
            new GameInputBindingDefinition(GameInputAction.Ability, "HABILIDADE", KeyCode.Q),
            new GameInputBindingDefinition(GameInputAction.Consume, "COLETAR / USAR", KeyCode.F),
            new GameInputBindingDefinition(GameInputAction.Descend, "DESCER", KeyCode.LeftControl),
            new GameInputBindingDefinition(GameInputAction.Aim, "MIRAR DE PRECISÃO", KeyCode.Mouse1)
        };

        private static readonly Dictionary<GameInputAction, KeyCode> keys = new Dictionary<GameInputAction, KeyCode>();

        public static IReadOnlyList<GameInputBindingDefinition> Definitions => definitions;

        static GameInputBindings()
        {
            foreach (GameInputBindingDefinition definition in definitions)
            {
                int stored = PlayerPrefs.GetInt(PlayerPrefsPrefix + definition.Action, (int)definition.DefaultKey);
                keys[definition.Action] = Enum.IsDefined(typeof(KeyCode), stored)
                    ? (KeyCode)stored
                    : definition.DefaultKey;
            }
        }

        public static KeyCode GetKey(GameInputAction action)
        {
            return keys.TryGetValue(action, out KeyCode key) ? key : KeyCode.None;
        }

        public static bool IsHeld(GameInputAction action)
        {
            KeyCode key = GetKey(action);
            return key != KeyCode.None && Input.GetKey(key);
        }

        public static bool WasPressedThisFrame(GameInputAction action)
        {
            KeyCode key = GetKey(action);
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        public static bool TryRebind(GameInputAction action, KeyCode newKey, out GameInputAction? swappedAction)
        {
            swappedAction = null;
            if (newKey == KeyCode.None || newKey == KeyCode.Escape || !keys.ContainsKey(action)) return false;

            KeyCode previousKey = keys[action];
            if (previousKey == newKey) return true;

            foreach (GameInputBindingDefinition definition in definitions)
            {
                if (definition.Action == action || keys[definition.Action] != newKey) continue;
                swappedAction = definition.Action;
                keys[definition.Action] = previousKey;
                PlayerPrefs.SetInt(PlayerPrefsPrefix + definition.Action, (int)previousKey);
                break;
            }

            keys[action] = newKey;
            PlayerPrefs.SetInt(PlayerPrefsPrefix + action, (int)newKey);
            PlayerPrefs.Save();
            return true;
        }

        public static void RestoreDefaults()
        {
            foreach (GameInputBindingDefinition definition in definitions)
            {
                keys[definition.Action] = definition.DefaultKey;
                PlayerPrefs.SetInt(PlayerPrefsPrefix + definition.Action, (int)definition.DefaultKey);
            }
            PlayerPrefs.Save();
        }

        public static string GetActionLabel(GameInputAction action)
        {
            foreach (GameInputBindingDefinition definition in definitions)
            {
                if (definition.Action == action) return definition.Label;
            }
            return action.ToString().ToUpperInvariant();
        }

        public static string GetDisplayName(GameInputAction action) => GetKeyDisplayName(GetKey(action));

        public static string GetKeyDisplayName(KeyCode key)
        {
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
            {
                int button = (int)key - (int)KeyCode.Mouse0 + 1;
                return $"MOUSE {button}";
            }

            string name = key.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal)) return name.Substring(5);
            return name switch
            {
                "Space" => "ESPAÇO",
                "LeftShift" => "SHIFT ESQ.",
                "RightShift" => "SHIFT DIR.",
                "LeftControl" => "CTRL ESQ.",
                "RightControl" => "CTRL DIR.",
                "LeftAlt" => "ALT ESQ.",
                "RightAlt" => "ALT DIR.",
                "UpArrow" => "SETA CIMA",
                "DownArrow" => "SETA BAIXO",
                "LeftArrow" => "SETA ESQ.",
                "RightArrow" => "SETA DIR.",
                "Return" => "ENTER",
                "KeypadEnter" => "ENTER NUM.",
                "Backspace" => "BACKSPACE",
                "Tab" => "TAB",
                _ => name.ToUpperInvariant()
            };
        }
    }
}

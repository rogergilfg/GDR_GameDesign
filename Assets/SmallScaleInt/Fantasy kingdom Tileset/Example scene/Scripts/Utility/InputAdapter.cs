using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SmallScale.FantasyKingdomTileset
{
    /// <summary>
    /// Lightweight bridge that mimics the legacy Input API using the new Input System.
    /// Only implements the members used in this sample scene.
    /// </summary>
    public static class InputAdapter
    {
        public static Vector3 mousePosition
        {
            get
            {
                Vector2 pos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
                return new Vector3(pos.x, pos.y, 0f);
            }
        }

        public static bool mousePresent => Mouse.current != null;

        public static Vector2 mouseScrollDelta
        {
            get
            {
                Vector2 scroll = Mouse.current?.scroll.ReadValue() ?? Vector2.zero;
                // Input System scroll is typically reported in multiples of 120; normalize to legacy-style notches.
                return scroll / 120f;
            }
        }

        public static bool GetMouseButtonDown(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null && control.wasPressedThisFrame;
        }

        public static bool GetMouseButton(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null && control.isPressed;
        }

        public static bool GetMouseButtonUp(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null && control.wasReleasedThisFrame;
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (TryGetMouseButtonState(key, out var down, MouseButtonState.Down))
                return down;

            var control = GetKeyControl(key);
            return control != null && control.wasPressedThisFrame;
        }

        public static bool GetKey(KeyCode key)
        {
            if (TryGetMouseButtonState(key, out var pressed, MouseButtonState.Pressed))
                return pressed;

            var control = GetKeyControl(key);
            return control != null && control.isPressed;
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (TryGetMouseButtonState(key, out var released, MouseButtonState.Up))
                return released;

            var control = GetKeyControl(key);
            return control != null && control.wasReleasedThisFrame;
        }

        public static float GetAxisRaw(string axisName)
        {
            Vector2 gamepadStick = ReadLeftStick();

            switch (axisName)
            {
                case "Horizontal":
                    return ComposeAxis(ReadNegativeHorizontal(), ReadPositiveHorizontal(), gamepadStick.x);
                case "Vertical":
                    return ComposeAxis(ReadNegativeVertical(), ReadPositiveVertical(), gamepadStick.y);
                case "Mouse ScrollWheel":
                    return mouseScrollDelta.y;
                default:
                    return 0f;
            }
        }

        public static float GetAxis(string axisName) => GetAxisRaw(axisName);

        private static ButtonControl GetMouseButtonControl(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return null;

            return button switch
            {
                0 => mouse.leftButton,
                1 => mouse.rightButton,
                2 => mouse.middleButton,
                3 => mouse.forwardButton,
                4 => mouse.backButton,
                _ => null
            };
        }

        private static bool TryGetMouseButtonState(KeyCode key, out bool state, MouseButtonState desiredState)
        {
            state = false;
            var mouse = Mouse.current;
            if (mouse == null) return false;

            ButtonControl control = key switch
            {
                KeyCode.Mouse0 => mouse.leftButton,
                KeyCode.Mouse1 => mouse.rightButton,
                KeyCode.Mouse2 => mouse.middleButton,
                KeyCode.Mouse3 => mouse.forwardButton,
                KeyCode.Mouse4 => mouse.backButton,
                _ => null
            };

            if (control == null) return false;

            state = desiredState switch
            {
                MouseButtonState.Down => control.wasPressedThisFrame,
                MouseButtonState.Up => control.wasReleasedThisFrame,
                _ => control.isPressed
            };
            return true;
        }

        private static KeyControl GetKeyControl(KeyCode key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;

            Key? mapped = MapKey(key);
            return mapped.HasValue ? keyboard[mapped.Value] : null;
        }

        private static Key? MapKey(KeyCode keyCode)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                return (Key)((int)Key.A + (keyCode - KeyCode.A));

            switch (keyCode)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;
                case KeyCode.Keypad0: return Key.Numpad0;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
                case KeyCode.Keypad4: return Key.Numpad4;
                case KeyCode.Keypad5: return Key.Numpad5;
                case KeyCode.Keypad6: return Key.Numpad6;
                case KeyCode.Keypad7: return Key.Numpad7;
                case KeyCode.Keypad8: return Key.Numpad8;
                case KeyCode.Keypad9: return Key.Numpad9;
                case KeyCode.None: return null;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                default: return null;
            }
        }

        private static float ComposeAxis(bool negative, bool positive, float analogContribution)
        {
            float value = analogContribution;
            if (negative) value -= 1f;
            if (positive) value += 1f;
            return Mathf.Clamp(value, -1f, 1f);
        }

        private static Vector2 ReadLeftStick()
        {
            var gamepad = Gamepad.current;
            return gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
        }

        private static bool ReadPositiveHorizontal() => IsKeyPressed(KeyCode.D) || IsKeyPressed(KeyCode.RightArrow);
        private static bool ReadNegativeHorizontal() => IsKeyPressed(KeyCode.A) || IsKeyPressed(KeyCode.LeftArrow);
        private static bool ReadPositiveVertical() => IsKeyPressed(KeyCode.W) || IsKeyPressed(KeyCode.UpArrow);
        private static bool ReadNegativeVertical() => IsKeyPressed(KeyCode.S) || IsKeyPressed(KeyCode.DownArrow);

        private static bool IsKeyPressed(KeyCode code)
        {
            var control = GetKeyControl(code);
            return control != null && control.isPressed;
        }

        private enum MouseButtonState
        {
            Down,
            Up,
            Pressed
        }
    }
}





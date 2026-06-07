// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    internal static class UnityInputSystemHelper {

        // Unity Input Manager reports delta values in half pixels for some reason
        public const float mouseXYDeltaMultiplier = 0.5f;

        private static uint s_lastRewiredAbsFrame;
        private static bool s_runInBackground;
        private static bool s_ignoreInputWhenAppNotInFocus;
        private static int s_lastSettingsSyncUnityFrame;
        private static int s_lastUpdateUnityFrame;

        static UnityInputSystemHelper() {
            StaticInitialize();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StaticInitialize() {
            s_lastRewiredAbsFrame = uint.MaxValue;
            s_runInBackground = false;
            s_ignoreInputWhenAppNotInFocus = false;
            s_lastSettingsSyncUnityFrame = -1;
            s_lastUpdateUnityFrame = int.MaxValue;
        }

        // This can be called from both UnityInputManagerProxy and UnityInputSystemInputSource on each frame
        internal static void Update() {
            if (!Rewired.ReInput.isReady) return;
            if (s_lastRewiredAbsFrame == Rewired.ReInput.time.absFrame) return; // already updated this Rewired frame
            s_lastRewiredAbsFrame = Rewired.ReInput.time.absFrame;
            UpdateSynchronizedSettings();

            // Update is no longer manually pumped every Rewired update loop due to problems with touch input and interference
            // with other scripts and plugins that may rely on Unity Input System.

            // Force manual update if it somehow got changed
            if (UnityEngine.InputSystem.InputSystem.settings.updateMode != UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually) {
                UnityEngine.InputSystem.InputSystem.settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
            }

            // Pump the InputSystem update only once per Unity frame, unless in Manual update mode.
            // If FixedUpdate is enabled, this will be called on the first of either FixedUpdate
            // or Update, effectively mirroring InputManager's update time.
            // Allow updating every call if manual mode.
            int unityFrame = UnityEngine.Time.frameCount;
            if (Rewired.ReInput.configuration.updateMode == Config.UpdateMode.Manual || s_lastUpdateUnityFrame != unityFrame) {
                s_lastUpdateUnityFrame = unityFrame;
                UnityEngine.InputSystem.InputSystem.Update();
            }
        }

        private static void UpdateSynchronizedSettings() {
            if (UnityEngine.Time.frameCount == s_lastSettingsSyncUnityFrame) return;
            s_lastSettingsSyncUnityFrame = UnityEngine.Time.frameCount;
            SyncRunInBackgroundSettings();
        }

        private static void SyncRunInBackgroundSettings() {
            bool changed = false;

            if (s_runInBackground != UnityEngine.Application.runInBackground || s_runInBackground != UnityEngine.InputSystem.InputSystem.runInBackground) {
                s_runInBackground = UnityEngine.Application.runInBackground;
                // Keep Input System runInBackground in sync with Application.runInBackground
                if (UnityEngine.InputSystem.InputSystem.runInBackground != s_runInBackground) {
                    UnityEngine.InputSystem.InputSystem.runInBackground = s_runInBackground;
                }
                changed = true;
            }

            if (s_ignoreInputWhenAppNotInFocus != Rewired.ReInput.configuration.ignoreInputWhenAppNotInFocus) {
                s_ignoreInputWhenAppNotInFocus = Rewired.ReInput.configuration.ignoreInputWhenAppNotInFocus;
                changed = true;
            }

            if (changed) {
                if (s_runInBackground) {
                    if (s_ignoreInputWhenAppNotInFocus) {
                        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.ResetAndDisableAllDevices;
                    } else {
                        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                    }
                } else {
                    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.ResetAndDisableAllDevices;
                }
            }
        }

        internal static void SyncToRewiredSettings() {
            if (!Rewired.ReInput.isReady) {
                UnityEngine.Debug.LogError("Rewired: Rewired is not initialized. Unity Input System settings could not be synchronized to Rewired settings.");
                return;
            }

            // Set to update events manually
            if (UnityEngine.InputSystem.InputSystem.settings.updateMode != UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually) {
                UnityEngine.InputSystem.InputSystem.settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
            }

            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;

            UpdateSynchronizedSettings();
        }

        public static UnityEngine.Vector2 GetValue(UnityEngine.InputSystem.Controls.Vector2Control control) {
            if (control == null) return new UnityEngine.Vector2();
            return control.value;
        }
        public static UnityEngine.Vector3 GetValue(UnityEngine.InputSystem.Controls.Vector3Control control) {
            if (control == null) return new UnityEngine.Vector3();
            return control.value;
        }
        public static float GetValue(UnityEngine.InputSystem.Controls.AxisControl control) {
            if (control == null) return 0f;
            return control.ReadUnprocessedValue();
        }
        public static bool GetValue(UnityEngine.InputSystem.Controls.ButtonControl control) {
            if (control == null) return false;
            return control.isPressed;
        }
        public static bool GetValue(UnityEngine.InputSystem.Controls.ButtonControl control, out float floatValue) {
            if (control == null) {
                floatValue = 0f;
                return false;
            }
            floatValue = control.ReadUnprocessedValue();
            return control.isPressed;
        }
        public static bool GetValueDown(UnityEngine.InputSystem.Controls.ButtonControl control) {
            if (control == null) return false;
            return control.wasPressedThisFrame;
        }
        public static bool GetValueUp(UnityEngine.InputSystem.Controls.ButtonControl control) {
            if (control == null) return false;
            return control.wasReleasedThisFrame;
        }

        public static UnityEngine.InputSystem.Key ToUnityInputSystemKey(KeyboardKeyCode keyCode) {
            UnityEngine.InputSystem.Key key;
            if (s_nisToRewiredKey.TryGetValue((int)keyCode, out key)) {
                return key;
            }
            return UnityEngine.InputSystem.Key.None;
        }

        public static bool ContainsType<TList, TItem>(System.Collections.Generic.IList<TList> list) where TList : class where TItem : class {
            if (list == null) return false;
            int count = list.Count;
            for (int i = 0; i < count; i++) {
                if (list[i] as TItem != null) return true;
            }
            return false;
        }

        // UnityEngine.InputSystem.Key: Input System 1.8.2
        private static readonly System.Collections.Generic.Dictionary<int, UnityEngine.InputSystem.Key> s_nisToRewiredKey = new System.Collections.Generic.Dictionary<int, UnityEngine.InputSystem.Key>() {
            { (int)KeyboardKeyCode.None, UnityEngine.InputSystem.Key.None },
            { (int)KeyboardKeyCode.Backspace, UnityEngine.InputSystem.Key.Backspace },
            { (int)KeyboardKeyCode.Tab, UnityEngine.InputSystem.Key.Tab },
            //{ (int)KeyboardKeyCode.Clear, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Return, UnityEngine.InputSystem.Key.Enter },
            { (int)KeyboardKeyCode.Pause, UnityEngine.InputSystem.Key.Pause },
            { (int)KeyboardKeyCode.Escape, UnityEngine.InputSystem.Key.Escape },
            { (int)KeyboardKeyCode.Space, UnityEngine.InputSystem.Key.Space },
            //{ (int)KeyboardKeyCode.Exclaim, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.DoubleQuote, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.Hash, UnityEngine.InputSystem.Key. },  // no equivalent key
            //{ (int)KeyboardKeyCode.Dollar, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.Ampersand, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Quote, UnityEngine.InputSystem.Key.Quote },
            //{ (int)KeyboardKeyCode.LeftParen, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.RightParen, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.Asterisk, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.Plus, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Comma, UnityEngine.InputSystem.Key.Comma },
            { (int)KeyboardKeyCode.Minus, UnityEngine.InputSystem.Key.Minus },
            { (int)KeyboardKeyCode.Period, UnityEngine.InputSystem.Key.Period },
            { (int)KeyboardKeyCode.Slash, UnityEngine.InputSystem.Key.Slash },
            { (int)KeyboardKeyCode.Alpha0, UnityEngine.InputSystem.Key.Digit0 },
            { (int)KeyboardKeyCode.Alpha1, UnityEngine.InputSystem.Key.Digit1 },
            { (int)KeyboardKeyCode.Alpha2, UnityEngine.InputSystem.Key.Digit2 },
            { (int)KeyboardKeyCode.Alpha3, UnityEngine.InputSystem.Key.Digit3 },
            { (int)KeyboardKeyCode.Alpha4, UnityEngine.InputSystem.Key.Digit4 },
            { (int)KeyboardKeyCode.Alpha5, UnityEngine.InputSystem.Key.Digit5 },
            { (int)KeyboardKeyCode.Alpha6, UnityEngine.InputSystem.Key.Digit6 },
            { (int)KeyboardKeyCode.Alpha7, UnityEngine.InputSystem.Key.Digit7 },
            { (int)KeyboardKeyCode.Alpha8, UnityEngine.InputSystem.Key.Digit8 },
            { (int)KeyboardKeyCode.Alpha9, UnityEngine.InputSystem.Key.Digit9 },
            //{ (int)KeyboardKeyCode.Colon, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Semicolon, UnityEngine.InputSystem.Key.Semicolon },
            //{ (int)KeyboardKeyCode.Less, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Equals, UnityEngine.InputSystem.Key.Equals },
            //{ (int)KeyboardKeyCode.Greater, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.Question, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.At, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.LeftBracket, UnityEngine.InputSystem.Key.LeftBracket },
            { (int)KeyboardKeyCode.Backslash, UnityEngine.InputSystem.Key.Backslash },
            { (int)KeyboardKeyCode.RightBracket, UnityEngine.InputSystem.Key.RightBracket },
            //{ (int)KeyboardKeyCode.Caret, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.Underscore, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.BackQuote, UnityEngine.InputSystem.Key.Backquote },
            { (int)KeyboardKeyCode.A, UnityEngine.InputSystem.Key.A },
            { (int)KeyboardKeyCode.B, UnityEngine.InputSystem.Key.B },
            { (int)KeyboardKeyCode.C, UnityEngine.InputSystem.Key.C },
            { (int)KeyboardKeyCode.D, UnityEngine.InputSystem.Key.D },
            { (int)KeyboardKeyCode.E, UnityEngine.InputSystem.Key.E },
            { (int)KeyboardKeyCode.F, UnityEngine.InputSystem.Key.F },
            { (int)KeyboardKeyCode.G, UnityEngine.InputSystem.Key.G },
            { (int)KeyboardKeyCode.H, UnityEngine.InputSystem.Key.H },
            { (int)KeyboardKeyCode.I, UnityEngine.InputSystem.Key.I },
            { (int)KeyboardKeyCode.J, UnityEngine.InputSystem.Key.J },
            { (int)KeyboardKeyCode.K, UnityEngine.InputSystem.Key.K },
            { (int)KeyboardKeyCode.L, UnityEngine.InputSystem.Key.L },
            { (int)KeyboardKeyCode.M, UnityEngine.InputSystem.Key.M },
            { (int)KeyboardKeyCode.N, UnityEngine.InputSystem.Key.N },
            { (int)KeyboardKeyCode.O, UnityEngine.InputSystem.Key.O },
            { (int)KeyboardKeyCode.P, UnityEngine.InputSystem.Key.P },
            { (int)KeyboardKeyCode.Q, UnityEngine.InputSystem.Key.Q },
            { (int)KeyboardKeyCode.R, UnityEngine.InputSystem.Key.R },
            { (int)KeyboardKeyCode.S, UnityEngine.InputSystem.Key.S },
            { (int)KeyboardKeyCode.T, UnityEngine.InputSystem.Key.T },
            { (int)KeyboardKeyCode.U, UnityEngine.InputSystem.Key.U },
            { (int)KeyboardKeyCode.V, UnityEngine.InputSystem.Key.V },
            { (int)KeyboardKeyCode.W, UnityEngine.InputSystem.Key.W },
            { (int)KeyboardKeyCode.X, UnityEngine.InputSystem.Key.X },
            { (int)KeyboardKeyCode.Y, UnityEngine.InputSystem.Key.Y },
            { (int)KeyboardKeyCode.Z, UnityEngine.InputSystem.Key.Z },
            { (int)KeyboardKeyCode.Delete, UnityEngine.InputSystem.Key.Delete },
            // ---- Numpad ----
            // NOTE: Numpad layout follows the 18-key numpad layout. Some PC keyboards
            //       have a 17-key numpad layout where the plus key is an elongated key
            //       like the numpad enter key. Be aware that in those layouts the positions
            //       of some of the operator keys are also different. However, we stay
            //       layout neutral here, too, and always use the 18-key blueprint.
            { (int)KeyboardKeyCode.Keypad0, UnityEngine.InputSystem.Key.Numpad0 },
            { (int)KeyboardKeyCode.Keypad1, UnityEngine.InputSystem.Key.Numpad1 },
            { (int)KeyboardKeyCode.Keypad2, UnityEngine.InputSystem.Key.Numpad2 },
            { (int)KeyboardKeyCode.Keypad3, UnityEngine.InputSystem.Key.Numpad3 },
            { (int)KeyboardKeyCode.Keypad4, UnityEngine.InputSystem.Key.Numpad4 },
            { (int)KeyboardKeyCode.Keypad5, UnityEngine.InputSystem.Key.Numpad5 },
            { (int)KeyboardKeyCode.Keypad6, UnityEngine.InputSystem.Key.Numpad6 },
            { (int)KeyboardKeyCode.Keypad7, UnityEngine.InputSystem.Key.Numpad7 },
            { (int)KeyboardKeyCode.Keypad8, UnityEngine.InputSystem.Key.Numpad8 },
            { (int)KeyboardKeyCode.Keypad9, UnityEngine.InputSystem.Key.Numpad9 },
            { (int)KeyboardKeyCode.KeypadPeriod, UnityEngine.InputSystem.Key.NumpadPeriod },
            { (int)KeyboardKeyCode.KeypadDivide, UnityEngine.InputSystem.Key.NumpadDivide },
            { (int)KeyboardKeyCode.KeypadMultiply, UnityEngine.InputSystem.Key.NumpadMultiply },
            { (int)KeyboardKeyCode.KeypadMinus, UnityEngine.InputSystem.Key.NumpadMinus },
            { (int)KeyboardKeyCode.KeypadPlus, UnityEngine.InputSystem.Key.NumpadPlus },
            { (int)KeyboardKeyCode.KeypadEnter, UnityEngine.InputSystem.Key.NumpadEnter },
            { (int)KeyboardKeyCode.KeypadEquals, UnityEngine.InputSystem.Key.NumpadEquals },
            { (int)KeyboardKeyCode.UpArrow, UnityEngine.InputSystem.Key.UpArrow },
            { (int)KeyboardKeyCode.DownArrow, UnityEngine.InputSystem.Key.DownArrow },
            { (int)KeyboardKeyCode.RightArrow, UnityEngine.InputSystem.Key.RightArrow },
            { (int)KeyboardKeyCode.LeftArrow, UnityEngine.InputSystem.Key.LeftArrow },
            { (int)KeyboardKeyCode.Insert, UnityEngine.InputSystem.Key.Insert },
            { (int)KeyboardKeyCode.Home, UnityEngine.InputSystem.Key.Home },
            { (int)KeyboardKeyCode.End, UnityEngine.InputSystem.Key.End },
            { (int)KeyboardKeyCode.PageUp, UnityEngine.InputSystem.Key.PageUp },
            { (int)KeyboardKeyCode.PageDown, UnityEngine.InputSystem.Key.PageDown },
            { (int)KeyboardKeyCode.F1, UnityEngine.InputSystem.Key.F1 },
            { (int)KeyboardKeyCode.F2, UnityEngine.InputSystem.Key.F2 },
            { (int)KeyboardKeyCode.F3, UnityEngine.InputSystem.Key.F3 },
            { (int)KeyboardKeyCode.F4, UnityEngine.InputSystem.Key.F4 },
            { (int)KeyboardKeyCode.F5, UnityEngine.InputSystem.Key.F5 },
            { (int)KeyboardKeyCode.F6, UnityEngine.InputSystem.Key.F6 },
            { (int)KeyboardKeyCode.F7, UnityEngine.InputSystem.Key.F7 },
            { (int)KeyboardKeyCode.F8, UnityEngine.InputSystem.Key.F8 },
            { (int)KeyboardKeyCode.F9, UnityEngine.InputSystem.Key.F9 },
            { (int)KeyboardKeyCode.F10, UnityEngine.InputSystem.Key.F10 },
            { (int)KeyboardKeyCode.F11, UnityEngine.InputSystem.Key.F11 },
            { (int)KeyboardKeyCode.F12, UnityEngine.InputSystem.Key.F12 },
            //{ (int)KeyboardKeyCode.F13, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.F14, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.F15, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Numlock, UnityEngine.InputSystem.Key.NumLock },
            { (int)KeyboardKeyCode.CapsLock, UnityEngine.InputSystem.Key.CapsLock },
            { (int)KeyboardKeyCode.ScrollLock, UnityEngine.InputSystem.Key.ScrollLock },
            { (int)KeyboardKeyCode.RightShift, UnityEngine.InputSystem.Key.RightShift },
            { (int)KeyboardKeyCode.LeftShift, UnityEngine.InputSystem.Key.LeftShift },
            { (int)KeyboardKeyCode.RightControl, UnityEngine.InputSystem.Key.RightCtrl },
            { (int)KeyboardKeyCode.LeftControl, UnityEngine.InputSystem.Key.LeftCtrl },
            { (int)KeyboardKeyCode.RightAlt, UnityEngine.InputSystem.Key.RightAlt },
            { (int)KeyboardKeyCode.LeftAlt, UnityEngine.InputSystem.Key.LeftAlt },

            // These keys all return either LeftMeta or RightMeta on NIS.
            // This will result in Windows and Command keys returning true when either is pressed. Is this a problem?
            { (int)KeyboardKeyCode.RightCommand, UnityEngine.InputSystem.Key.RightMeta },
            { (int)KeyboardKeyCode.LeftCommand, UnityEngine.InputSystem.Key.LeftMeta },
            // IS THIS CONSISTENT ON OSX AND LINUX AND OTHER PLATFORMS???????????????????????????????????????????????????????????????????????????
            //{ (int)KeyboardKeyCode.LeftWindows, UnityEngine.InputSystem.Key.LeftWindows }, // Windows key returns Command on Windows Standalone in UnityEngine.Input and can't be detected here, so ignore it
            //{ (int)KeyboardKeyCode.RightWindows, UnityEngine.InputSystem.Key.RightWindows }, // Windows key returns Command on Windows Standalone in UnityEngine.Input and can't be detected here, so ignore it

            // AltGr is an alias of RightAlt on NIS.
            // This means any time Right Alt is pressed, AltGr will return true.
            // DISCARD FOR NOW
            //{ (int)KeyboardKeyCode.AltGr, UnityEngine.InputSystem.Key.AltGr },

            //{ (int)KeyboardKeyCode.Help, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Print, UnityEngine.InputSystem.Key.PrintScreen },
            //{ (int)KeyboardKeyCode.SysReq, UnityEngine.InputSystem.Key. }, // no equivalent key
            //{ (int)KeyboardKeyCode.Break, UnityEngine.InputSystem.Key. }, // no equivalent key
            { (int)KeyboardKeyCode.Menu, UnityEngine.InputSystem.Key.ContextMenu },
        };

        public static bool TryGetHidPidVid(UnityEngine.InputSystem.InputDevice device, out HidPidVid hidPidVid) {
            try {
                hidPidVid = Rewired.Utils.Libraries.TinyJson.JsonParser.FromJson<HidPidVid>(device.description.capabilities);
                return hidPidVid.isValid;
            } catch {
                hidPidVid = new HidPidVid();
                return false;
            }
        }

        public struct HidPidVid {
            public int productId;
            public int vendorId;
            public bool isValid { get { return productId != 0 || vendorId != 0; } }
        }

#if UNITY_EDITOR

        public static string ToString(UnityEngine.InputSystem.InputDevice device, uint indent = 0) {
            string s = "";
            s += GetIndent(indent) + "Device Type: " + device.GetType().Name + "\n";
            s += GetIndent(indent) + "Device Id: " + device.deviceId + "\n";
            s += GetIndent(indent) + "description:\n" + ToString(device.description, indent) + "\n";
            s += ToString((UnityEngine.InputSystem.InputControl)device, indent) + "\n";
            var controls = device.allControls;
            s += GetIndent(indent) + "Controls (" + controls.Count + "):\n";
            indent++;
            for (int i = 0; i < controls.Count; i++) {
                s += GetIndent(indent) + "Control [" + i + "]:\n";
                s += ToString(controls[i], indent + 1) + "\n";
            }
            return s;
        }
        public static string ToString(UnityEngine.InputSystem.InputControl control, uint indent) {
            string s = "";
            s += GetIndent(indent) + "Control Type: " + control.GetType().Name + "\n";
            s += GetIndent(indent) + "name: " + control.name + "\n";
            s += GetIndent(indent) + "displayName: " + control.displayName + "\n";
            s += GetIndent(indent) + "shortDisplayName: " + control.shortDisplayName + "\n";
            s += GetIndent(indent) + "path: " + control.path + "\n";
            s += GetIndent(indent) + "layout: " + control.layout + "\n";
            s += GetIndent(indent) + "variants: " + control.variants;
            return s;
        }
        public static string ToString(UnityEngine.InputSystem.Layouts.InputDeviceDescription description, uint indent) {
            string s = "";
            s += GetIndent(indent) + "capabilities:\n";
             if (!string.IsNullOrEmpty(description.capabilities)) s += description.capabilities.Trim() + "\n";
            s += GetIndent(indent) + "deviceClass: " + description.deviceClass + "\n";
            s += GetIndent(indent) + "interfaceName: " + description.interfaceName + "\n";
            s += GetIndent(indent) + "manufacturer: " + description.manufacturer + "\n";
            s += GetIndent(indent) + "product: " + description.product + "\n";
            s += GetIndent(indent) + "serial: " + description.serial + "\n";
            s += GetIndent(indent) + "version: " + description.version;
            return s;
        }

        private static string GetIndent(uint level) {
            const string indent = "    ";
            string s = string.Empty;
            for (int i = 0; i < level; i++) {
                s += indent;
            }
            return s;
        }

#endif
    }
}

#endif

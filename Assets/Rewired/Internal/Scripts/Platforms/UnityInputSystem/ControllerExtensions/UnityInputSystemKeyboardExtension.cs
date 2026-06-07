// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    /// <summary>
    /// Provides access to information exposed by Unity Input System for a Keyboard.
    /// </summary>
    public class UnityInputSystemKeyboardExtension : UnityInputSystemControllerExtension {

        /// <summary>
        /// The underlying Keyboard InputDevice.
        /// </summary>
        public UnityEngine.InputSystem.Keyboard keyboard {
            get {
                return ((Source)GetSource()).getInputDevice() as UnityEngine.InputSystem.Keyboard;
            }
        }

        /// <summary>
        /// The current keyboard layout.
        /// Keyboard layouts are defined by Unity Input System.
        /// Refer to the Unity Input System documentation for possible return values.
        /// </summary>
        public string keyboardLayout {
            get {
                return keyboard != null ? keyboard.keyboardLayout : null;
            }
        }

        public UnityInputSystemKeyboardExtension(
            UnityEngine.InputSystem.Keyboard keyboard
        ) : base(keyboard) {
            if (keyboard == null) throw new System.ArgumentNullException("keyboard");
        }
        protected UnityInputSystemKeyboardExtension(
            // Using a Func to get the device because of UnifiedKeyboard/Mouse which use the current device
            System.Func<UnityEngine.InputSystem.InputDevice> getInputDevice
        ) : base(getInputDevice) {
        }
        protected UnityInputSystemKeyboardExtension(Source other) : base(other) {
        }
        protected UnityInputSystemKeyboardExtension(UnityInputSystemKeyboardExtension other) : base(other) {
        }

        public override Controller.Extension ShallowCopy() {
            return new UnityInputSystemKeyboardExtension(this);
        }
    }
}

#endif

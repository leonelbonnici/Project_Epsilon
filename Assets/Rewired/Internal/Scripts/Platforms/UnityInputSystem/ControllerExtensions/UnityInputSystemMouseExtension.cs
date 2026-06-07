// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    /// <summary>
    /// Provides access to information exposed by Unity Input System for a Mouse.
    /// </summary>
    public class UnityInputSystemMouseExtension : UnityInputSystemControllerExtension {

        /// <summary>
        /// The underlying Mouse InputDevice.
        /// </summary>
        public UnityEngine.InputSystem.Mouse mouse {
            get {
                return ((Source)GetSource()).getInputDevice() as UnityEngine.InputSystem.Mouse;
            }
        }

        public UnityInputSystemMouseExtension(
            UnityEngine.InputSystem.Mouse mouse
        ) : base(mouse) {
            if (mouse == null) throw new System.ArgumentNullException("mouse");
        }
        protected UnityInputSystemMouseExtension(
            // Using a Func to get the device because of UnifiedKeyboard/Mouse which use the current device
            System.Func<UnityEngine.InputSystem.InputDevice> getInputDevice
        ) : base(getInputDevice) {
        }
        protected UnityInputSystemMouseExtension(Source other) : base(other) {
        }
        protected UnityInputSystemMouseExtension(UnityInputSystemMouseExtension other) : base(other) {
        }

        public override Controller.Extension ShallowCopy() {
            return new UnityInputSystemMouseExtension(this);
        }
    }
}

#endif

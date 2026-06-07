// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    /// <summary>
    /// Provides access to information exposed by Unity Input System for a controller.
    /// </summary>
    public class UnityInputSystemControllerExtension : UnityInputSystemControllerExtensionBase {

        /// <summary>
        /// The underlying InputDevice.
        /// </summary>
        public UnityEngine.InputSystem.InputDevice inputDevice {
            get {
                return ((Source)GetSource()).getInputDevice();
            }
        }

        public UnityInputSystemControllerExtension(UnityEngine.InputSystem.InputDevice inputDevice) : this(() => inputDevice) {
            if (inputDevice == null) throw new System.ArgumentNullException("inputDevice");
        }
        // Overload for Universal Keyboard/Mouse
        protected UnityInputSystemControllerExtension(
            // Using a Func to get the device because of UnifiedKeyboard/Mouse which use the current device
            System.Func<UnityEngine.InputSystem.InputDevice> getInputDevice
        ) : base(new Source(getInputDevice)) {
            if (getInputDevice == null) throw new System.ArgumentNullException("getInputDevice");
        }
        protected UnityInputSystemControllerExtension(Source other) : base(other) {
        }
        protected UnityInputSystemControllerExtension(UnityInputSystemControllerExtension other) : base(other) {
        }

        public override Controller.Extension ShallowCopy() {
            return new UnityInputSystemControllerExtension(this);
        }

        new protected class Source : UnityInputSystemControllerExtensionBase.Source {

            public readonly System.Func<UnityEngine.InputSystem.InputDevice> getInputDevice;

            public Source(System.Func<UnityEngine.InputSystem.InputDevice> getInputDevice) : base() {
                this.getInputDevice = getInputDevice;
            }
        }
    }
}

#endif

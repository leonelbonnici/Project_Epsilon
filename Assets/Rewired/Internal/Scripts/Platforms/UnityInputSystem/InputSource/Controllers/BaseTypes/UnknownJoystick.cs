// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class UnknownJoystick : Joystick {

            private const string tag = "UnknownJoystick";

            public UnknownJoystick(InitOptions initOptions) : base(initOptions) {
                AddMatchingTag(tag);
            }

            new public class InitOptions : Joystick.InitOptions {

                public InitOptions(UnityEngine.InputSystem.InputDevice inputDevice)
                    : base(UnityInputSystemDeviceType.Unknown, inputDevice) {
                }
            }
        }
    }
}

#endif

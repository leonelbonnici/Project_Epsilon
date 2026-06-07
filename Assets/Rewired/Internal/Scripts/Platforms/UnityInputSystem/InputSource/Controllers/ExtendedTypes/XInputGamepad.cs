// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class XInputGamepad : Gamepad {

            private const string tag = "XInputGamepad";

            public XInputGamepad(InitOptions initOptions) : base(initOptions) {
                AddMatchingTag(tag);
            }

            new public class InitOptions : Gamepad.InitOptions {

                public InitOptions(UnityEngine.InputSystem.XInput.XInputController xInputController)
                    : base(xInputController) {
                }
            }
        }
    }
}

#endif

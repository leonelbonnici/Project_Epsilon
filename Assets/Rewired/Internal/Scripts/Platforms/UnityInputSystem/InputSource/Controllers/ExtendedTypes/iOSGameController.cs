// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#if UNITY_EDITOR || UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class iOSGameController : Gamepad {

            private const string tag = "iOSGameController";

            public iOSGameController(InitOptions initOptions) : base(initOptions) {
                AddMatchingTag(tag);
            }

            new public class InitOptions : Gamepad.InitOptions {

                public InitOptions(UnityEngine.InputSystem.iOS.iOSGameController iOSGameController)
                    : base(iOSGameController) {
                }
            }
        }
    }
}

#endif

#endif

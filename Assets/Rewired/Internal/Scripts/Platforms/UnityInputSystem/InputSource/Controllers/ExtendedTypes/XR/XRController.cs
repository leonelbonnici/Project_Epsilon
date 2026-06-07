// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !UNITY_FORCE_INPUTSYSTEM_XR_OFF

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        // Does not currently support XR Haptics

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class XRController : TrackedDevice {

            private const string tag = "XRController";

            public XRController(InitOptions initOptions) : base(initOptions) {
                AddMatchingTag(tag);

                UnityEngine.InputSystem.XR.XRController controller = initOptions.inputDevice as UnityEngine.InputSystem.XR.XRController;
                if (controller != null) {

                    var capabilities = controller.description.capabilities;
                    var deviceDescriptor = UnityEngine.InputSystem.XR.XRDeviceDescriptor.FromJson(capabilities);

                    if ((deviceDescriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Left) != 0) {
                        AddMatchingTag(tag + "_Left");
                    } else if ((deviceDescriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Right) != 0) {
                        AddMatchingTag(tag + "_Right");
                    }
                }
            }

            new public class InitOptions : TrackedDevice.InitOptions {

                public InitOptions(UnityEngine.InputSystem.XR.XRController xrController)
                    : base(xrController) {
                }
            }
        }
    }
}

#endif

#endif

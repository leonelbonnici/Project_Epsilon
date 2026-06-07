// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !DISABLE_BUILTIN_INPUT_SYSTEM_OPENVR && !UNITY_FORCE_INPUTSYSTEM_XR_OFF

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected sealed class OpenVRViveWand : XRController {

            private const string tag = "OpenVRViveWand";

            private readonly Unity.XR.OpenVR.ViveWand _inputDevice;

            public OpenVRViveWand(InitOptions initOptions) : base(initOptions) {
                _inputDevice = initOptions.inputDevice as Unity.XR.OpenVR.ViveWand;
                if (_inputDevice == null) throw new System.ArgumentNullException("inputDevice");
                AddMatchingTag(tag);

                var capabilities = _inputDevice.description.capabilities;
                var deviceDescriptor = UnityEngine.InputSystem.XR.XRDeviceDescriptor.FromJson(capabilities);

                if ((deviceDescriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Left) != 0) {
                    AddMatchingTag(tag + "_Left");
                } else if ((deviceDescriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Right) != 0) {
                    AddMatchingTag(tag + "_Right");
                }
            }

            new public class InitOptions : XRController.InitOptions {

                public InitOptions(Unity.XR.OpenVR.ViveWand inputDevice)
                    : base(inputDevice) {
                }
            }
        }
    }
}

#endif

#endif

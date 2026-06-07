// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !DISABLE_BUILTIN_INPUT_SYSTEM_OPENVR && !UNITY_FORCE_INPUTSYSTEM_XR_OFF

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected abstract class OpenVRViveTracker : Joystick {

            private const string tag = "OpenVRViveTracker";

            private readonly Unity.XR.OpenVR.ViveTracker _inputDevice;

            protected OpenVRViveTracker(InitOptions initOptions) : base(initOptions) {
                _inputDevice = initOptions.inputDevice as Unity.XR.OpenVR.ViveTracker;
                if (_inputDevice == null) throw new System.ArgumentNullException("inputDevice");
                AddMatchingTag(tag);
            }

            new public class InitOptions : TrackedDevice.InitOptions {

                public InitOptions(Unity.XR.OpenVR.ViveTracker inputDevice)
                    : base(inputDevice) {
                }
            }
        }
    }
}

#endif

#endif

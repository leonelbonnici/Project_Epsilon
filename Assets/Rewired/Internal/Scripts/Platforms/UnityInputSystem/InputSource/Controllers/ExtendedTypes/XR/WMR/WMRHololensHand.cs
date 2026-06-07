// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !DISABLE_BUILTIN_INPUT_SYSTEM_WINDOWSMR && !UNITY_FORCE_INPUTSYSTEM_XR_OFF

using UnityEngine.InputSystem;

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected sealed class WMRHololensHand : XRController, Rewired.ControllerExtensions.Internal.IHololensHandExtensionSource {

            private const string tag = "WMRHololensHand";

            private readonly UnityEngine.XR.WindowsMR.Input.HololensHand _inputDevice;

            public WMRHololensHand(InitOptions initOptions) : base(initOptions) {
                _inputDevice = initOptions.inputDevice as UnityEngine.XR.WindowsMR.Input.HololensHand;
                if (_inputDevice == null) throw new System.ArgumentNullException("inputDevice");
                AddMatchingTag(tag);
            }

            protected override void OnCreateExtensions(System.Collections.Generic.IList<Rewired.Controller.Extension> extensions) {
                extensions.Add(new Rewired.ControllerExtensions.HololensHandExtension(this));
                base.OnCreateExtensions(extensions);
            }

            protected override bool ExcludeControl(InputControl control) {
                // These are not inputs and should not be controller elements
                if (control == _inputDevice.sourceLossRisk) return true;
                if (control == _inputDevice.sourceLossMitigationDirection) return true;
                return base.ExcludeControl(control);
            }

            float Rewired.ControllerExtensions.Internal.IHololensHandExtensionSource.sourceLossRisk {
                get {
                    return _inputDevice.sourceLossRisk.value;
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.IHololensHandExtensionSource.sourceLossMitigationDirection {
                get {
                    return _inputDevice.sourceLossMitigationDirection.value;
                }
            }

            new public class InitOptions : XRController.InitOptions {

                public InitOptions(UnityEngine.XR.WindowsMR.Input.HololensHand inputDevice)
                    : base(inputDevice) {
                }
            }
        }
    }
}

#endif

#endif

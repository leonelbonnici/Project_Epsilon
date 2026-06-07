// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !DISABLE_BUILTIN_INPUT_SYSTEM_WINDOWSMR && !UNITY_FORCE_INPUTSYSTEM_XR_OFF

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class WMRSpatialController : XRController, Rewired.ControllerExtensions.Internal.IWMRSpatialControllerExtensionSource {

            private const string tag = "WMRSpatialController";

            private readonly UnityEngine.XR.WindowsMR.Input.WMRSpatialController _inputDevice;

            public WMRSpatialController(InitOptions initOptions) : base(initOptions) {
                _inputDevice = initOptions.inputDevice as UnityEngine.XR.WindowsMR.Input.WMRSpatialController;
                if (_inputDevice == null) throw new System.ArgumentNullException("inputDevice");
                AddMatchingTag(tag);
            }

            protected override void OnCreateExtensions(System.Collections.Generic.IList<Rewired.Controller.Extension> extensions) {
                extensions.Add(new Rewired.ControllerExtensions.WMRSpatialControllerExtension(this));
                base.OnCreateExtensions(extensions);
            }

            protected override bool ExcludeControl(UnityEngine.InputSystem.InputControl control) {
                // These are not inputs and should not be controller elements
                if (control == _inputDevice.batteryLevel) return true;
                if (control == _inputDevice.sourceLossRisk) return true;
                if (control == _inputDevice.sourceLossMitigationDirection) return true;
                // These should only be exposed through controller extension
                if (control == _inputDevice.pointerPosition) return true;
                if (control == _inputDevice.pointerRotation) return true;
                return base.ExcludeControl(control);
            }

            float Rewired.ControllerExtensions.Internal.IWMRSpatialControllerExtensionSource.batteryLevel {
                get {
                    return _inputDevice.batteryLevel.value;
                }
            }

            float Rewired.ControllerExtensions.Internal.IWMRSpatialControllerExtensionSource.sourceLossRisk {
                get {
                    return _inputDevice.sourceLossRisk.value;
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.IWMRSpatialControllerExtensionSource.sourceLossMitigationDirection {
                get {
                    return _inputDevice.sourceLossMitigationDirection.value;
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.IWMRSpatialControllerExtensionSource.pointerPosition {
                get {
                    return _inputDevice.pointerPosition.value;
                }
            }

            UnityEngine.Quaternion Rewired.ControllerExtensions.Internal.IWMRSpatialControllerExtensionSource.pointerRotation {
                get {
                    return _inputDevice.pointerRotation.value;
                }
            }

            new public class InitOptions : XRController.InitOptions {

                public InitOptions(UnityEngine.XR.WindowsMR.Input.WMRSpatialController inputDevice)
                    : base(inputDevice) {
                }
            }
        }
    }
}

#endif

#endif

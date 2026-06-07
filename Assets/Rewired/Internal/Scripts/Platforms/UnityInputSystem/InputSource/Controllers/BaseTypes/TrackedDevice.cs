// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class TrackedDevice : Joystick, Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource {

            private const string tag = "TrackedDevice";

            private readonly UnityEngine.InputSystem.TrackedDevice _inputDevice;
            private readonly InputControlWrapper<UnityEngine.Vector3> _accelerationControl;
            private readonly InputControlWrapper<UnityEngine.Vector3> _angularAccelerationControl;
            private readonly InputControlWrapper<UnityEngine.Vector3> _velocityControl;
            private readonly InputControlWrapper<UnityEngine.Vector3> _angularVelocityControl;

            public TrackedDevice(InitOptions initOptions) : base(initOptions) {
                _inputDevice = initOptions.inputDevice as UnityEngine.InputSystem.TrackedDevice;
                if (_inputDevice == null) throw new System.ArgumentNullException("inputDevice");
                _accelerationControl = new InputControlWrapper<UnityEngine.Vector3>();
                _angularAccelerationControl = new InputControlWrapper<UnityEngine.Vector3>();
                _velocityControl = new InputControlWrapper<UnityEngine.Vector3>();
                _angularVelocityControl = new InputControlWrapper<UnityEngine.Vector3>();
                AddMatchingTag(tag);

#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !UNITY_FORCE_INPUTSYSTEM_XR_OFF
                try {
                    var capabilities = _inputDevice.description.capabilities;
                    var deviceDescriptor = UnityEngine.InputSystem.XR.XRDeviceDescriptor.FromJson(capabilities);

                    if ((deviceDescriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Left) != 0) {
                        AddMatchingTag(tag + "_Left");
                    } else if ((deviceDescriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Right) != 0) {
                        AddMatchingTag(tag + "_Right");
                    }
                } catch {
                }
#endif
            }

            protected override void OnInitialize() {
                base.OnInitialize();
                _accelerationControl.SetControl(GetDeviceAccelerationControl());
                _angularAccelerationControl.SetControl(GetDeviceAngularAccelerationControl());
                _velocityControl.SetControl(GetDeviceVelocityControl());
                _angularVelocityControl.SetControl(GetDeviceAngularVelocityControl());
            }

            protected override void OnCreateControls(System.Collections.Generic.List<Control> controls) {

                // Exclude all controls from polling by path for unknown devices
                for (int i = 0; i < controls.Count; i++) {
                    if (ExcludeControlFromPolling(controls[i].control)) {
                        controls[i].excludeFromPolling = true;
                    }
                }

                base.OnCreateControls(controls);
            }

            protected override void OnCreateExtensions(System.Collections.Generic.IList<Rewired.Controller.Extension> extensions) {
                // Only add TrackedDevice extension is one hasn't already been added by a sub class
                if (Rewired.Utils.ListTools.Find(extensions, x => x is Rewired.ControllerExtensions.TrackedDeviceExtension) == null) {
                    extensions.Add(new Rewired.ControllerExtensions.TrackedDeviceExtension(this));
                }
                base.OnCreateExtensions(extensions);
            }

            protected override bool ExcludeControl(UnityEngine.InputSystem.InputControl control) {
                for (int i = 0; i < s_excludeControlNames.Length; i++) {
                    if (control.path.EndsWith(s_excludeControlNames[i], System.StringComparison.OrdinalIgnoreCase)) return true;
                }
                return base.ExcludeControl(control);
            }

            private static readonly string[] s_excludeControlNames = new string[] {

                "devicePosition",
                "deviceRotation",
                "deviceVelocity",
                "deviceAcceleration",
                "deviceAngularVelocity",
                "deviceAngularAcceleration",

                // Taken from various XRHMD sub classes.
                // XRHMD is not supported by default, but the user could enable support.
                "userPresence",
                "leftEyePosition",
                "leftEyeRotation",
                "leftEyeVelocity",
                "leftEyeAngularVelocity",
                "leftEyeAcceleration",
                "leftEyeAngularAcceleration",
                "rightEyePosition",
                "rightEyeRotation",
                "rightEyeVelocity",
                "rightEyeAngularVelocity",
                "rightEyeAcceleration",
                "rightEyeAngularAcceleration",
                "centerEyePosition",
                "centerEyeRotation",
                "centerEyeVelocity",
                "centerEyeAngularVelocity",
                "centerEyeAcceleration",
                "centerEyeAngularAcceleration",

                // Taken from WMR Spatial Controller
                "pointerPosition",
                "pointerRotation",

                // These are not really inputs.
                // They were taken from the various Unity XR device classes.
                "trackingState",
                "isTracked",
                "sourceLossRisk",
                "sourceLossMitigationDirection",
                "batteryLevel",
            };

            private static readonly string[] s_excludeFromPollingControlNames = new string[] {
            };

            private static bool ExcludeControlFromPolling(UnityEngine.InputSystem.InputControl control) {
                if (control == null) return false;
                for (int i = 0; i < s_excludeFromPollingControlNames.Length; i++) {
                    if (control.path.EndsWith(s_excludeFromPollingControlNames[i], System.StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }

            protected virtual UnityEngine.InputSystem.Controls.Vector3Control GetDeviceVelocityControl() {
                return _inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("deviceVelocity");
            }

            protected virtual UnityEngine.InputSystem.Controls.Vector3Control GetDeviceAngularVelocityControl() {
                return _inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("deviceAngularVelocity");
            }

            protected virtual UnityEngine.InputSystem.Controls.Vector3Control GetDeviceAccelerationControl() {
                return _inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("deviceAcceleration");
            }

            protected virtual UnityEngine.InputSystem.Controls.Vector3Control GetDeviceAngularAccelerationControl() {
                return _inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("deviceAngularAcceleration");
            }

#region ITrackedDeviceExtensionSource

            bool Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.isTracked {
                get {
                    return _inputDevice.isTracked.isPressed;
                }
            }

            Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.trackedValues {
                get {
                    return ToRewiredTrackingState(_inputDevice.trackingState.value);
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.position {
                get {
                    return _inputDevice.devicePosition.value;
                }
            }

            UnityEngine.Quaternion Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.rotation {
                get {
                    return _inputDevice.deviceRotation.value;
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.velocity {
                get {
                    return _velocityControl.value;
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.angularVelocity {
                get {
                    return _angularVelocityControl.value;
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.acceleration {
                get {
                    return _accelerationControl.value;
                }
            }

            UnityEngine.Vector3 Rewired.ControllerExtensions.Internal.ITrackedDeviceExtensionSource.angularAcceleration {
                get {
                    return _angularAccelerationControl.value;
                }
            }

#endregion

            private static Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags ToRewiredTrackingState(int state) {
#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !UNITY_FORCE_INPUTSYSTEM_XR_OFF
                UnityEngine.XR.InputTrackingState uState = (UnityEngine.XR.InputTrackingState)state;
                switch (uState) {
                    case UnityEngine.XR.InputTrackingState.None:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.None;
                    case UnityEngine.XR.InputTrackingState.Position:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.Position;
                    case UnityEngine.XR.InputTrackingState.Rotation:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.Rotation;
                    case UnityEngine.XR.InputTrackingState.Acceleration:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.Acceleration;
                    case UnityEngine.XR.InputTrackingState.AngularAcceleration:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.AngularAcceleration;
                    case UnityEngine.XR.InputTrackingState.Velocity:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.Velocity;
                    case UnityEngine.XR.InputTrackingState.AngularVelocity:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.AngularVelocity;
                    case UnityEngine.XR.InputTrackingState.All:
                        return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.All;
                    default:
                        return (Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags)state;
                }
#else
                if (state == 63) return Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags.All;
                return (Rewired.ControllerExtensions.TrackedDeviceTrackedValueFlags)state;
#endif
            }

            private class InputControlWrapper<TValue> where TValue : struct {
                
                private UnityEngine.InputSystem.InputControl<TValue> _control;
                
                public bool exists { get { return _control != null; } }
                public TValue value { get { return _control != null ? _control.value : default(TValue); } }
                public TValue valuePrev { get { return _control != null ? _control.ReadValueFromPreviousFrame() : default(TValue); } }
                public TValue valueRaw { get { return _control != null ? _control.ReadUnprocessedValue() : default(TValue); } }

                public InputControlWrapper() {
                }

                public void SetControl(UnityEngine.InputSystem.InputControl<TValue> control) {
                    _control = control;
                }
            }

            new public class InitOptions : Joystick.InitOptions {

                public InitOptions(UnityEngine.InputSystem.TrackedDevice trackedDevice)
                    : base(UnityInputSystemDeviceType.TrackedDevice, trackedDevice) {
                }
            }
        }
    }
}

#endif

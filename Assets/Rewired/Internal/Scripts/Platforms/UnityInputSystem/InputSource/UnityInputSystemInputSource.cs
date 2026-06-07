// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#pragma warning disable 0649

#if UNITY_EDITOR
//#define REWIRED_DEBUG_THIS
#endif

namespace Rewired.Platforms.UnityInputSystem {

    /// <summary>
    /// This class and all contained classes are for internal use only and should not be modified or extended.
    /// </summary>
    /// <exclude></exclude>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod()]
#endif
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StaticInitialize() {
            Rewired.Internal.GlobalObjectProvider.Register(Rewired.Internal.GlobalObjectProvider.key_createInputSource, CreateInstanceForObjectProvider);
        }

        private static bool CreateInstanceForObjectProvider(object arg1, object arg2, out object result) {
            result = null;
            if (!(arg1 is int)) return false;
            int inputSourceId = (int)arg1;
            if (inputSourceId != 10) return false;
            if (!(arg2 is UnityInputSystemInputSourceInitOptions)) return false;
            try {
                result = new UnityInputSystemInputSource((UnityInputSystemInputSourceInitOptions)arg2);
                return true;
            } catch (System.Exception) {
                return false;
            }
        }

        private const int maxDisconnectedJoysticks = 16;

        private readonly System.Collections.Generic.List<Joystick> _joysticks;
        private readonly System.Collections.Generic.List<Joystick> _disconnectedJoysticks;
        private readonly bool _handleKeyboard;
        private readonly bool _handleMouse;
        private readonly bool _handleJoysticks;
        private readonly bool _xrDevicesOnlyMode;

        private System.Collections.Generic.List<DeviceChangeEvent> _pendingDeviceChangeEvents;

        public UnityInputSystemInputSource(UnityInputSystemInputSourceInitOptions initOptions) :
            base(initOptions) {

            UnityInputSystemHelper.SyncToRewiredSettings();

            useApproximateMatching = false; // prevent weak matching (match only on systemId)

            _xrDevicesOnlyMode = initOptions.xrDevicesOnlyMode;
            if (_xrDevicesOnlyMode) {
                _handleJoysticks = true;
            } else {
                _handleKeyboard = initOptions.handleKeyboard;
                _handleMouse = initOptions.handleMouse;
                _handleJoysticks = initOptions.handleJoysticks;
            }

            if (_handleKeyboard) {
                bool showSystemKeyLabels;
                initOptions.TryGetConfigValue<bool>("showSystemKeyLabels", out showSystemKeyLabels);
                AddController(new UnifiedKeyboard(new UnifiedKeyboard.InitOptions(showSystemKeyLabels)));
            }

            if (_handleMouse) {
                AddController(new UnifiedMouse());
            }

            if (_handleJoysticks) {
                _joysticks = new System.Collections.Generic.List<Joystick>();
                _disconnectedJoysticks = new System.Collections.Generic.List<Joystick>();
            }

            _pendingDeviceChangeEvents = new System.Collections.Generic.List<DeviceChangeEvent>();
        }

        protected override void OnInitialize() {
            base.OnInitialize();

            // Synchronize run in background
            UnityEngine.InputSystem.InputSystem.runInBackground = !Rewired.ReInput.configuration.ignoreInputWhenAppNotInFocus;

            UnityEngine.InputSystem.InputSystem.onDeviceChange += OnDeviceChanged;
            UnityInputSystemSettings.AllowedJoystickInputDevicesChangedEvent += OnAllowedJoystickInputDeviceTypeChanged;

            if (_handleJoysticks) {
                RefreshJoysticks();
            }
        }

        protected override void OnBeforeUpdate() {
            base.OnBeforeUpdate();

            if (_handleJoysticks) {
                RefreshJoysticks();
            }
        }

        protected override void OnUpdate() {
            base.OnUpdate();
            UnityInputSystemHelper.Update();
        }

        protected override void OnDeinitialize() {
            base.OnDeinitialize();
            UnityInputSystemSettings.AllowedJoystickInputDevicesChangedEvent -= OnAllowedJoystickInputDeviceTypeChanged;
            UnityEngine.InputSystem.InputSystem.onDeviceChange -= OnDeviceChanged;
        }

        protected virtual Joystick CreateJoystick(UnityEngine.InputSystem.InputDevice device) {

            // Cannot support unknown device layouts because there is no way know if Unity will create a new
            // device layout that should not be included in the Joysticks list. Currently, there are many such
            // as Pointer, Pen, XR trackers, UI devices, etc. Since it's impossible to know what device layouts
            // they may add in the future, for example, ones that should be supported such as Steering Wheel,
            // and also ones that should not be supported, the only way to handle this is with a white list.
            if (!IsAllowedJoystickDevice(device)) return null;

            // Sub-classes must be evaluated before base classes

            // Xbox Controllers
            if (device as UnityEngine.InputSystem.XInput.XInputController != null) {
                return new XInputGamepad(new XInputGamepad.InitOptions(device as UnityEngine.InputSystem.XInput.XInputController));
            }

            // Sony Controllers
            if (device as UnityEngine.InputSystem.DualShock.DualShockGamepad != null) {

#if UNITY_EDITOR || UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS
                // iOS/tvOS/visionOS
                if (device as UnityEngine.InputSystem.iOS.DualShock4GampadiOS != null) {
                    return new DualShockGamepad(new DualShockGamepad.InitOptions(device as UnityEngine.InputSystem.iOS.DualShock4GampadiOS) {
                        dualShockGamepadType = DualShockGamepad.DualShockGamepadType.DualShock4
                    });
                }

                if (device as UnityEngine.InputSystem.iOS.DualSenseGampadiOS != null) {
                    return new DualShockGamepad(new DualShockGamepad.InitOptions(device as UnityEngine.InputSystem.iOS.DualSenseGampadiOS) {
                        dualShockGamepadType = DualShockGamepad.DualShockGamepadType.DualSense
                    });
                }
#endif
                return new DualShockGamepad(new DualShockGamepad.InitOptions(device as UnityEngine.InputSystem.DualShock.DualShockGamepad));
            }

#if UNITY_EDITOR || UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS
            // Apple controllers
            if (device as UnityEngine.InputSystem.iOS.iOSGameController != null) {
                return new iOSGameController(new iOSGameController.InitOptions(device as UnityEngine.InputSystem.iOS.iOSGameController));
            }
#endif

            if (device as UnityEngine.InputSystem.Gamepad != null) {
                return new Gamepad(new Gamepad.InitOptions(device as UnityEngine.InputSystem.Gamepad));
            }

            if (device as UnityEngine.InputSystem.Joystick != null) {
                return new GenericJoystick(new GenericJoystick.InitOptions(device as UnityEngine.InputSystem.Joystick));
            }

#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !UNITY_FORCE_INPUTSYSTEM_XR_OFF

#if !DISABLE_BUILTIN_INPUT_SYSTEM_OPENVR
            // OpenVR
            if (device as Unity.XR.OpenVR.ViveWand != null) {
                return new OpenVRViveWand(new OpenVRViveWand.InitOptions(device as Unity.XR.OpenVR.ViveWand));
            }
            if (device as Unity.XR.OpenVR.OpenVROculusTouchController != null) {
                return new XRController(new XRController.InitOptions(device as Unity.XR.OpenVR.OpenVROculusTouchController));
            }
            if (device as Unity.XR.OpenVR.OpenVRControllerWMR != null) {
                return new XRController(new XRController.InitOptions(device as Unity.XR.OpenVR.OpenVRControllerWMR));
            }
            if (device as Unity.XR.OpenVR.HandedViveTracker != null) {
                return new OpenVRViveTrackerHanded(new OpenVRViveTrackerHanded.InitOptions(device as Unity.XR.OpenVR.HandedViveTracker));
            }
            //if (device as Unity.XR.OpenVR.ViveTracker != null) {} // just handle as an unknown controller
#endif

#if !DISABLE_BUILTIN_INPUT_SYSTEM_OCULUS
            // Oculus XR
            if (device as Unity.XR.Oculus.Input.OculusTouchController != null) {
                return new XRController(new XRController.InitOptions(device as Unity.XR.Oculus.Input.OculusTouchController));
            }
            if (device as Unity.XR.Oculus.Input.OculusRemote != null) {
                return new Joystick(new Joystick.InitOptions(UnityInputSystemDeviceType.OtherJoystick, device as Unity.XR.Oculus.Input.OculusRemote));
            }
            if (device as Unity.XR.Oculus.Input.GearVRTrackedController != null) {
                return new XRController(new XRController.InitOptions(device as Unity.XR.Oculus.Input.GearVRTrackedController));
            }
#endif

#if !DISABLE_BUILTIN_INPUT_SYSTEM_WINDOWSMR
            // Windows Mixed Reality
            if (device as UnityEngine.XR.WindowsMR.Input.HololensHand != null) {
                return new WMRHololensHand(new WMRHololensHand.InitOptions(device as UnityEngine.XR.WindowsMR.Input.HololensHand));
            }
            if (device as UnityEngine.XR.WindowsMR.Input.WMRSpatialController != null) {
                return new WMRSpatialController(new WMRSpatialController.InitOptions(device as UnityEngine.XR.WindowsMR.Input.WMRSpatialController));
            }
#endif

            if (device as UnityEngine.InputSystem.XR.XRController != null) {
                // Unknown XRController should be okay to support.
                // Certain problem controls are excluded by name.
                return new XRController(new XRController.InitOptions(device as UnityEngine.InputSystem.XR.XRController));
            }
#endif

            if (device as UnityEngine.InputSystem.TrackedDevice != null) {
                return new TrackedDevice(new TrackedDevice.InitOptions(device as UnityEngine.InputSystem.TrackedDevice));
            }

            // Unknown input device type. This would never happen unless the user added allowed types.
            return new UnknownJoystick(new UnknownJoystick.InitOptions(device));
        }

        private void RefreshJoysticks() {

            UnityEngine.InputSystem.Utilities.ReadOnlyArray<UnityEngine.InputSystem.InputDevice> devices = UnityEngine.InputSystem.InputSystem.devices;

#if REWIRED_DEBUG_THIS
            //for (int i = 0; i < devices.Count; i++) { DebugLog("Device[" + i + "]:\n" + ToString(devices[i], 1)); }
#endif

            // Process events first
            {
                // Add and remove by event notification.
                // Removal is done in case device layout changes or similar.
                // This will remove the old controller and create a new one for simplicity.

                // Process all events in order
                DeviceChangeEvent e;

                for (int i = 0; i < _pendingDeviceChangeEvents.Count; i++) {
                    e = _pendingDeviceChangeEvents[i];
                    if (!IsAllowedJoystickDevice(e.device)) continue;

                    switch (e.type) {
                        case DeviceChangeEventType.Disconnected:
                            RemoveJoystick(e.device);
                            break;
                        case DeviceChangeEventType.Connected:
                            // Always try to reconnect to existing devices
                            if (!TryReconnectJoystick(e.device)) {
                                TryAddJoystick(e.device);
                            }
                            break;
                        case DeviceChangeEventType.IdentityChanged: {
                                RemoveJoystick(e.device);

                                // If the device identity changes, remove the device from disconnected pool
                                // to prevent it from being reused so a new device will be created for it
                                int index = IndexOf(_disconnectedJoysticks, e.device);
                                if (index >= 0) {
                                    for (int j = _disconnectedJoysticks.Count - 1; j >= 0; j--) {
                                        if (Equals(_disconnectedJoysticks[i], e.device)) {
                                            _disconnectedJoysticks.RemoveAt(i);
                                        }
                                    }
                                }
                            }
                            break;
                        default:
                            throw new System.NotImplementedException();
                    }
                }
            }

            // Process by comparing lists
            {
                // Remove
                int joystickCount = _joysticks.Count;
                for (int i = joystickCount - 1; i >= 0; i--) {
                    if (!Contains(devices, _joysticks[i].inputDevice)) {
                        RemoveJoystickAt(i);
                    }
                }

                // Add
                for (int i = 0; i < devices.Count; i++) {
                    if (!IsAllowedJoystickDevice(devices[i])) continue;
                    TryAddJoystick(devices[i]);
                }
            }

            _pendingDeviceChangeEvents.Clear();
        }

        private bool TryAddJoystick(UnityEngine.InputSystem.InputDevice device) {
            if (Contains(_joysticks, device)) return false; // already in list
            var joystick = CreateJoystick(device);
            if (joystick == null) return false;
            bool result = AddJoystick(joystick);
            DebugLogIf(result, "Connected joystick: " + _joysticks[_joysticks.Count - 1]);
            return result;
        }

        private bool TryReconnectJoystick(UnityEngine.InputSystem.InputDevice device) {
            if (Contains(_joysticks, device)) return false; // already in list
            int index = IndexOf(_disconnectedJoysticks, device);
            if (index < 0) return false; // not found in disconnected list
            bool result = AddJoystick(_disconnectedJoysticks[index]);
            _disconnectedJoysticks.RemoveAt(index);
            DebugLogIf(result, "Reconnected joystick: " + _joysticks[_joysticks.Count - 1]);
            return result;
        }

        private bool AddJoystick(Joystick joystick) {
            if (joystick == null) return false;
            if (_joysticks.Contains(joystick)) return false; // already in list
            base.AddController(joystick);
            _joysticks.Add(joystick);
            return true;
        }

        private bool RemoveJoystick(UnityEngine.InputSystem.InputDevice device) {
            bool result = false;
            int joystickIndex;
            while ((joystickIndex = IndexOf(_joysticks, device)) >= 0) {
                if (RemoveJoystickAt(joystickIndex)) {
                    result = true;
                }
            }
            return result;
        }

        private bool RemoveJoystickAt(int index) {
            return RemoveJoystickAt(index, true);
        }
        private bool RemoveJoystickAt(int index, bool addToDisconnectedList) {
            if (index < 0 || index >= _joysticks.Count) return false;
            var joystick = _joysticks[index];
            _joysticks.RemoveAt(index);
            base.RemoveController(joystick);
            if (addToDisconnectedList) {
                if (_disconnectedJoysticks.Count >= maxDisconnectedJoysticks) {
                    _disconnectedJoysticks.RemoveAt(0); // remove oldest
                }
                _disconnectedJoysticks.Add(joystick);
            }
            DebugLog("Disconnected joystick: " + joystick);
            return true;
        }

        private bool IsAllowedJoystickDevice(UnityEngine.InputSystem.InputDevice device) {
            if (device == null) return false;
            if (!UnityInputSystemSettings.IsAllowedJoystickDevice(device)) return false;
            if (_xrDevicesOnlyMode) {
                // All XR devices inherit from TrackedDevice, but it is possible not all TrackedDevices are XR...
                // I don't see a solution to this apart from a whitelist which is too problematic.
                // Deal with this if it comes up.
                return device is UnityEngine.InputSystem.TrackedDevice;
            } else {
                return true;
            }
        }

        private void OnDeviceChanged(UnityEngine.InputSystem.InputDevice device, UnityEngine.InputSystem.InputDeviceChange change) {

#if REWIRED_DEBUG_THIS
            DebugLog("Device change event: Device id " + device.deviceId + " (" + device.displayName + ") status change to " + change + "\n" + UnityInputSystemHelper.ToString(device));
#endif

            switch (change) {

                // Just simplify all device changes to disconnect so we create a new device.
                case UnityEngine.InputSystem.InputDeviceChange.Disconnected:
                case UnityEngine.InputSystem.InputDeviceChange.Removed:
                    _pendingDeviceChangeEvents.Add(
                        new DeviceChangeEvent() {
                            type = DeviceChangeEventType.Disconnected,
                            device = device
                        }
                    );
                    break;

                case UnityEngine.InputSystem.InputDeviceChange.ConfigurationChanged:
                case UnityEngine.InputSystem.InputDeviceChange.UsageChanged:
                    _pendingDeviceChangeEvents.Add(
                        new DeviceChangeEvent() {
                            type = DeviceChangeEventType.IdentityChanged,
                            device = device
                        }
                    );
                    break;

                // Added will always be sent on connect.
                // This will also be sent even if Reconnected is also sent.
                case UnityEngine.InputSystem.InputDeviceChange.Added:

                // It sends the same InputDevice object, but the deviceId is incremented.
                // NOTE: Reconnected doesn't work in testing on Windows with some devices
                // until the device is explicitly plugged in first, then unplugged during
                // a session. If the device is already connected on start, Unity will not track it.
                // This may result in problems with controller to Player mapping because
                // the controller will always be considered a new controller after the first
                // unplug / plug cycle.
                case UnityEngine.InputSystem.InputDeviceChange.Reconnected:
                    _pendingDeviceChangeEvents.Add(
                        new DeviceChangeEvent() {
                            type = DeviceChangeEventType.Connected,
                            device = device
                        }
                    );
                    break;
            }

            // Must refresh immediately because devices will throw an exception
            // if values are queried on them after they've already been removed
            // from the Unity Input System manager.
            if (_pendingDeviceChangeEvents.Count > 0) {
                RefreshJoysticks();
            }
        }

        private void OnAllowedJoystickInputDeviceTypeChanged() {

            // Remove from current list
            for (int i = _joysticks.Count - 1; i >= 0; i--) {
                if (!IsAllowedJoystickDevice(_joysticks[i].inputDevice)) {
                    RemoveJoystickAt(i);
                }
            }

            // Remove from disconnected list
            for (int i = _disconnectedJoysticks.Count - 1; i >= 0; i--) {
                if (!IsAllowedJoystickDevice(_disconnectedJoysticks[i].inputDevice)) {
                    _disconnectedJoysticks.RemoveAt(i);
                }
            }
        }

        #region Static

        private static int IndexOf(System.Collections.Generic.List<Joystick> joysticks, UnityEngine.InputSystem.InputDevice device) {
            int count = joysticks.Count;
            for (int i = 0; i < count; i++) {
                if (IsEquivalent(joysticks[i].inputDevice, device)) {
                    return i;
                }
            }
            return -1;
        }

        private static bool Contains(System.Collections.Generic.List<Joystick> joysticks, UnityEngine.InputSystem.InputDevice device) {
            return IndexOf(joysticks, device) >= 0;
        }
        private static bool Contains(UnityEngine.InputSystem.Utilities.ReadOnlyArray<UnityEngine.InputSystem.InputDevice> devices, UnityEngine.InputSystem.InputDevice device) {
            int count = devices.Count;
            for (int i = 0; i < count; i++) {
                if (IsEquivalent(devices[i], device)) {
                    return true;
                }
            }
            return false;
        }

        private static bool IsEquivalent(UnityEngine.InputSystem.InputDevice a, UnityEngine.InputSystem.InputDevice b) {
            if (a == null || b == null) return false;
            return a == b || // compare by instance because reconnect will use the same object but the deviceId is different
                a.deviceId == b.deviceId;
        }

        public static void UpdateButtonValues(
            Button button,
            UnityEngine.InputSystem.Controls.ButtonControl buttonControl
        ) {
            if (button == null || buttonControl == null) return;

            // Simulate additional events for Down/Up events that occur which do not represent the final state of the button.
            // This is so taps and releases that occur on a single frame can be captured.
            // Not sure if this can actually happen on InputSystem, but it could on InputManager.
            // TODO: Can this information be read from the low-level events instead?

            bool isPressed = buttonControl.isPressed;

            if (isPressed) { // button is on

                // A release occurred before the final press
                if (buttonControl.wasReleasedThisFrame) {
                    // Create an extra Off event before the On
                    button.SetValue(new Rewired.Platforms.Custom.CustomInputSource.Button.Value(false, 0f));
                }

            } else { // button is off

                // A press occurred before the final release
                if (buttonControl.wasPressedThisFrame) {
                    // Create an extra On event before the Off
                    button.SetValue(new Rewired.Platforms.Custom.CustomInputSource.Button.Value(true, 1f));
                }
            }

            // Create the final event
            button.SetValue(new Rewired.Platforms.Custom.CustomInputSource.Button.Value(isPressed, buttonControl.ReadUnprocessedValue()));
        }

        #endregion

        #region Debug

        [System.Diagnostics.Conditional("REWIRED_DEBUG_THIS")]
        private static void DebugLog(object o) {
            UnityEngine.Debug.Log(o);
        }
        [System.Diagnostics.Conditional("REWIRED_DEBUG_THIS")]
        private static void DebugLogIf(bool value, object o) {
            if (value) UnityEngine.Debug.Log(o);
        }

        #endregion

        #region Types

        public interface IUnityInputSystemInputDevice {
            UnityEngine.InputSystem.InputDevice inputDevice { get; }
        }

        protected abstract class Control {

            private Element _element;
            // Warning: This may be null
            private readonly UnityEngine.InputSystem.InputControl _control;
            private readonly string _relativePath;
            private bool _excludeFromPolling;

            public Element element { get { return _element; } }
            public UnityEngine.InputSystem.InputControl control { get { return _control; } }
            public string relativePath { get { return _relativePath; } }
            public bool excludeFromPolling { get { return _excludeFromPolling; } set { _excludeFromPolling = value; } }

            protected Control(UnityEngine.InputSystem.InputControl control) {
                // MUST allow nulls here because Element must exist even if the control is missing.
                // This is to prevent certain elements from being missing on certain controllers/platforms
                // which would cause element indices to shift. Element indicies must be the same on
                // all platforms for ease in mapping.
                _control = control;

                // Build path string relative to root device
                if (control != null) {
                    System.Collections.Generic.List<UnityEngine.InputSystem.InputControl> controls = new System.Collections.Generic.List<UnityEngine.InputSystem.InputControl>();
                    controls.Add(control);
                    UnityEngine.InputSystem.InputControl parent = control;
                    while ((parent = parent.parent) != null) {
                        controls.Add(parent);
                    }
                    controls.Reverse();
                    controls.RemoveAt(0); // remove root
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = 0; i < controls.Count; i++) {
                        sb.Append(controls[i].name);
                        if (i < controls.Count - 1) sb.Append('/');
                    }
                    _relativePath = sb.ToString();
                }
            }

            public void SetElement(Element element) {
                if (element == null) throw new System.ArgumentNullException("element");
                if (_element != null) throw new System.Exception("Element can only be set once.");
                _element = element;
            }

            public static bool IsChildOfType<T>(Control control) where T : UnityEngine.InputSystem.InputControl {
                if (control == null) return false;
                if (control._control == null) return false;
                return control._control.parent as T != null;
            }

            public static bool Contains(System.Collections.Generic.IList<Control> elements, UnityEngine.InputSystem.InputControl control) {
                if (control == null) return false;
                int count = elements.Count;
                Control item;
                for (int i = 0; i < count; i++) {
                    item = elements[i];
                    if (item == null) continue;
                    if (item._control == control) return true;
                }
                return false;
            }

            public static bool ContainsSelfOrChildren(System.Collections.Generic.IList<Control> elements, UnityEngine.InputSystem.InputControl control, bool recursive) {
                if (control == null) return false;
                if (Contains(elements, control)) return true;
                var children = control.children;
                if (recursive) {
                    for (int i = 0; i < children.Count; i++) {
                        if (ContainsSelfOrChildren(elements, children[i], true)) return true;
                    }
                } else {
                    for (int i = 0; i < children.Count; i++) {
                        if (children[i] == control) return true;
                    }
                }
                return false;
            }

            public static bool ContainsSelfOrParent(System.Collections.Generic.IList<Control> elements, UnityEngine.InputSystem.InputControl control, bool recursive) {
                if (control == null) return false;
                if (Contains(elements, control)) return true;
                var parent = control.parent;
                if (recursive) {
                    return ContainsSelfOrParent(elements, parent, true);
                } else {
                    return parent == control;
                }
            }
        }

        protected class AxisControl : Control {

            private readonly UnderlyingType _underlyingType;
            private readonly UnityEngine.InputSystem.Controls.AxisControl _control;

            public readonly AxisCoordinateMode coordinateMode;

            public float value {
                get {
                    if (_control == null) return 0f;
                    switch (_underlyingType) {
                        case UnderlyingType.Axis:
                        case UnderlyingType.Button:
                            return _control.ReadUnprocessedValue();
                        default:
                            return 0f;
                    }
                }
            }

            public AxisControl(UnityEngine.InputSystem.Controls.AxisControl control, AxisCoordinateMode coordinateMode) : base(control) {
                _control = control;
                this.coordinateMode = coordinateMode;
                _underlyingType = control != null ? UnderlyingType.Axis : UnderlyingType.Null;
            }
            public AxisControl(UnityEngine.InputSystem.Controls.ButtonControl control) : base(control) { // axis from button
                _control = control;
                coordinateMode = AxisCoordinateMode.Absolute;
                _underlyingType = control != null ? UnderlyingType.Button : UnderlyingType.Null;
            }

            private enum UnderlyingType {
                Null,
                Axis,
                Button
            }
        }

        protected sealed class ButtonControl : Control {

            private readonly UnityEngine.InputSystem.Controls.ButtonControl _control;
            private readonly bool _isPressureSensitive;

            public bool isPressureSensitive {
                get {
                    return _isPressureSensitive;
                }
            }

            public bool isPressed {
                get {
                    if (_control == null) return false;
                    return _control.isPressed;
                }
            }

            public bool wasPressedThisFrame {
                get {
                    if (_control == null) return false;
                    return _control.wasPressedThisFrame;
                }
            }

            public bool wasReleasedThisFrame {
                get {
                    if (_control == null) return false;
                    return _control.wasReleasedThisFrame;
                }
            }

            public float floatValue {
                get {
                    if (_control == null) return 0f;
                    return _control.ReadUnprocessedValue();
                }
            }

            public ButtonControl(UnityEngine.InputSystem.Controls.ButtonControl control) :
                // Consider all pressure sensitive because gamepad triggers are buttons
                // and there's no way to determine pressure sensitivity of buttons.
                this(control, true) {
                _control = control;
            }
            public ButtonControl(UnityEngine.InputSystem.Controls.ButtonControl control, bool isPressureSensitive) : base(control) {
                _control = control;
                _isPressureSensitive = isPressureSensitive;
            }

            public void UpdateButtonValues() {
                if (_control == null) return;
                Button button = element as Button;
                if (button == null) return;
                UnityInputSystemInputSource.UpdateButtonValues(button, _control);
            }
        }

        protected sealed class Vector2Control : Control {

            private readonly UnityEngine.InputSystem.Controls.Vector2Control _control;

            public readonly AxisCoordinateMode coordinateMode;

            public UnityEngine.Vector2 value {
                get {
                    if (_control == null) return new UnityEngine.Vector2();
                    return _control.ReadUnprocessedValue();
                }
            }

            public Vector2Control(UnityEngine.InputSystem.Controls.Vector2Control control, AxisCoordinateMode coordinateMode) : base(control) {
                _control = control;
                this.coordinateMode = coordinateMode;
            }
        }

        protected sealed class Vector3Control : Control {

            private readonly UnityEngine.InputSystem.Controls.Vector3Control _control;

            public readonly AxisCoordinateMode coordinateMode;

            public UnityEngine.Vector3 value {
                get {
                    if (_control == null) return new UnityEngine.Vector3();
                    return _control.ReadUnprocessedValue();
                }
            }

            public Vector3Control(UnityEngine.InputSystem.Controls.Vector3Control control, AxisCoordinateMode coordinateMode) : base(control) {
                _control = control;
                this.coordinateMode = coordinateMode;
            }
        }

        protected sealed class QuaternionControl : Control {

            private readonly UnityEngine.InputSystem.Controls.QuaternionControl _control;

            public UnityEngine.Quaternion value {
                get {
                    if (_control == null) return UnityEngine.Quaternion.identity;
                    return _control.ReadUnprocessedValue();
                }
            }

            public QuaternionControl(UnityEngine.InputSystem.Controls.QuaternionControl control) : base(control) {
                _control = control;
            }
        }

        private enum DeviceChangeEventType {
            Disconnected = 0,
            Connected = 1,
            IdentityChanged = 2
        }

        private struct DeviceChangeEvent {
            public DeviceChangeEventType type;
            public UnityEngine.InputSystem.InputDevice device;
        }

        #endregion
    }
}

#endif

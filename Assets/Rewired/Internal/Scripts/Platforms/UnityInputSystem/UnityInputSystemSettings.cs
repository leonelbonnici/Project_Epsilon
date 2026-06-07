// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {
    
    /// <summary>
    /// Settings for Unity Input System.
    /// </summary>
    public sealed class UnityInputSystemSettings {

        private static UnityInputSystemSettings __s_instance;
        private static UnityInputSystemSettings instance {
            get { 
                return __s_instance != null ? __s_instance : (__s_instance = new UnityInputSystemSettings());
            }
        }

        internal static System.Action AllowedJoystickInputDevicesChangedEvent;

        /// <summary>
        /// Read-only list of input device types that will appear in the Joysticks list in Rewired.
        /// If a Unity Input System Input Device matches the type or inherits from the type, it will be included.
        /// To modify the list, replace it by setting this value with a new list.
        /// <see cref="allowedJoystickInputDeviceTypes"/>, <see cref="disallowedJoystickInputDeviceTypes"/>, and <see cref="disallowedJoystickInputDevices"/>
        /// are used to determine which devices are allowed to appear in the joysticks list.
        /// An allow/disallow rule that targets a more derived type has higher priority than over a rule that targets a less derived type.
        /// For example, if Gamepad is disallowed, but DualShockGamepad (which is a subclass of Gamepad) is allowed,
        /// a device of type DualShockGamepad or a subclass of it will be allowed, while other Gamepad devices will be disallowed.
        /// If TrackedDevice is allowed, but XRHMD (which is a subclass of TrackedDevice) is disallowed, a device of type XRHMD or a subclass of it
        /// will be disallowed, while other TrackedDevice devices will be allowed.
        /// </summary>
        public static System.Collections.Generic.IList<System.Type> allowedJoystickInputDeviceTypes {
            get {
                return instance._allowedJoystickInputDeviceTypes_readOnly;
            }
            set {
                int prevCount = instance._allowedJoystickInputDeviceTypes.Count;
                instance._allowedJoystickInputDeviceTypes.Clear();
                if (value == null) {
                    if (prevCount > 0) {
                        SendAllowedJoystickInputDevicesChangedEvent();
                    }
                    return;
                }
                for (int i = 0; i < value.Count; i++) {
                    if (value[i] == null) continue;
                    instance._allowedJoystickInputDeviceTypes.Add(value[i]);
                }
                if (instance._allowedJoystickInputDeviceTypes.Count > 0) {
                    SendAllowedJoystickInputDevicesChangedEvent();
                }
            }
        }

        /// <summary>
        /// Read-only list of input device types that will not appear in the Joysticks list in Rewired.
        /// If a Unity Input System Input Device matches the type or inherits from the type, it will be excluded.
        /// To modify the list, replace it by setting this value with a new list.
        /// <see cref="allowedJoystickInputDeviceTypes"/>, <see cref="disallowedJoystickInputDeviceTypes"/>, and <see cref="disallowedJoystickInputDevices"/>
        /// are used to determine which devices are allowed to appear in the joysticks list.
        /// An allow/disallow rule that targets a more derived type has higher priority than over a rule that targets a less derived type.
        /// For example, if Gamepad is disallowed, but DualShockGamepad (which is a subclass of Gamepad) is allowed,
        /// a device of type DualShockGamepad or a subclass of it will be allowed, while other Gamepad devices will be disallowed.
        /// If TrackedDevice is allowed, but XRHMD (which is a subclass of TrackedDevice) is disallowed, a device of type XRHMD or a subclass of it
        /// will be disallowed, while other TrackedDevice devices will be allowed.
        /// </summary>
        public static System.Collections.Generic.IList<System.Type> disallowedJoystickInputDeviceTypes {
            get {
                return instance._disallowedJoystickInputDeviceTypes_readOnly;
            }
            set {
                int prevCount = instance._disallowedJoystickInputDeviceTypes.Count;
                instance._disallowedJoystickInputDeviceTypes.Clear();
                if (value == null) {
                    if (prevCount > 0) {
                        SendAllowedJoystickInputDevicesChangedEvent();
                    }
                    return;
                }
                for (int i = 0; i < value.Count; i++) {
                    if (value[i] == null) continue;
                    instance._disallowedJoystickInputDeviceTypes.Add(value[i]);
                }
                if (instance._disallowedJoystickInputDeviceTypes.Count > 0) {
                    SendAllowedJoystickInputDevicesChangedEvent();
                }
            }
        }

        /// <summary>
        /// Read-only list of input devices that will not appear in the Joysticks list in Rewired.
        /// This can be used to exclude specific devices rather than entire types.
        /// The <see cref="DeviceIdentifier"/> provides device matching critera.
        /// To modify the list, replace it by setting this value with a new list.
        /// <see cref="allowedJoystickInputDeviceTypes"/>, <see cref="disallowedJoystickInputDeviceTypes"/>, and <see cref="disallowedJoystickInputDevices"/>
        /// are used to determine which devices are allowed to appear in the joysticks list.
        /// </summary>
        public static System.Collections.Generic.IList<DeviceIdentifier> disallowedJoystickInputDevices {
            get {
                return instance._disallowedJoystickInputDevices_readOnly;
            }
            set {
                int prevCount = instance._disallowedJoystickInputDevices.Count;
                instance._disallowedJoystickInputDevices.Clear();
                if (value == null) {
                    if (prevCount > 0) {
                        SendAllowedJoystickInputDevicesChangedEvent();
                    }
                    return;
                }
                for (int i = 0; i < value.Count; i++) {
                    instance._disallowedJoystickInputDevices.Add(value[i]);
                }
                if (instance._disallowedJoystickInputDevices.Count > 0) {
                    SendAllowedJoystickInputDevicesChangedEvent();
                }
            }
        }

        internal static bool IsAllowedJoystickDevice(UnityEngine.InputSystem.InputDevice device) {
            if (device == null) return false;
            System.Type type = device.GetType();

            // More derived takes precedence over less derived.
            // If TrackedDevice is allowed, but XRHMD is not allowed, if the device a TrackedDevice, but not XRHMD, allow it.
            // If Gamepad is not allowed, but DualShockGamepad is allowed, allow it.

            int levelAllowed;
            int levelDisallowed;
            int j;

            var allowedTypes = instance._allowedJoystickInputDeviceTypes;
            var disallowedTypes = instance._disallowedJoystickInputDeviceTypes;
            int allowedTypeCount = allowedTypes.Count;
            int disallowedTypeCount = disallowedTypes.Count;

            bool allowed = false;

            for (int i = 0; i < allowedTypeCount; i++) {
                levelAllowed = GetSubclassDepth(type, allowedTypes[i]);
                if (levelAllowed < 0) continue; // does not apply
                allowed = true;

                // See if disallowed
                for (j = 0; j < disallowedTypeCount; j++) {
                    levelDisallowed = GetSubclassDepth(type, disallowedTypes[j]);
                    if (levelDisallowed < 0) continue; // does not apply
                    if (levelDisallowed < levelAllowed) { // lowest wins, if equal, allow wins over disallow
                        return false;
                    }
                }
            }

            // Check specific device exclusions
            {
                int count = instance._disallowedJoystickInputDevices.Count;
                for (int i = 0; i < count; i++) {
                    if (instance._disallowedJoystickInputDevices[i].Matches(device)) return false;
                }
            }

            return allowed;
        }

        private static void SendAllowedJoystickInputDevicesChangedEvent() {
            var evt = AllowedJoystickInputDevicesChangedEvent;
            if (evt != null) {
                evt();
            }
        }

        private static int GetSubclassDepth(System.Type derivedType, System.Type baseType) {
            if (object.ReferenceEquals(derivedType, baseType)) return 0;
            if (!derivedType.IsSubclassOf(baseType)) return -1;
            System.Type type = derivedType;
            int count = 0;
            do {
                count += 1;
            } while (!object.ReferenceEquals(type = type.BaseType, baseType));
            return count;
        }

        // Instance

        private readonly System.Collections.Generic.List<System.Type> _allowedJoystickInputDeviceTypes;
        private readonly System.Collections.Generic.List<System.Type> _disallowedJoystickInputDeviceTypes;
        private readonly System.Collections.Generic.List<DeviceIdentifier> _disallowedJoystickInputDevices;
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<System.Type> _allowedJoystickInputDeviceTypes_readOnly;
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<System.Type> _disallowedJoystickInputDeviceTypes_readOnly;
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<DeviceIdentifier> _disallowedJoystickInputDevices_readOnly;

        private UnityInputSystemSettings() {

            // Allowed device types.
            // All derived types will also be allowed.
            _allowedJoystickInputDeviceTypes = new System.Collections.Generic.List<System.Type>() {

                typeof(UnityEngine.InputSystem.Gamepad),
                typeof(UnityEngine.InputSystem.Joystick),
                
                // TrackedDevice captures XRController and a bunch of other devices like XR headsets
                // Using TrackedDevice instead of XRController because some XR controllers inherit from TrackedDevice directly like HandedViveTracker.
                typeof(UnityEngine.InputSystem.TrackedDevice),
            };

            // Disallowed device types.
            // All derived types will also be disallowed.
            _disallowedJoystickInputDeviceTypes = new System.Collections.Generic.List<System.Type>() {
#if UNITY_INPUT_SYSTEM_ENABLE_XR && (ENABLE_VR || UNITY_GAMECORE) && !UNITY_FORCE_INPUTSYSTEM_XR_OFF
                // Disallow XRHMD headsets because TrackedDevice allowed above
                typeof(UnityEngine.InputSystem.XR.XRHMD),
#endif
            };

            _allowedJoystickInputDeviceTypes_readOnly = new System.Collections.ObjectModel.ReadOnlyCollection<System.Type>(_allowedJoystickInputDeviceTypes);
            _disallowedJoystickInputDeviceTypes_readOnly = new System.Collections.ObjectModel.ReadOnlyCollection<System.Type>(_disallowedJoystickInputDeviceTypes);

            _disallowedJoystickInputDevices = new System.Collections.Generic.List<DeviceIdentifier>();
            _disallowedJoystickInputDevices_readOnly = new System.Collections.ObjectModel.ReadOnlyCollection<DeviceIdentifier>(_disallowedJoystickInputDevices);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Exclude bad devices exposed on Android

            _disallowedJoystickInputDevices.Add(
                new DeviceIdentifier() {
                    productName = "Null Device"
                }
            );

            _disallowedJoystickInputDevices.Add(
                new DeviceIdentifier() {
                    deviceClass = "AndroidGameController",
                    productName = "Virtual"
                }
            );

            _disallowedJoystickInputDevices.Add(
                new DeviceIdentifier() {
                    productName = "Amazon Fire TV Remote"
                }
            );
            
            // This is Amazon Fire TV Remote in other versions of Android
            _disallowedJoystickInputDevices.Add(
                new DeviceIdentifier() {
                    productName = "AR Keyboard"
                }
            );

            _disallowedJoystickInputDevices.Add(
                new DeviceIdentifier() {
                    productName = ".*Nexus Remote.*",
                    useRegex = true
                }
            );

#endif
        }

        /// <summary>
        /// Used to identify a specific input device for exclusion.
        /// All values are optional, but you must provide at least one for matching.
        /// Any values provided will become required for a match to occur.
        /// </summary>
        public struct DeviceIdentifier {

            /// <summary>
            /// Match by class type. See <see cref="typeMatchMode"/> for options.
            /// Optional.
            /// </summary>
            public System.Type type;

            /// <summary>
            /// Determines how type matching will be performed.
            /// </summary>
            public TypeMatchMode typeMatchMode;

            /// <summary>
            /// Device class string. This is obtained from UnityEngine.InputSystem.Layouts.InputDeviceDescription.deviceClass.
            /// Optional.
            /// </summary>
            public string deviceClass;

            /// <summary>
            /// Product name string. This is obtained from UnityEngine.InputSystem.Layouts.InputDeviceDescription.product.
            /// Optional.
            /// </summary>
            public string productName;

            /// <summary>
            /// Display name string. This is obtained from UnityEngine.InputSystem.InputDevice.displayName.
            /// Optional.
            /// </summary>
            public string displayName;

            /// <summary>
            /// Manufacturer string. This is obtained from UnityEngine.InputSystem.Layouts.InputDeviceDescription.manufacturer.
            /// Optional.
            /// </summary>
            public string manufacturer;

            /// <summary>
            /// HID vendor id. This is obtained from UnityEngine.InputSystem.Layouts.InputDeviceDescription.capabilities.
            /// This values is not available on all platforms.
            /// Optional.
            /// </summary>
            public ushort vendorId;

            /// <summary>
            /// HID vendor id. This is obtained from UnityEngine.InputSystem.Layouts.InputDeviceDescription.capabilities.
            /// This values is not available on all platforms.
            /// Optional.
            /// </summary>
            public ushort productId;

            /// <summary>
            /// Determines if regex will be used when evaluating string matches.
            /// This applies to all strings.
            /// </summary>
            public bool useRegex;

            /// <summary>
            /// Determines if the input device matches the device identifier.
            /// </summary>
            /// <param name="device">The input device.</param>
            /// <returns>True if match, false if not.</returns>
            public bool Matches(UnityEngine.InputSystem.InputDevice device) {
                if (device == null) return false;

                bool matched = false;

                // All values are optional.
                // If a value is provided, it must match.

                if (type != null) {
                    System.Type deviceType = device.GetType();
                    switch(typeMatchMode) {
                        case TypeMatchMode.ExactOrIsSubclassOf:
                            if (!object.ReferenceEquals(deviceType, type) && !deviceType.IsSubclassOf(type)) return false;
                            break;
                        case TypeMatchMode.Exact:
                            if (!object.ReferenceEquals(deviceType, type)) return false;
                            break;
                        default:
                            throw new System.NotImplementedException();
                    }
                    matched = true;
                }

                if (!string.IsNullOrEmpty(deviceClass)) {
                    if (!StringMatches(device.description.deviceClass, deviceClass, useRegex)) return false;
                    matched = true;
                }

                if (!string.IsNullOrEmpty(productName)) {
                    if (!StringMatches(device.description.product, productName, useRegex)) return false;
                    matched = true;
                }

                if (!string.IsNullOrEmpty(displayName)) {
                    if (!StringMatches(device.displayName, displayName, useRegex)) return false;
                    matched = true;
                }

                if (!string.IsNullOrEmpty(manufacturer)) {
                    if (!StringMatches(device.description.manufacturer, manufacturer, useRegex)) return false;
                    matched = true;
                }

                if (vendorId != 0 && productId != 0) {
                    Rewired.Platforms.UnityInputSystem.UnityInputSystemHelper.HidPidVid hidPidVid;
                    if (Rewired.Platforms.UnityInputSystem.UnityInputSystemHelper.TryGetHidPidVid(device, out hidPidVid)) {
                        if (vendorId != (ushort)hidPidVid.vendorId || productId != (ushort)hidPidVid.productId) {
                            return false;
                        }
                        matched = true;
                    } else {
                        return false;
                    }
                }

                return matched;
            }

            private static bool StringMatches(string input, string pattern, bool useRegex) {
                return useRegex ? System.Text.RegularExpressions.Regex.IsMatch(input, pattern) :
                    string.Equals(input, pattern, System.StringComparison.Ordinal);
            }

            /// <summary>
            /// How type matching will be performed.
            /// </summary>
            public enum TypeMatchMode {

                /// <summary>
                /// Matches if the device type is equal to or inherits from the base type.
                /// </summary>
                ExactOrIsSubclassOf = 0,

                /// <summary>
                /// Matches only if the device type is equal to the type.
                /// </summary>
                Exact = 1
            }
        }
    }
}

#endif

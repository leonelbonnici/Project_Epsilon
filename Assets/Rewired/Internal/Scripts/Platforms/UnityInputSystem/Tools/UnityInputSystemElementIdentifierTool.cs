// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#pragma warning disable 0649

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        new public sealed class ElementIdentifierTool : UnityInputSystemInputSourceBase.ElementIdentifierTool {

#if UNITY_EDITOR
            [UnityEditor.InitializeOnLoadMethod()]
#endif
            [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
            private static void StaticInitialize() {
                Rewired.Internal.GlobalObjectProvider.Register(Rewired.Internal.GlobalObjectProvider.key_createElementIdentifierTool, CreateInstanceForObjectProvider);
            }

            private static bool CreateInstanceForObjectProvider(object arg1, object arg2, out object result) {
                result = null;
                if (!(arg1 is int)) return false;
                int inputSourceId = (int)arg1;
                if (inputSourceId != 10) return false;
                if (!(arg2 is UnityInputSystemInputSource)) return false;
                result = Create((UnityInputSystemInputSource)arg2);
                return result != null;
            }

            private static ElementIdentifierTool Create(UnityInputSystemInputSource inputSource) {
                try {
                    ElementIdentifierTool o = new ElementIdentifierTool(inputSource);
                    return o;
                } catch (System.Exception ex) {
                    UnityEngine.Debug.LogError("Rewired: Exception creating Element Identifier Tool.\n" + ex);
                    return null;
                }
            }

            private readonly UnityInputSystemInputSource _inputSource;
            private readonly System.Collections.Generic.List<Rewired.Platforms.Custom.CustomInputSource.Joystick> _joysticks;
            private bool _refreshDevices;
            private readonly System.Text.StringBuilder _sb;
            private int _selectedDeviceId = -1;
            private bool _ready;
            private int _selectedDeviceIndex = 1;
            private int _selectedDeviceIndexPrev = 1;

            protected override int selectedDeviceIndex {
                get {
                    return _selectedDeviceIndex;
                }
                set {
                    if (value < 0) value = 0;
                    _selectedDeviceIndex = value;
                }
            }

            private ElementIdentifierTool(UnityInputSystemInputSource inputSource) : base() {
                if (inputSource == null) throw new System.ArgumentNullException("inputSource");
                _inputSource = inputSource;
                _joysticks = new System.Collections.Generic.List<Custom.CustomInputSource.Joystick>();
                _sb = new System.Text.StringBuilder();
            }

            protected override void OnInitialize() {
                _refreshDevices = true;
                _ready = true;
            }

            protected override void OnDeinitialize() {
            }

            protected override void OnUpdate() {
                if (!_ready) return;

                _sb.Length = 0;
                Log("Unity Input System Joystick Element Identifier\n\n");

                if (_refreshDevices) {
                    RefreshDevices();
                }

                int joystickCount = _joysticks.Count;

                // Write info to screen

                // Write connected device names
                if (joystickCount > 0) {
                    Log(joystickCount);
                    Log(" connected devices:\n");
                }
                for (int i = 0; i < joystickCount; i++) {
                    Log(_joysticks[i].deviceName);
                    Log("\n");
                }
                Log("\n");

                int joystickIndex = -1;

                {
                    // Handle selection index changes
                    bool selectionChanged = _selectedDeviceIndexPrev != _selectedDeviceIndex;
                    if (selectionChanged) {
                        _selectedDeviceId = -1; // clear selection
                        if (_selectedDeviceIndex < joystickCount) {
                            joystickIndex = _selectedDeviceIndex;
                            _selectedDeviceId = _joysticks[joystickIndex].id;
                        } else { // bad index
                            _selectedDeviceIndex = 0; // clear
                        }
                    }

                    // Find the joystick based on previous selection
                    if (_selectedDeviceId >= 0) {
                        for (int i = 0; i < joystickCount; i++) {
                            if (_joysticks[i].id == _selectedDeviceId) {
                                joystickIndex = i;
                                _selectedDeviceId = _joysticks[joystickIndex].id;
                                _selectedDeviceIndex = joystickIndex;
                                break;
                            }
                        }
                        // Clear selection when previous joystick is lost
                        if (joystickIndex < 0) {
                            _selectedDeviceId = -1;
                            _selectedDeviceIndex = 0;
                        }
                    }

                    // Select first joystick when none selected
                    if (joystickIndex < 0 && joystickCount > 0) {
                        joystickIndex = 0;
                        _selectedDeviceId = _joysticks[joystickIndex].id;
                        _selectedDeviceIndex = 0; // keep sync'd
                    }

                    _selectedDeviceIndexPrev = _selectedDeviceIndex;
                }

                // Display joystick elements on screen
                if (joystickIndex >= 0) {
                    var joystick = _joysticks[joystickIndex] as Joystick;
                    Log("Current device ");
                    Log(selectedDeviceIndex);
                    Log(": \"");
                    Log(joystick.deviceName);
                    Log("\"\n");
                    Log("(Press + or - to change monitored device id.)\n\n");

                    // Display info
                    var description = joystick.inputDevice.description;
                    LogLine("Device Name", "\"" + joystick.deviceName + "\"");
                    LogLine("Class Type", joystick.inputDevice.GetType().Name);
                    LogLine("Device Class", description.deviceClass);
                    LogLine("Is Gamepad", (joystick.inputDevice as UnityEngine.InputSystem.Gamepad) != null);
                    LogLine("Product Name", description.product);
                    LogLine("Manufacturer", description.manufacturer);
                    LogLine("Interface Name", description.interfaceName);
                    LogLine("Serial", description.serial);
                    LogLine("Version", description.version);
                    //LogLine("Capabilities", description.capabilities); // the raw JSON string
                    HidCapabilities hidCaps;
                    if (TryGetHidCapabilities(joystick.inputDevice, out hidCaps)) {
                        //LogLine("Identifier", hidCaps.productId.ToString("x2") + hidCaps.vendorId.ToString("x2"));
                        LogLine("Product Id", hidCaps.productId);
                        LogLine("Vendor Id", hidCaps.vendorId);
                    }
                    LogLine("Can Run in Background", joystick.inputDevice.canRunInBackground);

                    // Display capabilities
                    LogLine("Axis Count", joystick.axisCount);
                    LogLine("Button Count", joystick.buttonCount);

                    string name;

                    for (int i = 0; i < joystick.axisCount; i++) {
                        name = "Axis " + i;
                        LogLine(name, joystick.Axes[i].value);
                    }

                    {
                        // Buttons
                        string buttonStr = "";
                        for (int i = 0; i < joystick.buttonCount; i++) {
                            // All buttons are considered pressure sensitive on this platform
                            // because all gamepad triggers are treated as buttons and there
                            // is no way to test for this.
                            float floatValue = joystick.Buttons[i].floatValue;
                            bool boolValue = joystick.Buttons[i].boolValue;
                            if (!boolValue && floatValue == 0f) continue;

                            if (buttonStr != "") buttonStr += ", ";
                            buttonStr += i + ": " + (boolValue ? "On" : "Off") + " (" + floatValue.ToString("f2") + ")";
                        }
                        LogLine("Buttons ", buttonStr);
                    }
                }

                this.text = _sb.ToString();
            }

            protected override void OnDeviceConnected() {
                _refreshDevices = true;
            }

            protected override void OnDeviceDisconnected() {
                _refreshDevices = true;
            }

            private void RefreshDevices() {
                var joysticks = _inputSource.GetJoysticks();
                _joysticks.Clear();
                _joysticks.AddRange(joysticks);
                _refreshDevices = false;
            }

            private void Log(object value) {
                _sb.Append(value);
            }

            private void LogLine(object value) {
                _sb.Append(value);
                _sb.Append("\n");
            }
            private void LogLine(string key, object value) {
                _sb.Append(key);
                _sb.Append(" = ");
                _sb.Append(value.ToString());
                _sb.Append("\n");
            }

            private void LogLineSet(string label, object value) {
                _sb.Append(label);
                _sb.Append(":\n");
                _sb.Append(value.ToString());
                _sb.Append("\n");
            }

            private static bool TryGetHidCapabilities(UnityEngine.InputSystem.InputDevice device, out HidCapabilities hidCapabilities) {
                try {
                    hidCapabilities = Rewired.Utils.Libraries.TinyJson.JsonParser.FromJson<HidCapabilities>(device.description.capabilities);
                    return hidCapabilities.isValid;
                } catch {
                    hidCapabilities = new HidCapabilities();
                    return false;
                }
            }

            private struct HidCapabilities {
                public int productId;
                public int vendorId;

                public bool isValid { get { return productId != 0 || vendorId != 0; } }
            }
        }
    }
}

#endif

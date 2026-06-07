// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class DualShockGamepad : Gamepad {

            private const float VIBRATION_RENEW_INTERVAL = 2.0f; // renew vibration every x seconds

            private const string tag = "DualShockGamepad";

            private UnityEngine.InputSystem.DualShock.DualShockGamepad source {
                get {
                    return (UnityEngine.InputSystem.DualShock.DualShockGamepad)inputDevice;
                }
            }

            public DualShockGamepad(InitOptions initOptions) : base(initOptions) {
                AddMatchingTag(tag);

                // Add additional matching tags for the type if specified
                switch (initOptions.dualShockGamepadType) {
                    case DualShockGamepadType.DualShock4:
                        AddMatchingTag("DualShock4");
                        break;
                    case DualShockGamepadType.DualSense:
                        AddMatchingTag("DualSense");
                        break;
                    case DualShockGamepadType.DualShock3:
                        AddMatchingTag("DualShock3");
                        break;
                    case DualShockGamepadType.None:
                        break;
                    default:
                        throw new System.NotImplementedException();
                }
            }

            protected override void OnCreateControls(System.Collections.Generic.List<Control> controls) {

                UnityEngine.InputSystem.DualShock.DualShockGamepad source = this.source;

                // Create in fixed order
                // Add controls even if they are null to prevent index shifting if some are missing for whatever reason

                // Axes
                AddControls(source.leftStick, controls);
                AddControls(source.rightStick, controls);
                // Add triggers as axes instead of buttons for consistency with other platforms
                if (!Control.Contains(controls, source.leftTrigger)) {
                    controls.Add(new AxisControl(source.leftTrigger));
                }
                if (!Control.Contains(controls, source.rightTrigger)) {
                    controls.Add(new AxisControl(source.rightTrigger));
                }

                // Buttons
                AddControls(source.buttonSouth, controls);
                AddControls(source.buttonEast, controls);
                AddControls(source.buttonWest, controls);
                AddControls(source.buttonNorth, controls);
                AddControls(source.leftShoulder, controls);
                AddControls(source.rightShoulder, controls);
                AddControls(source.selectButton, controls);
                AddControls(source.startButton, controls);
                AddControls(source.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("systemButton"), controls);
                AddControls(source.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("touchpadButton"), controls);
                AddControls(source.leftStickButton, controls);
                AddControls(source.rightStickButton, controls);

                // D-Pad
                AddControls(source.dpad, controls);

                // Do not create base Gamepad controls
                //base.OnCreateControls(controls);
            }

            protected override bool ExcludeControl(UnityEngine.InputSystem.InputControl control) {
                if (base.ExcludeControl(control)) return true;

                // Exclude redundant buttons that activate at 0.5 for both triggers in DualShockGamepadHID
                if (control.path.EndsWith("leftTriggerButton", System.StringComparison.OrdinalIgnoreCase) || 
                    control.path.EndsWith("rightTriggerButton", System.StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
                return false;
            }

            protected override void OnInitializationFinished() {
                base.OnInitializationFinished();

                // Renew vibration before it times out so it continues forever
                GetComponent<DualMotorVibrationComponent>().renewVibrationInterval = VIBRATION_RENEW_INTERVAL;
            }

            public void SetLightBarColor(UnityEngine.Color color) {
                source.SetLightBarColor(color);
            }

            new public class InitOptions : Gamepad.InitOptions {

                public DualShockGamepadType dualShockGamepadType { get; set; }

                public InitOptions(UnityEngine.InputSystem.DualShock.DualShockGamepad dualShockGamepad)
                    : base(dualShockGamepad) {
                }
            }

            public enum DualShockGamepadType {
                None = 0,
                DualShock3 = 1,
                DualShock4 =2 ,
                DualSense = 3
            }
        }
    }
}

#endif

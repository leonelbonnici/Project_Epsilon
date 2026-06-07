// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        protected class Gamepad : Joystick {

            private const string tag = "Gamepad";

            public Gamepad(InitOptions initOptions) : base(initOptions) {
                AddMatchingTag(tag);
            }

            protected override void OnCreateControls(System.Collections.Generic.List<Control> controls) {

                UnityEngine.InputSystem.Gamepad source = (UnityEngine.InputSystem.Gamepad)inputDevice;

                // Create in fixed order so this is consistent across all platforms
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
                AddControls(source.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("systemButton"), controls); // in case Guide/PS/etc is supported
                AddControls(source.leftStickButton, controls);
                AddControls(source.rightStickButton, controls);

                // D-Pad
                AddControls(source.dpad, controls);

                base.OnCreateControls(controls);
            }

            protected override void OnCreateComponents(System.Collections.Generic.IList<Component> components) {

                // Add a dual motor vibration component only if another vibration component hasn't already been added upstream.
                // Controllers with more motors like Xbox One will create their own vibration components.
                if (Rewired.Utils.ListTools.Find(components, x => x is Rewired.Platforms.Custom.ControllerComponents.IVibrationComponent) == null) {
                    components.Add(new DualMotorVibrationComponent(this, inputDevice));
                }

                base.OnCreateComponents(components);
            }

            protected override void OnCreateExtensions(System.Collections.Generic.IList<Rewired.Controller.Extension> extensions) {

                extensions.Add(
                    new Rewired.Platforms.UnityInputSystem.UnityInputSystemGamepadExtension(
                        (UnityEngine.InputSystem.Gamepad)inputDevice,
                        GetComponent<IntensityVibrationComponent>()
                    )
                );

                base.OnCreateExtensions(extensions);
            }

            new public class InitOptions : Joystick.InitOptions {

                public InitOptions(UnityEngine.InputSystem.Gamepad gamepad)
                    : base(UnityInputSystemDeviceType.Gamepad, gamepad) {
                }
            }
        }
    }
}

#endif

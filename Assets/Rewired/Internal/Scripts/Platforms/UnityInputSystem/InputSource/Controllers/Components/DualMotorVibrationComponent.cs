// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public sealed class DualMotorVibrationComponent : IntensityVibrationComponent {

            private const int MOTOR_COUNT = 2;
            
            private readonly UnityEngine.InputSystem.InputDevice _inputDevice;
            private readonly UnityEngine.InputSystem.Haptics.IDualMotorRumble _dualMotorRumble;
            private float _renewVibrationInterval;

            public float renewVibrationInterval {
                get {
                    return _renewVibrationInterval;
                }
                set {
                    if (value < 0f) value = 0f;
                    _renewVibrationInterval = value;
                    for (int i = 0; i < motorCount; i++) {
                        GetMotor(i).renewInterval = value;
                    }
                }
            }

            public DualMotorVibrationComponent(
                Rewired.Platforms.Custom.CustomInputSource.Controller controller,
                UnityEngine.InputSystem.InputDevice inputDevice
            ) : base(MOTOR_COUNT, controller) {
                if (inputDevice as UnityEngine.InputSystem.Haptics.IDualMotorRumble == null) throw new System.ArgumentNullException("inputDevice");
                _dualMotorRumble = (UnityEngine.InputSystem.Haptics.IDualMotorRumble)inputDevice;
                _inputDevice = inputDevice;
            }

            protected override void OnCommitVibration() {
                // Hack to prevent errors from being logged if SetMotorSpeeds is called after a controller is disconnected
                if (!_inputDevice.added) return;
                _dualMotorRumble.SetMotorSpeeds(
                    GetVibration(0).intensity,
                    GetVibration(1).intensity
                );
            }

            private static bool IsConnected(UnityEngine.InputSystem.InputDevice device) {
                if (device == null) return false;
                return device.added;
            }
        }
    }
}

#endif

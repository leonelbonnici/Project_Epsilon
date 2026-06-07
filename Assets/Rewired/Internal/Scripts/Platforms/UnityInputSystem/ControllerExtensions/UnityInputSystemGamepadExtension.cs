// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    /// <summary>
    /// Provides access to information exposed by Unity Input System for a Gamepad.
    /// </summary>
    public sealed class UnityInputSystemGamepadExtension :
        UnityInputSystemControllerExtension,
        Rewired.Interfaces.IControllerVibrator {

        private readonly Rewired.Platforms.Custom.ControllerComponents.IIntensityVibrationComponent _vibrationComponent;

        /// <summary>
        /// The underlying Gamepad.
        /// </summary>
        public UnityEngine.InputSystem.Gamepad gamepad {
            get {
                return ((Source)GetSource()).getInputDevice() as UnityEngine.InputSystem.Gamepad;
            }
        }

        public UnityInputSystemGamepadExtension(
            UnityEngine.InputSystem.Gamepad gamepad,
            Rewired.Platforms.Custom.ControllerComponents.IIntensityVibrationComponent vibrationComponent
        ) : base(new Source(gamepad)) {
            if (gamepad == null) throw new System.ArgumentNullException("gamepad");
            _vibrationComponent = vibrationComponent;
        }
        private UnityInputSystemGamepadExtension(UnityInputSystemGamepadExtension other) : base(other) {
            _vibrationComponent = other._vibrationComponent;
        }

        public override Controller.Extension ShallowCopy() {
            return new UnityInputSystemGamepadExtension(this);
        }

        // Unity gamepad implements vibration even if it doesn't actually support it

#region IControllerVibrator

        int Rewired.Interfaces.IControllerVibrator.vibrationMotorCount {
            get {
                return _vibrationComponent != null ? _vibrationComponent.motorCount : 0;
            }
        }

        void Rewired.Interfaces.IControllerVibrator.SetVibration(int motorIndex, float motorLevel) {
            ((Rewired.Interfaces.IControllerVibrator)this).SetVibration(motorIndex, motorLevel, 0f, false);
        }

        void Rewired.Interfaces.IControllerVibrator.SetVibration(int motorIndex, float motorLevel, float duration) {
            ((Rewired.Interfaces.IControllerVibrator)this).SetVibration(motorIndex, motorLevel, duration, false);
        }

        void Rewired.Interfaces.IControllerVibrator.SetVibration(int motorIndex, float motorLevel, bool stopOtherMotors) {
            ((Rewired.Interfaces.IControllerVibrator)this).SetVibration(motorIndex, motorLevel, 0f, stopOtherMotors);
        }

        void Rewired.Interfaces.IControllerVibrator.SetVibration(int motorIndex, float motorLevel, float duration, bool stopOtherMotors) {
            if (_vibrationComponent == null) return;
            _vibrationComponent.SetVibration(new Custom.ControllerComponents.IntensityVibrationCommand() {
                motorIndex = motorIndex,
                vibration = new Custom.ControllerComponents.IntensityVibration(motorLevel),
                duration = duration,
                stopOtherMotors = stopOtherMotors
            });
        }

        float Rewired.Interfaces.IControllerVibrator.GetVibration(int motorIndex) {
            if (_vibrationComponent == null) return 0f;
            return _vibrationComponent.GetVibration(motorIndex).intensity;
        }

        void Rewired.Interfaces.IControllerVibrator.StopVibration() {
            if (_vibrationComponent == null) return;
            _vibrationComponent.StopVibration();
        }

#endregion

        new private class Source : UnityInputSystemControllerExtension.Source {

            public Source(UnityEngine.InputSystem.Gamepad gamepad) : base(() => gamepad) {
            }
        }
    }
}

#endif

// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        new protected sealed class UnifiedMouse : UnityInputSystemInputSourceBase.UnifiedMouse, IUnityInputSystemInputDevice {

            private const string tag = "UnifiedMouse";

            private Button[] _buttons;
            private Vector2Element _screenPosition;

            private UnityEngine.InputSystem.Mouse source { get { return UnityEngine.InputSystem.Mouse.current; } }

            public UnityEngine.InputSystem.InputDevice inputDevice { get { return source; } }

            public UnifiedMouse() : base(new UnityInputSystemInputSourceBase.UnifiedMouse.InitOptions()) {
                _isConnected = true; // start connected
                systemId = this.id;
                AddMatchingTag(tag);
            }

            protected override void OnCreateComponents(System.Collections.Generic.IList<Component> components) {
                base.OnCreateComponents(components);
                components.Add(new InputDeviceInfoComponent(this, () => source));
            }

            protected override void OnCreateExtensions(System.Collections.Generic.IList<Rewired.Controller.Extension> extensions) {
                base.OnCreateExtensions(extensions);

                // Create base device extension
                extensions.Add(
                    new Rewired.Platforms.UnityInputSystem.UnityInputSystemUnifiedMouseExtension()
                );
            }

            protected override void OnInitializationFinished() {
                base.OnInitializationFinished();
                // Cache some elements locally for speed
                System.Collections.Generic.List<Button> buttons = new System.Collections.Generic.List<Button>(fixedButtonCount);
                GetElements(buttons);
                _buttons = buttons.ToArray();
                _screenPosition = GetElement<Vector2Element>(vector2Index_screenPosition);
            }

            public override void Update() {

                UnityEngine.InputSystem.Mouse mouse = source;
                if (mouse == null) return;

                UnityEngine.Vector2 xy = mouse.delta != null ? mouse.delta.value : new UnityEngine.Vector2();
                UnityEngine.Vector2 wheel = mouse.scroll != null ? mouse.scroll.value : new UnityEngine.Vector2();

                SetAxisValue(axisIndex_x, xy.x * UnityInputSystemHelper.mouseXYDeltaMultiplier);
                SetAxisValue(axisIndex_y, xy.y * UnityInputSystemHelper.mouseXYDeltaMultiplier);
                SetAxisValue(axisIndex_wheelY, wheel.y);
                SetAxisValue(axisIndex_wheelX, wheel.x);

                UnityInputSystemInputSource.UpdateButtonValues(_buttons[buttonIndex_left], mouse.leftButton);
                UnityInputSystemInputSource.UpdateButtonValues(_buttons[buttonIndex_right], mouse.rightButton);
                UnityInputSystemInputSource.UpdateButtonValues(_buttons[buttonIndex_middle], mouse.middleButton);
                UnityInputSystemInputSource.UpdateButtonValues(_buttons[buttonIndex_back], mouse.backButton);
                UnityInputSystemInputSource.UpdateButtonValues(_buttons[buttonIndex_forward], mouse.forwardButton);

                // Screen position
                _screenPosition.SetValue(mouse.position.value);
            }

            protected override int GetElementInfo(System.Collections.Generic.IList<ControllerElementInfo> results) {
                // Not supported. Path might not be the same for all mice?
                return 0;
            }
        }
    }
}

#endif

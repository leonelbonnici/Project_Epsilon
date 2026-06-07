// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        new protected sealed class UnifiedKeyboard : UnityInputSystemInputSourceBase.UnifiedKeyboard, IUnityInputSystemInputDevice {

            private const string tag = "UnifiedKeyboard";

            private Key[] _keys;
            private string _layout;
            private bool _showSystemKeyLabels;

            private UnityEngine.InputSystem.Keyboard source { get { return UnityEngine.InputSystem.Keyboard.current; } }

            public UnityEngine.InputSystem.InputDevice inputDevice { get { return source; } }

            public UnifiedKeyboard(InitOptions initOptions) : base(initOptions) {
                _showSystemKeyLabels = initOptions.showSystemKeyLabels;
                _isConnected = true; // start connected
                _keys = new Key[fixedKeyCount];
                systemId = this.id;
                AddMatchingTag(tag);
            }

            protected override void OnCreateElements(System.Collections.Generic.IList<Element> elements) {
                // Do not call create elements in base because elements are being created here of a different type
                for (int i = 0; i < fixedKeyCount; i++) {
                    _keys[i] = new Key();
                    elements.Add(_keys[i]);
                }
            }

            protected override void OnCreateComponents(System.Collections.Generic.IList<Component> components) {
                base.OnCreateComponents(components);
                components.Add(new InputDeviceInfoComponent(this, () => source));
            }

            protected override void OnCreateExtensions(System.Collections.Generic.IList<Rewired.Controller.Extension> extensions) {
                base.OnCreateExtensions(extensions);

                // Create base device extension
                extensions.Add(
                    new Rewired.Platforms.UnityInputSystem.UnityInputSystemUnifiedKeyboardExtension()
                );
            }

            public override void Update() {

                UnityEngine.InputSystem.Keyboard keyboard = source;
                if (keyboard == null) return;

                // Check if keyboard layout changed
                string keyboardLayout = source.keyboardLayout;
                bool layoutChanged = false;
                if (!string.Equals(keyboardLayout, _layout, System.StringComparison.Ordinal)) {
                    _layout = keyboardLayout;
                    layoutChanged = true;
                }
                
                if (_showSystemKeyLabels != Rewired.ReInput.configuration.showSystemKeyLabels) {
                    _showSystemKeyLabels = !_showSystemKeyLabels;
                    layoutChanged = true;
                }

                UnityEngine.InputSystem.Key nisKey;
                Rewired.Keyboard rewiredKeyboard = Rewired.ReInput.controllers.Keyboard;
                Rewired.KeyboardKeyCode keyCode;

                for (int i = 0; i < fixedKeyCount; i++) {
                    keyCode = rewiredKeyboard.GetKeyCodeByButtonIndex(i);
                    nisKey = UnityInputSystemHelper.ToUnityInputSystemKey(keyCode);
                    if (nisKey != UnityEngine.InputSystem.Key.None) {
                        UnityInputSystemInputSource.UpdateButtonValues(_keys[i], keyboard[nisKey]);
                    }

                    // Update display name of key when layout changes
                    if (layoutChanged && nisKey != UnityEngine.InputSystem.Key.None) {
                        _keys[i].displayName = _showSystemKeyLabels ? keyboard[nisKey].displayName : null;
                        SetKeyLabel(keyCode, _keys[i].displayName);
                    }
                }

                // Update generic modifier key labels
                if (layoutChanged) {
                    if (_showSystemKeyLabels) {
                        SetKeyLabel(Rewired.ModifierKey.Shift, keyboard.shiftKey.displayName, keyboard.shiftKey.displayName);
                        SetKeyLabel(Rewired.ModifierKey.Alt, keyboard.altKey.displayName, keyboard.altKey.displayName);
                        SetKeyLabel(
                            Rewired.ModifierKey.Control,
                            string.Equals(keyboard.ctrlKey.displayName, "Control") ? "Ctrl" : keyboard.ctrlKey.displayName,
                            keyboard.ctrlKey.displayName
                        );
                    } else {
                        SetKeyLabel(Rewired.ModifierKey.Shift, null, null);
                        SetKeyLabel(Rewired.ModifierKey.Alt, null, null);
                        SetKeyLabel(Rewired.ModifierKey.Control, null, null);
                    }
                }
            }

            protected override int GetElementInfo(System.Collections.Generic.IList<ControllerElementInfo> results) {
                // Not supported. Path might not be the same for all keyboards?
                return 0;
            }

            private class Key : Rewired.Platforms.Custom.CustomInputSource.Button {

                private string _displayName;

                public string displayName {
                    get { return _displayName; }
                    set { _displayName = value; }
                }

                public Key() {
                }
            }

            new public sealed class InitOptions : UnityInputSystemInputSourceBase.UnifiedKeyboard.InitOptions {

                public bool showSystemKeyLabels { get; private set; }

                public InitOptions(bool showSystemKeyLabels)
                    : base() {
                    this.showSystemKeyLabels = showSystemKeyLabels;
                }
            }
        }
    }
}

#endif

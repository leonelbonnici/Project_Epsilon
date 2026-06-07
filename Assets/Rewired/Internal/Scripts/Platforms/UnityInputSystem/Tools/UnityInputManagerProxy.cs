// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    internal sealed class UnityInputManagerProxy : Rewired.Platforms.UnityInputManager.UnityInputManagerProxyBase {

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod()]
#endif
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StaticInitialize() {
            Rewired.Internal.GlobalObjectProvider.Register(Rewired.Internal.GlobalObjectProvider.key_createUnityInputManagerProxy, CreateInstanceForObjectProvider);
        }

        private static bool CreateInstanceForObjectProvider(object arg1, object arg2, out object result) {
            result = null;
            if (!(arg1 is int)) return false;
            int inputSourceId = (int)arg1;
            if (inputSourceId != 10) return false;
            result = new UnityInputManagerProxy();
            return true;
        }

        private static readonly int keyboardKeyCount = System.Enum.GetValues(typeof(Rewired.KeyboardKeyCode)).Length;
        private static readonly int keyboardModifierKeyTypeCount = System.Enum.GetValues(typeof(Rewired.ModifierKey)).Length;
        private string _lastKeyboardLayout;
        private string[] _keyLabels;
        private string[] _modifierLabelsShort;
        private string[] _modifierLabelsLong;
        private readonly TouchHelper _touchHelper;
        private bool _hadMouse;
        private bool _hadKeyboard;
        private bool _showSystemKeyLabels;

        public System.Action _KeyboardLayoutChangedEvent;
        public override event System.Action KeyboardLayoutChangedEvent {
            add {
                _KeyboardLayoutChangedEvent += value;
            }
            remove {
                _KeyboardLayoutChangedEvent -= value;
            }
        }

        private System.Collections.Generic.Dictionary<string, System.Func<float>> __axes;
        private System.Collections.Generic.Dictionary<string, System.Func<float>> axes {
            get {
                if (__axes == null) {
                    __axes = new System.Collections.Generic.Dictionary<string, System.Func<float>>() {
                        { "MouseAxis1", () => GetMouseAxis(mouse, Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.axisIndex_x) * UnityInputSystemHelper.mouseXYDeltaMultiplier },
                        { "MouseAxis2", () => GetMouseAxis(mouse, Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.axisIndex_y) * UnityInputSystemHelper.mouseXYDeltaMultiplier },
                        { "MouseAxis3", () => GetMouseAxis(mouse, Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.axisIndex_wheelY) }
                    };
                }
                return __axes;
            }
        }

        private System.Collections.Generic.Dictionary<string, System.Func<ButtonFlags>> __buttons;
        private System.Collections.Generic.Dictionary<string, System.Func<ButtonFlags>> buttons {
            get {
                if (__buttons == null) {
                    __buttons = new System.Collections.Generic.Dictionary<string, System.Func<ButtonFlags>>() {
                        { "MouseButton0", () => GetButtonFlags(mouse, 0) },
                        { "MouseButton1", () => GetButtonFlags(mouse, 1) },
                        { "MouseButton2", () => GetButtonFlags(mouse, 2) },
                        { "MouseButton3", () => GetButtonFlags(mouse, 3) },
                        { "MouseButton4", () => GetButtonFlags(mouse, 4) },
                        { "MouseButton5", () => GetButtonFlags(mouse, 5) },
                        { "MouseButton6", () => GetButtonFlags(mouse, 6) }
                    };
                }
                return __buttons;
            }
        }

        public UnityInputManagerProxy() : base() {
            UnityInputSystemHelper.SyncToRewiredSettings();
            _keyLabels = new string[keyboardKeyCount];
            _modifierLabelsShort = new string[keyboardModifierKeyTypeCount];
            _modifierLabelsLong = new string[keyboardModifierKeyTypeCount];
            _touchHelper = new TouchHelper();
            _showSystemKeyLabels = Rewired.ReInput.configuration.showSystemKeyLabels;
        }

        private bool hasMouse {
            get {
                return UnityEngine.InputSystem.Mouse.current != null;
            }
        }

        private UnityEngine.InputSystem.Mouse mouse {
            get {
                return UnityEngine.InputSystem.Mouse.current;
            }
        }

        public override string inputSourceTypeString {
            get {
                return "UnityInputSystem";
            }
        }

        public override UnityEngine.Vector2 mousePosition {
            get {
                return hasMouse ? UnityInputSystemHelper.GetValue(mouse.position) : new UnityEngine.Vector2();
            }
        }

        public override UnityEngine.Vector2 mouseScrollDelta {
            get {
                return hasMouse ? UnityInputSystemHelper.GetValue(mouse.scroll) : new UnityEngine.Vector2();
            }
        }

        public override bool anyKey {
            get {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard == null) return false;
                return UnityInputSystemHelper.GetValue(keyboard.anyKey);
            }
        }

        public override bool mousePresent {
            get { return hasMouse; }
        }

        public override UnityEngine.Vector3 acceleration {
            get {
                UnityEngine.InputSystem.Accelerometer accelerometer = UnityEngine.InputSystem.Accelerometer.current;
                if (accelerometer == null) return new UnityEngine.Vector3();
                if (!accelerometer.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(accelerometer);
                return UnityInputSystemHelper.GetValue(accelerometer.acceleration);
            }
        }

        public override bool touchSupported {
            get {
                return UnityEngine.InputSystem.Touchscreen.current != null;
            }
        }

        public override int touchCount {
            get {
                return _touchHelper.touchCount;
            }
        }

        public override Rewired.UnityTouch[] touches {
            get {
                int touchCount = this.touchCount;
                Rewired.UnityTouch[] r = new Rewired.UnityTouch[touchCount];
                for (int i = 0; i < touchCount; i++) {
                    r[i] = GetTouch(i);
                }
                return r;
            }
        }

        public override bool simulateMouseWithTouches {
            get { return false; }
            set { }
        }

        public override bool multiTouchEnabled {
            get { return true; }
            set { }
        }

        public override bool GetKey(int keyCode) {
            UnityEngine.InputSystem.Key key = UnityInputSystemHelper.ToUnityInputSystemKey((KeyboardKeyCode)keyCode);
            if (key == UnityEngine.InputSystem.Key.None) return false;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return false;
            return UnityInputSystemHelper.GetValue(keyboard[key]);
        }

        public override bool GetKeyDown(int keyCode) {
            UnityEngine.InputSystem.Key key = UnityInputSystemHelper.ToUnityInputSystemKey((KeyboardKeyCode)keyCode);
            if (key == UnityEngine.InputSystem.Key.None) return false;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return false;
            return UnityInputSystemHelper.GetValueDown(keyboard[key]);
        }

        public override bool GetKeyUp(int keyCode) {
            UnityEngine.InputSystem.Key key = UnityInputSystemHelper.ToUnityInputSystemKey((KeyboardKeyCode)keyCode);
            if (key == UnityEngine.InputSystem.Key.None) return false;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return false;
            return UnityInputSystemHelper.GetValueUp(keyboard[key]);
        }

        public override bool GetButton(string name) {
            System.Func<ButtonFlags> value;
            if (!buttons.TryGetValue(name, out value)) {
                return false;
            }
            ButtonFlags flags = value();
            return (flags & ButtonFlags.On) != 0;
        }

        public override bool GetButtonDown(string name) {
            System.Func<ButtonFlags> value;
            if (!buttons.TryGetValue(name, out value)) {
                return false;
            }
            ButtonFlags flags = value();
            return (flags & ButtonFlags.Down) != 0;
        }

        public override bool GetButtonUp(string name) {
            System.Func<ButtonFlags> value;
            if (!buttons.TryGetValue(name, out value)) {
                return false;
            }
            ButtonFlags flags = value();
            return (flags & ButtonFlags.Up) != 0;
        }

        public override bool GetMouseButton(int index) {
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            switch (index) {
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_left:
                    return mouse.leftButton.isPressed;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_right:
                    return mouse.rightButton.isPressed;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_middle:
                    return mouse.middleButton.isPressed;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_back:
                    return mouse.backButton.isPressed;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_forward:
                    return mouse.forwardButton.isPressed;
                default:
                    return false;
            }
        }

        public override bool GetMouseButtonDown(int index) {
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            switch (index) {
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_left:
                    return mouse.leftButton.wasPressedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_right:
                    return mouse.rightButton.wasPressedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_middle:
                    return mouse.middleButton.wasPressedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_back:
                    return mouse.backButton.wasPressedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_forward:
                    return mouse.forwardButton.wasPressedThisFrame;
                default:
                    return false;
            }
        }

        public override bool GetMouseButtonUp(int index) {
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            switch (index) {
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_left:
                    return mouse.leftButton.wasReleasedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_right:
                    return mouse.rightButton.wasReleasedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_middle:
                    return mouse.middleButton.wasReleasedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_back:
                    return mouse.backButton.wasReleasedThisFrame;
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.buttonIndex_forward:
                    return mouse.forwardButton.wasReleasedThisFrame;
                default:
                    return false;
            }
        }

        public override float GetAxis(string name) {
            System.Func<float> value;
            if (axes.TryGetValue(name, out value)) {
                return value();
            }
            return 0f;
        }

        public override float GetAxisRaw(string name) {
            return GetAxis(name);
        }

        public override Rewired.UnityTouch GetTouch(int index) {
            return _touchHelper.GetTouch(index);
        }

        public override int GetJoystickNames(System.Collections.Generic.IList<string> results) {
            return 0;
        }

        public override bool GetKeyLabels(string[] keyLabels, string[] modifierLabelsShort, string[] modifierLabelsLong) {
            if (keyLabels.Length != _keyLabels.Length ||
                modifierLabelsShort.Length != _modifierLabelsShort.Length ||
                modifierLabelsLong.Length != _modifierLabelsLong.Length
            ) {
                UnityEngine.Debug.LogError("Rewired: Array size does not match.");
                return false;
            }
            System.Array.Copy(_keyLabels, keyLabels, _keyLabels.Length);
            System.Array.Copy(_modifierLabelsShort, modifierLabelsShort, _modifierLabelsShort.Length);
            System.Array.Copy(_modifierLabelsLong, modifierLabelsLong, _modifierLabelsLong.Length);
            return true;
        }

        public override void Update() {
            UnityInputSystemHelper.Update();
            UpdateKeyboardLayout();
            _touchHelper.Update();
        }

        private void UpdateKeyboardLayout() {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            // Check if keyboard layout changed
            string keyboardLayout = keyboard.keyboardLayout;
            bool layoutChanged = false;
            if (!string.Equals(keyboardLayout, _lastKeyboardLayout, System.StringComparison.Ordinal)) {
                _lastKeyboardLayout = keyboardLayout;
                layoutChanged = true;
            }
            
            if (_showSystemKeyLabels != ReInput.configuration.showSystemKeyLabels) {
                _showSystemKeyLabels = !_showSystemKeyLabels;
                layoutChanged = true;
            }

            if (!layoutChanged) return;

            UnityEngine.InputSystem.Key nisKey;
            Rewired.Keyboard rewiredKeyboard = Rewired.ReInput.controllers.Keyboard;
            Rewired.KeyboardKeyCode keyCode;

            for (int i = 0; i < keyboardKeyCount; i++) {
                keyCode = rewiredKeyboard.GetKeyCodeByButtonIndex(i);
                nisKey = UnityInputSystemHelper.ToUnityInputSystemKey(keyCode);

                // Update display name of key when layout changes
                if (layoutChanged && nisKey != UnityEngine.InputSystem.Key.None) {
                    if (_showSystemKeyLabels) _keyLabels[i] = keyboard[nisKey].displayName;
                    else _keyLabels[i] = null; // use default
                }
            }

            // Update generic modifier key labels
            if (layoutChanged) {

                const int indexControl = 1;
                const int indexAlt = 2;
                const int indexShift = 3;
                //const int indexCommand = 4;

                if (_showSystemKeyLabels) {
                    _modifierLabelsShort[indexControl] = string.Equals(keyboard.ctrlKey.displayName, "Control") ? "Ctrl" : keyboard.ctrlKey.displayName;
                    _modifierLabelsLong[indexControl] = keyboard.ctrlKey.displayName;

                    _modifierLabelsShort[indexAlt] = keyboard.altKey.displayName;
                    _modifierLabelsLong[indexAlt] = keyboard.altKey.displayName;

                    _modifierLabelsShort[indexShift] = keyboard.shiftKey.displayName;
                    _modifierLabelsLong[indexShift] = keyboard.shiftKey.displayName;

                    // Command not supported
                    //_modifierLabelsShort[indexCommand] = ;
                    //_modifierLabelsLong[indexCommand] = ;

                } else {

                    // Use defaults
                    _modifierLabelsShort[indexControl] = null;
                    _modifierLabelsLong[indexControl] = null;

                    _modifierLabelsShort[indexAlt] = null;
                    _modifierLabelsLong[indexAlt] = null;

                    _modifierLabelsShort[indexShift] = null;
                    _modifierLabelsLong[indexShift] = null;
                }

                // Send layout changed event
                var evt = _KeyboardLayoutChangedEvent;
                if (evt != null) {
                    evt();
                }
            }
        }

        // Static

        private static Rewired.UnityTouch ToRewired(UnityEngine.InputSystem.LowLevel.TouchState touchState) {
            Rewired.UnityTouch r = new Rewired.UnityTouch();
            r.fingerId = touchState.touchId;
            r.position = touchState.position;
            r.rawPosition = touchState.startPosition;
            r.deltaPosition = touchState.delta;
            r.deltaTime = 0f; // timestamp not available in TouchState
            r.tapCount = touchState.tapCount;
            r.phase = ToRewired(touchState.phase);
            r.pressure = touchState.pressure;
            r.maximumPossiblePressure = 1f;
            r.type = touchState.isIndirectTouch ? Rewired.UnityTouch.TouchType.Indirect : Rewired.UnityTouch.TouchType.Direct;
            r.altitudeAngle = 0f;
            r.azimuthAngle = 0f;
            r.radius = UnityEngine.Mathf.Max(touchState.radius.x, touchState.radius.y);
            r.radiusVariance = 0f;
            return r;
        }

        private static Rewired.UnityTouch.TouchPhase ToRewired(UnityEngine.InputSystem.TouchPhase touchPhase) {
            switch (touchPhase) {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    return Rewired.UnityTouch.TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    return Rewired.UnityTouch.TouchPhase.Canceled;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    return Rewired.UnityTouch.TouchPhase.Ended;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    return Rewired.UnityTouch.TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    return Rewired.UnityTouch.TouchPhase.Stationary;
                default:
                    return Rewired.UnityTouch.TouchPhase.Canceled;

            }
        }

        private ButtonFlags GetButtonFlags(UnityEngine.InputSystem.Mouse mouse, int index) {
            if (mouse == null) return ButtonFlags.None;
            UnityEngine.InputSystem.Controls.ButtonControl control;
            switch (index) {
                case 0:
                    control = mouse.leftButton;
                    break;
                case 1:
                    control = mouse.rightButton;
                    break;
                case 2:
                    control = mouse.middleButton;
                    break;
                case 3:
                    control = mouse.backButton;
                    break;
                case 4:
                    control = mouse.forwardButton;
                    break;
                default:
                    return ButtonFlags.None;
            }

            return GetButtonFlags(control);
        }

        private ButtonFlags GetButtonFlags(UnityEngine.InputSystem.Controls.ButtonControl control) {
            ButtonFlags flags = ButtonFlags.None;
            if (control == null) return flags;
            if (control.isPressed) flags |= ButtonFlags.On;
            if (control.wasPressedThisFrame) flags |= ButtonFlags.Down;
            if (control.wasReleasedThisFrame) flags |= ButtonFlags.Up;
            return flags;
        }

        private float GetMouseAxis(UnityEngine.InputSystem.Mouse mouse, int index) {
            if (mouse == null) return 0f;
            switch (index) {
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.axisIndex_x:
                    return UnityInputSystemHelper.GetValue(mouse.delta.x);
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.axisIndex_y:
                    return UnityInputSystemHelper.GetValue(mouse.delta.y);
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.axisIndex_wheelX:
                    return UnityInputSystemHelper.GetValue(mouse.scroll.x);
                case Rewired.Platforms.Custom.CustomInputSource.UnifiedMouse.axisIndex_wheelY:
                    return UnityInputSystemHelper.GetValue(mouse.scroll.y);
            }
            return 0f;
        }

        private enum ButtonFlags {
            None = 0,
            On = 1,
            Down = 2,
            Up = 3
        }

        private struct TouchEvent {
            public UnityEngine.InputSystem.LowLevel.TouchState touchState;
            public double lastActiveTime;
            public bool isActive;
            public bool pendingExpiration;
        }

        // Tracks touches because UnityEngine.InputSystem does not expose enough
        // information in the touch state to determine that the touch just ended or was canceled.
        // Phase goes to ended and remains there permanently.
        private class TouchHelper {

            private const int maxHistoryCount = 10;

            private readonly System.Collections.Generic.List<TouchEvent> _history;

            public int touchCount {
                get {
                    int count = 0;
                    for (int i = 0; i < _history.Count; i++) {
                        if (_history[i].isActive) count += 1;
                    }
                    return count;
                }
            }

            public TouchHelper() {
                _history = new System.Collections.Generic.List<TouchEvent>(maxHistoryCount);
            }

            public Rewired.UnityTouch GetTouch(int index) {
                int count = 0;
                for (int i = 0; i < _history.Count; i++) {
                    if (_history[i].isActive) {
                        if (index == count) return ToRewired(_history[i].touchState);
                        count += 1;
                    }
                }
                throw new System.ArgumentOutOfRangeException("index");
            }

            public void Update() {

                UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
                if (touchscreen == null) {
                    _history.Clear();
                } else {

                    double currentTime = UnityEngine.Time.unscaledTimeAsDouble;
                    var touches = touchscreen.touches;

                    // Remove no longer valid touches first
                    for (int i = _history.Count - 1; i >= 0; i--) {
                        if (!ContainsTouchId(touches, _history[i].touchState.touchId)) {
                            _history.RemoveAt(i);
                        }
                    }

                    // Update touches
                    for (int i = 0; i < touches.Count; i++) {
                        int index = IndexOf(touches[i].touchId.value);
                        if (index < 0) { // this is new
                            if (_history.Count >= maxHistoryCount) {
                                RemoveOldest(_history);
                            }
                            _history.Add(new TouchEvent() {
                                touchState = touches[i].value,
                                lastActiveTime = currentTime,
                                isActive = IsActivePhase(touches[i].phase.value)
                            });
                        } else { // update existing entry
                            TouchEvent prev = _history[index];
                            bool isCurrentActivePhase = IsActivePhase(touches[i].phase.value);
                            if (isCurrentActivePhase || (prev.isActive && !prev.pendingExpiration)) {
                                prev.touchState = touches[i].value;
                                prev.lastActiveTime = currentTime;
                                prev.isActive = true;
                                // Allow Ended / Canceled phase to remain active for one frame
                                prev.pendingExpiration = !isCurrentActivePhase;
                                _history[index] = prev;
                            } else if (prev.pendingExpiration) { // expire
                                prev.touchState = touches[i].value;
                                prev.isActive = false;
                                prev.pendingExpiration = false;
                                _history[index] = prev;
                            }
                        }
                    }
                }
            }

            private int IndexOf(int touchId) {
                for (int i = 0; i < _history.Count; i++) {
                    if (_history[i].touchState.touchId == touchId) return i;
                }
                return -1;
            }

            private static void RemoveOldest(System.Collections.Generic.List<TouchEvent> list) {
                int count = list.Count;
                if (count == 0) return;
                double oldestTime = double.MaxValue;
                int oldestIndex = 0; // remove 0 if no other found
                for (int i = 0; i < count; i++) {
                    if (list[i].lastActiveTime < oldestTime) {
                        oldestIndex = i;
                    }
                }
                list.RemoveAt(oldestIndex);
            }

            private static bool ContainsTouchId(UnityEngine.InputSystem.Utilities.ReadOnlyArray<UnityEngine.InputSystem.Controls.TouchControl> touches, int touchId) {
                int count = touches.Count;
                for (int i = 0; i < count; i++) {
                    if (touches[i].touchId.value == touchId) return true;
                }
                return false;
            }
        }

        private static bool IsActivePhase(UnityEngine.InputSystem.TouchPhase phase) {
            switch (phase) {
                case UnityEngine.InputSystem.TouchPhase.Began:
                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    return true;
            }
            return false;
        }
    }
}

#endif

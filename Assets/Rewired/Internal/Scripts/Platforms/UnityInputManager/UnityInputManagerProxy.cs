// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if UNITY_6000 || UNITY_6000_0_OR_NEWER
#define UNITY_6000_PLUS
#endif

#if UNITY_2023 || UNITY_6000_PLUS
#define UNITY_2023_PLUS
#endif

#if UNITY_2022 || UNITY_2023_PLUS
#define UNITY_2022_PLUS
#endif

#if UNITY_2021 || UNITY_2022_PLUS
#define UNITY_2021_PLUS
#endif

#if UNITY_2020 || UNITY_2021_PLUS
#define UNITY_2020_PLUS
#endif

#if UNITY_2019 || UNITY_2020_PLUS
#define UNITY_2019_PLUS
#endif

#if UNITY_2018 || UNITY_2019_PLUS
#define UNITY_2018_PLUS
#endif

#if UNITY_2017 || UNITY_2018_PLUS
#define UNITY_2017_PLUS
#endif

#if UNITY_5 || UNITY_2017_PLUS
#define UNITY_5_PLUS
#endif

#if UNITY_5_1 || UNITY_5_2 || UNITY_5_3_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_1_PLUS
#endif

#if UNITY_5_2 || UNITY_5_3_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_2_PLUS
#endif

#if UNITY_5_3_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_3_PLUS
#endif

#if UNITY_5_4_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_4_PLUS
#endif

#if UNITY_5_5_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_5_PLUS
#endif

#if UNITY_5_6_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_6_PLUS
#endif

#if UNITY_5_7_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_7_PLUS
#endif

#if UNITY_5_8_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_8_PLUS
#endif

#if UNITY_5_9_OR_NEWER || UNITY_2017_PLUS
#define UNITY_5_9_PLUS
#endif

#if (UNITY_2017_PLUS && !UNITY_2019_PLUS) || ENABLE_LEGACY_INPUT_MANAGER

namespace Rewired.Platforms.UnityInputManager {

    internal sealed class UnityInputManagerProxy : UnityInputManagerProxyBase {

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
            if (inputSourceId != 4) return false;
            result = new UnityInputManagerProxy();
            return true;
        }

        private int _joystickNamesLastFrameChecked;
        private string[] _joystickNames;

        public UnityInputManagerProxy() : base() {
            _joystickNamesLastFrameChecked = -1;
            _joystickNames = new string[0];
        }

        public override event System.Action KeyboardLayoutChangedEvent {
            add { }
            remove { }
        }

        public override string inputSourceTypeString {
            get {
                return "UnityInputManager";
            }
        }

        public override UnityEngine.Vector2 mousePosition {
            get { return UnityEngine.Input.mousePosition; }
        }

#if UNITY_5_PLUS
        public override UnityEngine.Vector2 mouseScrollDelta {
            get { return UnityEngine.Input.mouseScrollDelta; }
        }
#endif
        public override bool anyKey {
            get { return UnityEngine.Input.anyKey; }
        }

        public override bool mousePresent {
            get { return UnityEngine.Input.mousePresent; }
        }

        public override UnityEngine.Vector3 acceleration {
            get { return UnityEngine.Input.acceleration; }
        }

#if UNITY_5_PLUS
        public override bool touchSupported {
            get { return UnityEngine.Input.touchSupported; }
        }
#endif

        public override int touchCount {
            get { return UnityEngine.Input.touchCount; }
        }

        public override Rewired.UnityTouch[] touches {
            get {
                UnityEngine.Touch[] touches = UnityEngine.Input.touches;
                Rewired.UnityTouch[] r = new Rewired.UnityTouch[touches.Length];
                for (int i = 0; i < r.Length; i++) {
                    r[i] = ToRewired(touches[i]);
                }
                return r;
            }
        }

        public override bool simulateMouseWithTouches {
            get { return UnityEngine.Input.simulateMouseWithTouches; }
            set { UnityEngine.Input.simulateMouseWithTouches = value; }
        }

        public override bool multiTouchEnabled {
            get { return UnityEngine.Input.multiTouchEnabled; }
            set { UnityEngine.Input.multiTouchEnabled = value; }
        }


        public override bool GetKey(int keyCode) {
            return UnityEngine.Input.GetKey((UnityEngine.KeyCode)keyCode);
        }

        public override bool GetKeyDown(int keyCode) {
            return UnityEngine.Input.GetKeyDown((UnityEngine.KeyCode)keyCode);
        }

        public override bool GetKeyUp(int keyCode) {
            return UnityEngine.Input.GetKeyUp((UnityEngine.KeyCode)keyCode);
        }

        public override bool GetButton(string name) {
            return UnityEngine.Input.GetButton(name);
        }

        public override bool GetButtonDown(string name) {
            return UnityEngine.Input.GetButtonDown(name);
        }

        public override bool GetButtonUp(string name) {
            return UnityEngine.Input.GetButtonUp(name);
        }

        public override bool GetMouseButton(int index) {
            return UnityEngine.Input.GetMouseButton(index);
        }

        public override bool GetMouseButtonDown(int index) {
            return UnityEngine.Input.GetMouseButtonDown(index);
        }

        public override bool GetMouseButtonUp(int index) {
            return UnityEngine.Input.GetMouseButtonUp(index);
        }

        public override float GetAxis(string name) {
            return UnityEngine.Input.GetAxis(name);
        }

        public override float GetAxisRaw(string name) {
            return UnityEngine.Input.GetAxisRaw(name);
        }

        public override Rewired.UnityTouch GetTouch(int index) {
            return ToRewired(UnityEngine.Input.GetTouch(index));
        }

        public override int GetJoystickNames(System.Collections.Generic.IList<string> results) {
            if (results == null) throw new System.ArgumentNullException("results");
            UpdateJoystickNames();
            int origCount = results.Count;
            for (int i = 0; i < _joystickNames.Length; i++) {
                results.Add(_joystickNames[i]);
            }
            return results.Count - origCount;
        }

        public override bool GetKeyLabels(string[] keyLabels, string[] modifierLabelsShort, string[] modifierLabelsLong) {
            return false;
        }

        public override void Update() {
        }

        private void UpdateJoystickNames() {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _joystickNamesLastFrameChecked) return;
            _joystickNamesLastFrameChecked = frame;
            _joystickNames = UnityEngine.Input.GetJoystickNames();
        }

        private static Rewired.UnityTouch ToRewired(UnityEngine.Touch touch) {
            Rewired.UnityTouch r = new Rewired.UnityTouch();
            r.deltaPosition = touch.deltaPosition;
            r.deltaTime = touch.deltaTime;
            r.fingerId = touch.fingerId;
            r.phase = ToRewired(touch.phase);
            r.position = touch.position;
            r.rawPosition = touch.rawPosition;
            r.tapCount = touch.tapCount;
            r.type = ToRewired(touch.type);
            r.altitudeAngle = touch.altitudeAngle;
            r.azimuthAngle = touch.azimuthAngle;
            r.maximumPossiblePressure = touch.maximumPossiblePressure;
            r.pressure = touch.pressure;
            r.radius = touch.radius;
            r.radiusVariance = touch.radiusVariance;
            return r;
        }

        private static Rewired.UnityTouch.TouchPhase ToRewired(UnityEngine.TouchPhase touchPhase) {
            switch (touchPhase) {
                case UnityEngine.TouchPhase.Began:
                    return Rewired.UnityTouch.TouchPhase.Began;
                case UnityEngine.TouchPhase.Moved:
                    return Rewired.UnityTouch.TouchPhase.Moved;
                case UnityEngine.TouchPhase.Stationary:
                    return Rewired.UnityTouch.TouchPhase.Stationary;
                case UnityEngine.TouchPhase.Ended:
                    return Rewired.UnityTouch.TouchPhase.Ended;
                case UnityEngine.TouchPhase.Canceled:
                    return Rewired.UnityTouch.TouchPhase.Canceled;
                default:
                    return Rewired.UnityTouch.TouchPhase.Canceled;
            }
        }

        private static Rewired.UnityTouch.TouchType ToRewired(UnityEngine.TouchType touchType) {
            switch(touchType) {
                case UnityEngine.TouchType.Direct:
                    return Rewired.UnityTouch.TouchType.Direct;
                case UnityEngine.TouchType.Indirect:
                    return Rewired.UnityTouch.TouchType.Indirect;
                case UnityEngine.TouchType.Stylus:
                    return Rewired.UnityTouch.TouchType.Stylus;
                default:
                    return Rewired.UnityTouch.TouchType.Direct;
            }
        }
    }
}

#endif

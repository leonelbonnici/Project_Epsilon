// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

#pragma warning disable 0649

//#define REWIRED_DEBUG_THIS

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        new protected class Joystick : UnityInputSystemInputSourceBase.Joystick, IUnityInputSystemInputDevice {

            private UnityEngine.InputSystem.InputDevice _inputDevice;
            private readonly System.Collections.Generic.List<Control> _allControls;
            private readonly System.Collections.Generic.List<ButtonControl> _buttons;
            private readonly System.Collections.Generic.List<AxisControl> _axes;
            private readonly System.Collections.Generic.List<Vector2Control> _vector2Controls;
            private readonly System.Collections.Generic.List<Vector3Control> _vector3Controls;
            private readonly System.Collections.Generic.List<QuaternionControl> _quaternionControls;

            private readonly int _buttonCount;
            private readonly int _axisCount;
            private readonly int _vector2ControlCount;
            private readonly int _vector3ControlCount;
            private readonly int _quaternionControlCount;

            private UnityInputSystemDeviceMatchingProperties _matchingProperties;

            public UnityEngine.InputSystem.InputDevice inputDevice { get { return _inputDevice; } }

            public Joystick(InitOptions initOptions) : base(initOptions) {
                if (initOptions == null) throw new System.ArgumentNullException("initOptions");
                _inputDevice = initOptions.inputDevice;

                // Matching only to this device instance id. Approximate matching is disabled.
                // This requires disconnected controllers be retained in memory,
                // and that Unity Input System is able to return the same instance
                // for a device when reconnected. Sometimes it does, sometimes it doesn't.
                // Device id is always incremented by Unity when a device is reconnected
                // even if the instance is the same, so cannot use deviceId for this.
                // There doesn't appear to be any other unique identifier information
                // exposed by the device (system path, etc.). This would differ by
                // platform / input source anyway. This makes Rewired's ability to
                // match up previously-connected controllers back to Players dependent
                // upon Unity's specific implementation per platform / input source.
                if (!systemId.HasValue) {
                    systemId = this.id;
                }

                _allControls = new System.Collections.Generic.List<Control>();
                _buttons = new System.Collections.Generic.List<ButtonControl>();
                _axes = new System.Collections.Generic.List<AxisControl>();
                _vector2Controls = new System.Collections.Generic.List<Vector2Control>();
                _vector3Controls = new System.Collections.Generic.List<Vector3Control>();
                _quaternionControls = new System.Collections.Generic.List<QuaternionControl>();

                CreateControls();

                _buttonCount = _buttons != null ? _buttons.Count : 0;
                _axisCount = _axes != null ? _axes.Count : 0;
                _vector2ControlCount = _vector2Controls != null ? _vector2Controls.Count : 0;
                _vector3ControlCount = _vector3Controls != null ? _vector3Controls.Count : 0;
                _quaternionControlCount = _quaternionControls != null ? _quaternionControls.Count : 0;

                _deviceName = !string.IsNullOrEmpty(_inputDevice.description.product) ? _inputDevice.description.product : _inputDevice.displayName;

                // Add a matching tag that matches the input device class name in order to match to future device types by tag
                if (_inputDevice != null) {
                    System.Type rootType = typeof(UnityEngine.InputSystem.InputDevice); // this type will not be added
                    System.Type type = _inputDevice.GetType();
                    while (type != null && !object.ReferenceEquals(type, rootType)) {
                        AddMatchingTag(type.FullName); // use fully qualified name in case of overlap in different namespaces
                        type = type.BaseType;
                    }
                }
            }

            protected override void OnCreateElements(System.Collections.Generic.IList<Element> elements) {
                base.OnCreateElements(elements);

                Element element;

                for (int i = 0; i < _buttonCount; i++) {
                    element = new Button(
                        new Button.InitOptions() {
                            isPressureSensitive = _buttons[i].isPressureSensitive
                        }
                    );
                    AssignElement(_buttons[i], element);
                    elements.Add(element);
                }

                for (int i = 0; i < _axisCount; i++) {
                    element = new Axis(new Axis.InitOptions() { coordinateMode = _axes[i].coordinateMode });
                    AssignElement(_axes[i], element);
                    elements.Add(element);
                }

                for (int i = 0; i < _vector2ControlCount; i++) {
                    element = new Vector2Element(
                        new Vector2Element.InitOptions() { coordinateMode = _vector2Controls[i].coordinateMode });
                    AssignElement(_vector2Controls[i], element);
                    elements.Add(element);
                }

                for (int i = 0; i < _vector3ControlCount; i++) {
                    element = new Vector3Element(
                        new Vector3Element.InitOptions() { coordinateMode = _vector3Controls[i].coordinateMode });
                    AssignElement(_vector3Controls[i], element);
                    elements.Add(element);
                }

                for (int i = 0; i < _quaternionControlCount; i++) {
                    element = new QuaternionElement();
                    AssignElement(_quaternionControls[i], element);
                    elements.Add(element);
                }
            }

            protected override void OnCreateComponents(System.Collections.Generic.IList<Component> components) {
                base.OnCreateComponents(components);

                components.Add(new InputDeviceInfoComponent(this, () => _inputDevice));
            }

            protected override void OnCreateExtensions(System.Collections.Generic.IList<Rewired.Controller.Extension> extensions) {
                base.OnCreateExtensions(extensions);

                // Create base device extension
                // Do not add this if it was added upstream
                if (Rewired.Utils.ListTools.Find(extensions, x => x is Rewired.Platforms.UnityInputSystem.UnityInputSystemControllerExtension) == null) {
                    extensions.Add(
                        new Rewired.Platforms.UnityInputSystem.UnityInputSystemControllerExtension(_inputDevice)
                    );
                }
            }

            public override void Update() {

                // TODO: Can timestamp be obtained from Input System?

                for (int i = 0; i < _buttonCount; i++) {
                    _buttons[i].UpdateButtonValues();
                }

                for (int i = 0; i < _axisCount; i++) {
                    SetAxisValue(i, _axes[i].value);
                }

                {
                    Vector2Element element;
                    for (int i = 0; i < _vector2ControlCount; i++) {
                        element = _vector2Controls[i].element as Vector2Element;
                        if (element == null) continue;
                        element.SetValue(_vector2Controls[i].value);
                    }
                }

                {
                    Vector3Element element;
                    for (int i = 0; i < _vector3ControlCount; i++) {
                        element = _vector3Controls[i].element as Vector3Element;
                        if (element == null) continue;
                        element.SetValue(_vector3Controls[i].value);
                    }
                }

                {
                    QuaternionElement element;
                    for (int i = 0; i < _quaternionControlCount; i++) {
                        element = _quaternionControls[i].element as QuaternionElement;
                        if (element == null) continue;
                        element.SetValue(_quaternionControls[i].value);
                    }
                }
            }

            protected virtual bool ExcludeControl(UnityEngine.InputSystem.InputControl control) {
                if (control == null) return true;
                
                // Synthetic controls include things like "leftTriggerButton" in DualShock controllers and "isTracked" in XR controllers.
                // These are fake controls created by the Layout.
                // Exclude all synthetic controls for now because they are not real controls.
                if (control.synthetic) return true;

                return false;
            }

            public override UnityInputSystemDeviceMatchingProperties GetDeviceMatchingProperties() {
                if (_matchingProperties == null) {
                    _matchingProperties = new UnityInputSystemDeviceMatchingProperties() {
                        deviceName = _deviceName,
                        unityDeviceType = unityDeviceType
                    };
                    Rewired.Platforms.UnityInputSystem.UnityInputSystemHelper.HidPidVid hidPidVid;
                    if (Rewired.Platforms.UnityInputSystem.UnityInputSystemHelper.TryGetHidPidVid(_inputDevice, out hidPidVid)) {
                        _matchingProperties.vendorId = (ushort)hidPidVid.vendorId;
                        _matchingProperties.productId = (ushort)hidPidVid.productId;
                    }
                }
                return _matchingProperties;
            }

            // Used by sub-classes to create and sort elements in a fixed order
            // Any element created by a subclass will be skipped by this base class.
            // This is primarily to guarantee known layouts like DualShockGamepad have the same element orderings
            // on all platforms.
            protected virtual void OnCreateControls(System.Collections.Generic.List<Control> controls) {
            }

            protected virtual void OnControlAdded(Control control) {
            }

            private void CreateControls() {

                System.Collections.Generic.List<Control> subClassControls = new System.Collections.Generic.List<Control>();
                System.Collections.Generic.List<Control> localControls = new System.Collections.Generic.List<Control>();

                UnityEngine.InputSystem.Utilities.ReadOnlyArray<UnityEngine.InputSystem.InputControl> deviceInputControls = _inputDevice.allControls;
                UnityEngine.InputSystem.InputControl control;

                // Create elements in sub classes
                OnCreateControls(subClassControls);

                // Report subclass controls added to subclasses
                for (int i = 0; i < subClassControls.Count; i++) {
                    OnControlAdded(subClassControls[i]);
                }

                for (int i = 0; i < deviceInputControls.Count; i++) {
                    control = deviceInputControls[i];
                    if (control == null) continue;

#if REWIRED_DEBUG_THIS

                    string s = "";
                    
                    s += "Usages: ";
                    foreach (var usage in control.usages) {
                        s += usage + ", ";
                    }
                    s += ", Aliases: ";
                    foreach (var alias in control.aliases) {
                        s += alias + ", ";
                    }

                    s += "Device:\n" + UnityInputSystemHelper.ToString(control.device) + "\n";

                    DebugLog("Control [" + i + "]: type: " + (control.GetType().Name) + ", path: " + control.path + ", parent: " + (control.parent != null ? control.parent.name : "NONE") + ", " + s);
#endif
                    // Skip controls excluded by sub-classes
                    if (ExcludeControl(control)) continue;

                    // Skip all child controls of known compound controls because they are handled manually below
                    if (IsManuallyHandledType(control)) continue;

                    // Skip if the control or any of its child controls were already added by a sub class
                    if (Control.ContainsSelfOrChildren(subClassControls, control, true)) continue;

                    // Order of evaluation matters because of inheritance. Most-derived types must be listed first.
                    // If adding any compound control types, they must be added to IsManuallyHandledType also.

                    // All controls are being split out now in order to support unknown XR devices.
                    // Unknown Controller will expose all the Vectors and Quaternions as axes, but hopefully they will
                    // not interfere with polling because of .nosiy.

                    // If synthetic or noisy, don't add individual controls.
                    // For compound axes, only separated axes will be added to the Controller.
                    bool separateAxes = !control.noisy && !control.synthetic;

                    if (control is UnityEngine.InputSystem.Controls.ButtonControl) { // button extends axis, check first
                        AddControls((UnityEngine.InputSystem.Controls.ButtonControl)control, localControls);
                    } else if (control is UnityEngine.InputSystem.Controls.AxisControl) {
                        AddControls((UnityEngine.InputSystem.Controls.AxisControl)control, AxisCoordinateMode.Absolute, localControls);
                    } else if (control is UnityEngine.InputSystem.Controls.StickControl) { // handle stick as 2 axes
                        AddControls((UnityEngine.InputSystem.Controls.StickControl)control, localControls);
                    } else if (control is UnityEngine.InputSystem.Controls.DpadControl) { // handle d-pads as 4 buttons
                        AddControls((UnityEngine.InputSystem.Controls.DpadControl)control, localControls);
                    } else if (control is UnityEngine.InputSystem.Controls.DeltaControl) { // handle as single control (not bindable)
                        // Exclude from polling by default.
                        AddControls((UnityEngine.InputSystem.Controls.DeltaControl)control, separateAxes, localControls);
                    } else if (control is UnityEngine.InputSystem.Controls.Vector2Control) {
                        // Some sticks are treated as Vector2Control instead of StickControl (XR controllers).
                        AddControls((UnityEngine.InputSystem.Controls.Vector2Control)control, separateAxes, AxisCoordinateMode.Absolute, localControls);
                    } else if (control is UnityEngine.InputSystem.Controls.Vector3Control) {
                        // Sensors are sometimes mapped to axes by Unity Input System.
                        // Exclude controls and from polling by default.
                        // This is normally used for acceleration, angular velocity, or a 3d position.
                        // This can be overridden in a class by adding the controls manually.
                        AddControls((UnityEngine.InputSystem.Controls.Vector3Control)control, separateAxes, AxisCoordinateMode.Absolute, localControls);
                    } else if (control is UnityEngine.InputSystem.Controls.QuaternionControl) {
                        // Sensors are sometimes mapped to axes by Unity Input System.
                        // Exclude controls and from polling by default.
                        // Quaternion is almost certainly a rotation value.
                        AddControls((UnityEngine.InputSystem.Controls.QuaternionControl)control, separateAxes, localControls);
                    }
                }

                // Report local controls added to subclasses
                for (int i = 0; i < localControls.Count; i++) {
                    OnControlAdded(localControls[i]);
                }

                // Add subclass controls first so they are not sorted
                CopyAllOfType(subClassControls, _axes);
                CopyAllOfType(subClassControls, _buttons);

                // Sort the local results and store the lists
                System.Collections.Generic.List<AxisControl> tempAxes = new System.Collections.Generic.List<AxisControl>();
                CopyAllOfType(localControls, tempAxes);
                Sort(tempAxes, _axes);

                System.Collections.Generic.List<ButtonControl> tempButtons = new System.Collections.Generic.List<ButtonControl>();
                CopyAllOfType(localControls, tempButtons);
                Sort(tempButtons, _buttons);

                CopyAllOfType(localControls, _vector2Controls);
                CopyAllOfType(localControls, _vector3Controls);
                CopyAllOfType(localControls, _quaternionControls);

                _allControls.AddRange(subClassControls);
                _allControls.AddRange(localControls);
            }

            protected override int GetElementInfo(System.Collections.Generic.IList<ControllerElementInfo> results) {
                if (results == null) return 0;
                int origCount = results.Count;
                int count = _allControls.Count;
                for (int i = 0; i < count; i++) {
                    results.Add(new ControllerElementInfo() {
                        element = _allControls[i].element,
                        path = _allControls[i].relativePath
                    });
                }
                return results.Count - origCount;
            }

            public override bool GetKey(Rewired.KeyboardKeyCode keyCode) {
                // This function exists for Amazon Fire TV and Android because some controller elements may activate keyboard keys.
                // Each device may be exposed as multiple different devices like Keyboard + Gamepad.
                // For now, simply use the current keyboard instead of isolating the device's keyboard.
                UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard == null) return false;
                UnityEngine.InputSystem.Key unityKey = UnityInputSystemHelper.ToUnityInputSystemKey(keyCode);
                if (unityKey == UnityEngine.InputSystem.Key.None) return false;
                return keyboard[unityKey].isPressed;
            }

            // Static

            protected static readonly System.Action<Control> excludeControlFromPollingDelegate = x => x.excludeFromPolling = true;

            private static void AssignElement(Control control, Element element) {
                if (element == null || control == null) return;

                control.SetElement(element);

                // Sync properties
                if (control.control != null) {
                    if (
                        // Exclude controls from polling that are reported as noisy. // This includes sensor axes.
                        control.control.noisy ||
                        // Synthetic controls include things like "leftTriggerButton" in DualShock controllers and "isTracked" in XR controllers.
                        // These are fake controls created by the Layout. They get in the way of polling.
                        control.control.synthetic
                    )
                    {
                        element.excludeFromPolling = true;
                    }
                }
                if (control.excludeFromPolling) { // explicitly excluded by class
                    element.excludeFromPolling = true;
                }

            }

            private static int CopyAllOfType<TSource, TDestination>(System.Collections.Generic.IList<TSource> source, System.Collections.Generic.IList<TDestination> destination) where TDestination : class {
                int origCount = destination.Count;
                TDestination item;
                for (int i = 0; i < source.Count; i++) {
                    item = source[i] as TDestination;
                    if (item == null) continue;
                    destination.Add(item);
                }
                return destination.Count - origCount;
            }

            private static void Sort(
                System.Collections.Generic.List<AxisControl> axes,
                System.Collections.Generic.List<AxisControl> results
            ) {
                // Sticks
                Sort(
                    axes, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.Controls.StickControl>(x)
                );

                // V2
                // Some sticks are Vector2Control and not StickControl. Prefer they be listed first.
                // This only includes Vector2's that have not been excluded from polling (touchpad, etc.)
                Sort(
                    axes, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.Controls.Vector2Control>(x) && !x.excludeFromPolling
                );

                // Root
                Sort(
                    axes, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.InputDevice>(x)
                );

                // V2
                // Remaining Vector2's that have been excluded from polling.
                Sort(
                    axes, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.Controls.Vector2Control>(x) && x.excludeFromPolling
                );

                // V3
                Sort(
                    axes, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.Controls.Vector3Control>(x)
                );

                // Quat
                Sort(
                    axes, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.Controls.QuaternionControl>(x)
                );

                // ...
                Sort(
                    axes, results,
                    x => true
                );
            }
            private static void Sort(
                System.Collections.Generic.List<ButtonControl> buttons,
                System.Collections.Generic.List<ButtonControl> results
            ) {
                // Root
                Sort(
                    buttons, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.InputDevice>(x)
                );

                // D-Pad
                Sort(
                    buttons, results,
                    x => Control.IsChildOfType<UnityEngine.InputSystem.Controls.DpadControl>(x)
                );

                // ...
                Sort(
                    buttons, results,
                    x => true
                );
            }
            private static void Sort<T>(
                System.Collections.Generic.List<T> list,
                System.Collections.Generic.List<T> results,
                System.Func<T, bool> predicate
            ) {
                int count = list.Count;
                for (int i = 0; i < count; i++) {
                    if (predicate(list[i])) {
                        results.Add(list[i]);
                        list.RemoveAt(i);
                        i--;
                        count--;
                    }
                }
            }

            private static bool IsManuallyHandledType(UnityEngine.InputSystem.InputControl control) {
                return IsDescendantOfType<UnityEngine.InputSystem.Controls.Vector2Control>(control) || // captures delta
                       IsDescendantOfType<UnityEngine.InputSystem.Controls.DpadControl>(control) ||
                       IsDescendantOfType<UnityEngine.InputSystem.Controls.StickControl>(control) ||
                       IsDescendantOfType<UnityEngine.InputSystem.Controls.Vector3Control>(control) ||
                       IsDescendantOfType<UnityEngine.InputSystem.Controls.QuaternionControl>(control);
            }

            protected static int AddControls(
                UnityEngine.InputSystem.Controls.ButtonControl control,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                //if (control == null) return; // ALLOW NULL CONTROLS
                if (Control.Contains(controls, control)) return 0; // prevent duplicates
                controls.Add(new ButtonControl(control));
                if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                DebugLog("Added button: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control, 0));
#endif
                return 1;
            }
            protected static int AddControls(
                UnityEngine.InputSystem.Controls.ButtonControl control,
                bool isPressureSensitive,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                //if (control == null) return; // ALLOW NULL CONTROLS
                if (Control.Contains(controls, control)) return 0; // prevent duplicates
                controls.Add(new ButtonControl(control, isPressureSensitive));
                if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                DebugLog("Added button: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control, 0));
#endif
                return 1;
            }
            protected static void AddControls(
                UnityEngine.InputSystem.Controls.AxisControl control,
                AxisCoordinateMode coordinateMode,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                //if (control == null) return; // ALLOW NULL CONTROLS
                if (Control.Contains(controls, control)) return; // prevent duplicates
                controls.Add(new AxisControl(control, coordinateMode));
                if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                DebugLog("Added axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control, 0));
#endif
            }
            protected static int AddControls(
                UnityEngine.InputSystem.Controls.StickControl control,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                if (control == null) return 0;
                if (Control.ContainsSelfOrChildren(controls, control, true)) return 0; // some or all of child elements have already been added
                if (Control.ContainsSelfOrParent(controls, control, true)) return 0; // this or a parent element has already been added

                // Add all controls even if they are null

                int origCount = controls.Count;

                // Add axes
                if (!Control.Contains(controls, control.x)) {
                    controls.Add(new AxisControl(control.x, AxisCoordinateMode.Absolute));
                    if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                    DebugLog("Added 2d stick X axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.x, 0));
#endif
                }
                if (!Control.Contains(controls, control.y)) {
                    controls.Add(new AxisControl(control.y, AxisCoordinateMode.Absolute));
                    if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                    DebugLog("Added 2d stick Y axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.y, 0));
#endif
                }

                // Skip directional buttons
                return controls.Count - origCount;
            }
            protected static int AddControls(
                UnityEngine.InputSystem.Controls.DpadControl control,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                if (control == null) return 0;
                if (Control.ContainsSelfOrChildren(controls, control, true)) return 0; // some or all of child elements have already been added
                if (Control.ContainsSelfOrParent(controls, control, true)) return 0; // this or a parent element has already been added

                // Add all controls even if they are null

                int origCount = controls.Count;

                if (!Control.Contains(controls, control.up)) {
                    controls.Add(new ButtonControl(control.up));
                    if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                    DebugLog("Added d-pad up button: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.up, 0));
#endif
                }
                if (!Control.Contains(controls, control.right)) {
                    controls.Add(new ButtonControl(control.right));
                    if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                    DebugLog("Added d-pad right button: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.right, 0));
#endif
                }
                if (!Control.Contains(controls, control.down)) {
                    controls.Add(new ButtonControl(control.down));
                    if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                    DebugLog("Added d-pad down button: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.down, 0));
#endif
                }
                if (!Control.Contains(controls, control.left)) {
                    controls.Add(new ButtonControl(control.left));
                    if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                    DebugLog("Added d-pad left button: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.left, 0));
#endif
                }
                return controls.Count - origCount;
            }
            protected static int AddControls(
                UnityEngine.InputSystem.Controls.DeltaControl control,
                bool separateAxes,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                if (control == null) return 0;
                if (separateAxes) { // just handle as Vector2
                    return AddControls(
                        (UnityEngine.InputSystem.Controls.Vector2Control)control, 
                        separateAxes,
                        AxisCoordinateMode.Relative,
                        controls
                    );
                }
                if (Control.ContainsSelfOrChildren(controls, control, true)) return 0; // some or all of child elements have already been added
                if (Control.ContainsSelfOrParent(controls, control, true)) return 0; // this or a parent element has already been added

                // Add all controls even if they are null

                int origCount = controls.Count;

                if (!Control.ContainsSelfOrChildren(controls, control, true)) {
                    controls.Add(new Vector2Control(control, AxisCoordinateMode.Relative));
                    if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                    DebugLog("Added delta control: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control, 0));
#endif
                }

                return controls.Count - origCount;
            }
            protected static int AddControls(
                UnityEngine.InputSystem.Controls.Vector2Control control,
                bool separateAxes,
                AxisCoordinateMode coordinateMode,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                if (control == null) return 0;
                if (!separateAxes && Control.ContainsSelfOrChildren(controls, control, true)) return 0; // some or all of child elements have already been added
                if (Control.ContainsSelfOrParent(controls, control, true)) return 0; // this or a parent element has already been added

                // Add all controls even if they are null

                int origCount = controls.Count;

                if (separateAxes) {
                    if (!Control.Contains(controls, control.x)) {
                        controls.Add(new AxisControl(control.x, coordinateMode));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added vector 2 X axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.x, 0));
#endif
                    }
                    if (!Control.Contains(controls, control.y)) {
                        controls.Add(new AxisControl(control.y, coordinateMode));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added vector 2 Y axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.y, 0));
#endif
                    }
                } else {
                    if (!Control.ContainsSelfOrChildren(controls, control, true)) {
                        controls.Add(new Vector2Control(control, coordinateMode));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added vector2 control: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control, 0));
#endif
                    }
                }

                return controls.Count - origCount;
            }
            protected static int AddControls(
                UnityEngine.InputSystem.Controls.Vector3Control control,
                bool separateAxes,
                AxisCoordinateMode coordinateMode,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                if (control == null) return 0;
                if (!separateAxes && Control.ContainsSelfOrChildren(controls, control, true)) return 0; // some or all of child elements have already been added
                if (Control.ContainsSelfOrParent(controls, control, true)) return 0; // this or a parent element has already been added

                // Add all controls even if they are null

                int origCount = controls.Count;

                if (separateAxes) {
                    if (!Control.Contains(controls, control.x)) {
                        controls.Add(new AxisControl(control.x, coordinateMode));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added vector 3 X axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.x, 0));
#endif
                    }
                    if (!Control.Contains(controls, control.y)) {
                        controls.Add(new AxisControl(control.y, coordinateMode));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added vector 3 Y axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.y, 0));
#endif
                    }
                    if (!Control.Contains(controls, control.z)) {
                        controls.Add(new AxisControl(control.z, coordinateMode));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added vector 3 Z axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.z, 0));
#endif
                    }
                } else {
                    if (!Control.ContainsSelfOrChildren(controls, control, true)) {
                        controls.Add(new Vector3Control(control, coordinateMode));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added vector3 control: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control, 0));
#endif
                    }
                }

                return controls.Count - origCount;
            }
            protected static int AddControls(
                UnityEngine.InputSystem.Controls.QuaternionControl control,
                bool separateAxes,
                System.Collections.Generic.List<Control> controls,
                System.Action<Control> addedCallback = null
            ) {
                if (control == null) return 0;
                if (!separateAxes && Control.ContainsSelfOrChildren(controls, control, true)) return 0; // some or all of child elements have already been added
                if (Control.ContainsSelfOrParent(controls, control, true)) return 0; // this or a parent element has already been added

                // Add all controls even if they are null

                int origCount = controls.Count;

                if (separateAxes) {
                    if (!Control.Contains(controls, control.x)) {
                        controls.Add(new AxisControl(control.x, AxisCoordinateMode.Absolute));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added quaternion X axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.x, 0));
#endif
                    }
                    if (!Control.Contains(controls, control.y)) {
                        controls.Add(new AxisControl(control.y, AxisCoordinateMode.Absolute));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added quaternion Y axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.y, 0));
#endif
                    }
                    if (!Control.Contains(controls, control.z)) {
                        controls.Add(new AxisControl(control.z, AxisCoordinateMode.Absolute));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added quaternion Z axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.z, 0));
#endif
                    }
                    if (!Control.Contains(controls, control.w)) {
                        controls.Add(new AxisControl(control.w, AxisCoordinateMode.Absolute));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added quaternion W axis: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control.w, 0));
#endif
                    }
                } else {
                    if (!Control.ContainsSelfOrChildren(controls, control, true)) {
                        controls.Add(new QuaternionControl(control));
                        if (addedCallback != null) addedCallback(controls[controls.Count - 1]);
#if REWIRED_DEBUG_THIS
                        DebugLog("Added quaternion control: " + control.displayName + "\n" + UnityInputSystemHelper.ToString(control, 0));
#endif
                    }
                }

                return controls.Count - origCount;
            }

            private static bool IsDescendantOfType<T>(UnityEngine.InputSystem.InputControl control) {
                if (control == null || control.parent == null) return false;
                if (control.parent is T) {
                    return true;
                } else {
                    return IsDescendantOfType<T>(control.parent);
                }
            }

            new public class InitOptions : UnityInputSystemInputSourceBase.Joystick.InitOptions {

                public UnityEngine.InputSystem.InputDevice inputDevice { get; set; }

                public InitOptions(
                    UnityInputSystemDeviceType unityDeviceType,
                    UnityEngine.InputSystem.InputDevice inputDevice
                    )
                    : base(unityDeviceType) {
                    if (inputDevice == null) throw new System.ArgumentNullException("inputDevice");
                    this.inputDevice = inputDevice;
                }
            }

            [System.Diagnostics.Conditional("REWIRED_DEBUG_THIS")]
            private static void DebugLog(object o) {
                UnityEngine.Debug.Log(o);
            }
            [System.Diagnostics.Conditional("REWIRED_DEBUG_THIS")]
            private static void DebugLogWarning(object o) {
                UnityEngine.Debug.LogWarning(o);
            }
        }
    }
}

#endif

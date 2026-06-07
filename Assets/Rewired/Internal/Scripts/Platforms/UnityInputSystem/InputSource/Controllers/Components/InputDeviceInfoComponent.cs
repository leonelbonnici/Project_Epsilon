// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    public partial class UnityInputSystemInputSource : UnityInputSystemInputSourceBase {

        /// <exclude></exclude>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        new public sealed class InputDeviceInfoComponent : UnityInputSystemInputSourceBase.InputDeviceInfoComponent {

            private readonly System.Func<UnityEngine.InputSystem.InputDevice> _getInputDevice;

            private bool hasInputDevice { get { return _getInputDevice() != null; } }

            public override object inputDevice { get { return _getInputDevice(); } }

            public override string product { get { return hasInputDevice ? _getInputDevice().description.product : null; } }
            public override string capabilities { get { return hasInputDevice ? _getInputDevice().description.capabilities : null; } }
            public override string deviceClass { get { return hasInputDevice ? _getInputDevice().description.deviceClass : null; } }
            public override string interfaceName { get { return hasInputDevice ? _getInputDevice().description.interfaceName : null; } }
            public override string manufacturer { get { return hasInputDevice ? _getInputDevice().description.manufacturer : null; } }
            public override string serial { get { return hasInputDevice ? _getInputDevice().description.serial : null; } }
            public override string version { get { return hasInputDevice ? _getInputDevice().description.version : null; } }

            public override string path { get { return hasInputDevice ? _getInputDevice().path : null; } }
            public override string layout { get { return hasInputDevice ? _getInputDevice().layout : null; } }
            public override string variants { get { return hasInputDevice ? _getInputDevice().variants : null; } }
            public override bool noisy { get { return hasInputDevice ? _getInputDevice().noisy : false; } }
            public override bool synthetic { get { return hasInputDevice ? _getInputDevice().synthetic : false; } }
            public override System.Type valueType { get { return hasInputDevice ? _getInputDevice().valueType : null; } }
            public override int valueSizeInBytes { get { return hasInputDevice ? _getInputDevice().valueSizeInBytes : 0; } }
            public override float magnitude { get { return hasInputDevice ? _getInputDevice().magnitude : 0f; } }
            public override string shortDisplayName { get { return hasInputDevice ? _getInputDevice().shortDisplayName : null; } }
            public override string name { get { return hasInputDevice ? _getInputDevice().name : null; } }
            public override string displayName { get { return hasInputDevice ? _getInputDevice().displayName : null; } }
            public override bool canRunInBackground { get { return hasInputDevice ? _getInputDevice().canRunInBackground : false; } }
            public override bool added { get { return hasInputDevice ? _getInputDevice().added : false; } }
            public override int GetAliases(System.Collections.Generic.IList<string> results) {
                if (!hasInputDevice) return 0;
                var inputDevice = _getInputDevice();
                int origCount = results.Count;
                for (int i = 0; i < inputDevice.aliases.Count; i++) {
                    results.Add(inputDevice.aliases[i]);
                }
                return results.Count - origCount;
            }

            public override int GetUsages(System.Collections.Generic.IList<string> results) {
                if (!hasInputDevice) return 0;
                var inputDevice = _getInputDevice();
                int origCount = results.Count;
                for (int i = 0; i < inputDevice.usages.Count; i++) {
                    results.Add(inputDevice.usages[i]);
                }
                return results.Count - origCount;
            }

            public InputDeviceInfoComponent(
                Rewired.Platforms.Custom.CustomInputSource.Controller controller,
                System.Func<UnityEngine.InputSystem.InputDevice> getInputDevice
            ) : base(controller) {
                if (getInputDevice == null) throw new System.ArgumentNullException("getInputDevice");
                _getInputDevice = getInputDevice;
            }
        }
    }
}

#endif

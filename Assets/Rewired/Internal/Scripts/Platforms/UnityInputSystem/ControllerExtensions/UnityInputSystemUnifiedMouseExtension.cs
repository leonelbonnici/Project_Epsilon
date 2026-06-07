// Copyright (c) 2025 Augie R. Maddox, Guavaman Enterprises. All rights reserved.

#if REWIRED_UNITY_INPUT_SYSTEM_SUPPORTED && REWIRED_UNITY_INPUT_SYSTEM_INSTALLED

namespace Rewired.Platforms.UnityInputSystem {

    /// <summary>
    /// Provides access to information exposed by Unity Input System for the current Mouse.
    /// </summary>
    /// <exclude></exclude>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class UnityInputSystemUnifiedMouseExtension : UnityInputSystemMouseExtension {

        public UnityInputSystemUnifiedMouseExtension() : base(() => UnityEngine.InputSystem.Mouse.current) {
        }
        private UnityInputSystemUnifiedMouseExtension(UnityInputSystemUnifiedMouseExtension other) : base(other) {
        }

        public override Controller.Extension ShallowCopy() {
            return new UnityInputSystemUnifiedMouseExtension(this);
        }
    }
}

#endif

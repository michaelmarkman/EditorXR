﻿using UnityEditor.Experimental.EditorVR.Core;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Menus
{
    /// <summary>
    /// Mandates that derived classes implement core SpatialUI implementation
    /// The SpatialMenu is the first robust implementation, SpatialContextUI is planned to derive from core
    /// </summary>
    public abstract class SpatialUIView : MonoBehaviour, IControlHaptics, INodeToRay
    {
        /// <summary>
        /// Enum that defines the allowed SpatialUI input-modes
        /// </summary>
        public enum SpatialInterfaceInputMode
        {
            Neutral,
            Ray,
            TriggerAffordanceRotation
        }

        [Header("Haptic Pulses")]
        [SerializeField]
        protected HapticPulse m_HighlightUIElementPulse;

        [SerializeField]
        protected HapticPulse m_SustainedHoverUIElementPulse;
    }
}

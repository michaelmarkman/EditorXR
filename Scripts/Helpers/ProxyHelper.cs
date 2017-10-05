﻿#if UNITY_EDITOR
using System;
using UnityEditor.Experimental.EditorVR.UI;
using UnityEngine;
using UnityEngine.InputNew;

namespace UnityEditor.Experimental.EditorVR.Proxies
{
	/// <summary>
	/// Reference container for additional content origins on a device
	/// </summary>
	sealed class ProxyHelper : MonoBehaviour
	{
		[Serializable]
		public class ButtonObject
		{
			[SerializeField]
			VRInputDevice.VRControl m_Control;

			[SerializeField]
			Transform m_Transform;

			[SerializeField]
			Renderer m_Renderer;

			[FlagsProperty]
			[SerializeField]
			AxisFlags m_TranslateAxes;

			[FlagsProperty]
			[SerializeField]
			AxisFlags m_RotateAxes;

			[SerializeField]
			float m_Min;

			[SerializeField]
			float m_Max;

			public VRInputDevice.VRControl control { get { return m_Control; } }
			public Transform transform { get { return m_Transform; } }
			public Renderer renderer { get { return m_Renderer; } }
			public AxisFlags translateAxes { get { return m_TranslateAxes; } }
			public AxisFlags rotateAxes { get { return m_RotateAxes; } }
			public float min { get { return m_Min; } }
			public float max { get { return m_Max; } }
		}

		[SerializeField]
		Transform m_RayOrigin;

		[SerializeField]
		Transform m_MenuOrigin;

		[SerializeField]
		Transform m_AlternateMenuOrigin;

		[SerializeField]
		Transform m_PreviewOrigin;

		[SerializeField]
		Transform m_FieldGrabOrigin;

		[SerializeField]
		ButtonObject[] m_Buttons;

		[SerializeField]
		Tooltip[] m_LeftTooltips;

		[SerializeField]
		Tooltip[] m_RightTooltips;

		/// <summary>
		/// The transform that the device's ray contents (default ray, custom ray, etc) will be parented under
		/// </summary>
		public Transform rayOrigin { get { return m_RayOrigin; } }

		/// <summary>
		/// The transform that the menu content will be parented under
		/// </summary>
		public Transform menuOrigin { get { return m_MenuOrigin; } }

		/// <summary>
		/// The transform that the alternate-menu content will be parented under
		/// </summary>
		public Transform alternateMenuOrigin { get { return m_AlternateMenuOrigin; } }

		/// <summary>
		/// The transform that the display/preview objects will be parented under
		/// </summary>
		public Transform previewOrigin { get { return m_PreviewOrigin; } }

		/// <summary>
		/// The transform that the display/preview objects will be parented under
		/// </summary>
		public Transform fieldGrabOrigin { get { return m_FieldGrabOrigin; } }

		/// <summary>
		/// Button objects to store transform and renderer references
		/// </summary>
		public ButtonObject[] buttons { get { return m_Buttons; } }

		/// <summary>
		/// Tooltip components to be removed from a right-handed controller
		/// </summary>
		public Tooltip[] leftTooltips { get { return m_LeftTooltips; } }

		/// <summary>
		/// Tooltip components to be removed from a left-handed controller
		/// </summary>
		public Tooltip[] rightTooltips { get { return m_RightTooltips; } }
	}
}
#endif

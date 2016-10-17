﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputNew;
using UnityEngine.VR;
using UnityEngine.VR.Helpers;
using UnityEngine.VR.Modules;
using UnityEngine.VR.Tools;
using UnityEngine.VR.Utilities;

public class TransformTool : MonoBehaviour, ITool, ICustomActionMaps, ITransformTool, ISelectionChanged, IDirectSelection, IBlockUIInput
{
	const float kBaseManipulatorSize = 0.3f;
	const float kLazyFollowTranslate = 8f;
	const float kLazyFollowRotate = 12f;
	const float kViewerPivotTransitionTime = 0.75f;

	class GrabData
	{
		public Transform grabbedObject;
		public Transform rayOrigin;
		public Vector3 positionOffset;
		public Quaternion rotationOffset;
		public Vector3 initialScale;

		public GrabData(Transform rayOrigin, Transform grabbedObject)
		{
			this.rayOrigin = rayOrigin;
			this.grabbedObject = grabbedObject;
			var inverseRotation = Quaternion.Inverse(rayOrigin.rotation);
			positionOffset = inverseRotation * (grabbedObject.transform.position - rayOrigin.position);
			rotationOffset = inverseRotation * grabbedObject.transform.rotation;
			initialScale = grabbedObject.transform.localScale;
		}
	}

	[SerializeField]
	GameObject m_StandardManipulatorPrefab;

	[SerializeField]
	GameObject m_ScaleManipulatorPrefab;

	[SerializeField]
	ActionMap m_TransformActionMap;

	[SerializeField]
	ActionMap m_DirectSelectActionMap;

	readonly List<GameObject> m_AllManipulators = new List<GameObject>();
	GameObject m_CurrentManipulator;
	int m_CurrentManipulatorIndex;

	Transform[] m_SelectionTransforms;
	Bounds m_SelectionBounds;
	Vector3 m_TargetPosition;
	Quaternion m_TargetRotation;
	Vector3 m_TargetScale;
	Quaternion m_PositionOffsetRotation;
	Quaternion m_StartRotation;

	readonly Dictionary<Transform, Vector3> m_PositionOffsets = new Dictionary<Transform, Vector3>();
	readonly Dictionary<Transform, Quaternion> m_RotationOffsets = new Dictionary<Transform, Quaternion>();
	readonly Dictionary<Transform, Vector3> m_ScaleOffsets = new Dictionary<Transform, Vector3>();

	PivotRotation m_PivotRotation = PivotRotation.Local;
	PivotMode m_PivotMode = PivotMode.Pivot;

	readonly Dictionary<Node, GrabData> m_GrabData = new Dictionary<Node, GrabData>();
	bool m_DirectSelected;
	float m_ZoomStartDistance;
	Node m_ZoomFirstNode;
	float m_ScaleFactor;
	bool m_WasScaling;

	TransformInput m_TransformInput;
	DirectSelectInput m_DirectSelectInput;

	public ActionMap[] actionMaps { get { return new [] { m_TransformActionMap, m_DirectSelectActionMap }; } }

	public DirectSelectInput directSelectInput { get { return m_DirectSelectInput; } }

	public bool directManipulationEnabled { get; set; }

	public ActionMapInput[] actionMapInputs
	{
		get
		{
			return m_ActionMapInputs;
		}
		set
		{
			m_ActionMapInputs = value;
			foreach (var input in m_ActionMapInputs)
			{
				var transformInput = input as TransformInput;
				if (transformInput != null)
					m_TransformInput = transformInput;

				var directInput = input as DirectSelectInput;
				if (directInput != null)
					m_DirectSelectInput = directInput;
			}
		}
	}
	ActionMapInput[] m_ActionMapInputs;

	public Func<Dictionary<Transform, DirectSelection>> getDirectSelection { private get; set; }

	public Action<bool> setInputBlocked { get; set; }

	void Awake()
	{
		directManipulationEnabled = true;
		// Add standard and scale manipulator prefabs to a list (because you cannot add asset references directly to a serialized list)
		if (m_StandardManipulatorPrefab != null)
			m_AllManipulators.Add(CreateManipulator(m_StandardManipulatorPrefab));

		if (m_ScaleManipulatorPrefab != null)
			m_AllManipulators.Add(CreateManipulator(m_ScaleManipulatorPrefab));

		m_CurrentManipulatorIndex = 0;
		m_CurrentManipulator = m_AllManipulators[m_CurrentManipulatorIndex];
	}

	public void OnSelectionChanged()
	{
		m_SelectionTransforms = Selection.GetTransforms(SelectionMode.Editable);
		m_DirectSelected = false;

		if (m_SelectionTransforms.Length == 0)
			m_CurrentManipulator.SetActive(false);
		else
			UpdateCurrentManipulator();
	}

	void Update()
	{
		var hasObject = false;
		if (directManipulationEnabled)
		{
			var directSelection = getDirectSelection();
			var hasLeft = m_GrabData.ContainsKey(Node.LeftHand);
			var hasRight = m_GrabData.ContainsKey(Node.RightHand);
			hasObject = directSelection.Count > 0 || hasLeft || hasRight;
			m_DirectSelectInput.active = hasObject;
			if (m_CurrentManipulator.activeSelf && hasObject)
				m_CurrentManipulator.SetActive(false);

			foreach (var selection in directSelection)
			{
				if (selection.Value.gameObject.tag == "VRPlayer" && !selection.Value.isMiniWorldRay)
					continue;
				if (selection.Value.node == Node.LeftHand && m_DirectSelectInput.selectLeft.wasJustPressed)
				{
					if (selection.Value.gameObject.tag == "VRPlayer")
						selection.Value.gameObject.transform.parent = null;

					setInputBlocked(true);
					var grabbedObject = selection.Value.gameObject.transform;
					var rayOrigin = selection.Key;

					// Check if the other hand is already grabbing for two-handed scale
					foreach (var grabData in m_GrabData)
					{
						var otherNode = grabData.Key;
						if (otherNode != Node.LeftHand)
						{
							m_ZoomStartDistance = (rayOrigin.position - grabData.Value.rayOrigin.position).magnitude;
							m_ZoomFirstNode = otherNode;
							grabData.Value.positionOffset = grabbedObject.position - grabData.Value.rayOrigin.position;
							break;
						}
					}

					m_GrabData[Node.LeftHand] = new GrabData(rayOrigin, grabbedObject);

					Selection.activeGameObject = grabbedObject.gameObject;
				}
				if (selection.Value.node == Node.RightHand && m_DirectSelectInput.selectRight.wasJustPressed)
				{
					if (selection.Value.gameObject.tag == "VRPlayer")
						selection.Value.gameObject.transform.parent = null;

					setInputBlocked(true);
					var grabbedObject = selection.Value.gameObject.transform;
					var rayOrigin = selection.Key;

					// Check if the other hand is already grabbing for two-handed scale
					foreach (var grabData in m_GrabData)
					{
						var otherNode = grabData.Key;
						if (otherNode != Node.RightHand)
						{
							m_ZoomStartDistance = (rayOrigin.position - grabData.Value.rayOrigin.position).magnitude;
							m_ZoomFirstNode = otherNode;
							grabData.Value.positionOffset = grabbedObject.position - grabData.Value.rayOrigin.position;
							break;
						}
					}

					m_GrabData[Node.RightHand] = new GrabData(rayOrigin, grabbedObject);

					Selection.activeGameObject = grabbedObject.gameObject;
				}
			}

			GrabData leftData;
			hasLeft = m_GrabData.TryGetValue(Node.LeftHand, out leftData);

			GrabData rightData;
			hasRight = m_GrabData.TryGetValue(Node.RightHand, out rightData);

			var leftHeld = m_DirectSelectInput.selectLeft.isHeld;
			var rightHeld = m_DirectSelectInput.selectRight.isHeld;
			if (hasLeft && hasRight && leftHeld && rightHeld && leftData.grabbedObject == rightData.grabbedObject)
			{
				m_WasScaling = true;
				m_ScaleFactor = (leftData.rayOrigin.position - rightData.rayOrigin.position).magnitude / m_ZoomStartDistance;
				if (m_ScaleFactor > 0 && m_ScaleFactor < Mathf.Infinity)
				{
					if (m_ZoomFirstNode == Node.LeftHand)
					{
						var rayOrigin = leftData.rayOrigin;
						var grabbedObject = leftData.grabbedObject;
						grabbedObject.position = rayOrigin.position + leftData.positionOffset * m_ScaleFactor;
						grabbedObject.localScale = leftData.initialScale * m_ScaleFactor;
					}
					else
					{
						var rayOrigin = rightData.rayOrigin;
						var grabbedObject = rightData.grabbedObject;
						grabbedObject.position = rayOrigin.position + rightData.positionOffset * m_ScaleFactor;
						grabbedObject.localScale = rightData.initialScale * m_ScaleFactor;
					}
				}

				m_DirectSelected = true;
			}
			else
			{
				if (m_WasScaling)
				{
					// Reset initial conditions
					if (hasLeft)
						leftData = m_GrabData[Node.LeftHand] = new GrabData(leftData.rayOrigin, leftData.grabbedObject);
					if (hasRight)
						rightData = m_GrabData[Node.RightHand] = new GrabData(rightData.rayOrigin, rightData.grabbedObject);

					m_WasScaling = false;
				}
				if (hasLeft && leftHeld)
				{
					var rayOrigin = leftData.rayOrigin;
					var grabbedObject = leftData.grabbedObject;
					grabbedObject.position = rayOrigin.position + rayOrigin.rotation * leftData.positionOffset;
					grabbedObject.rotation = rayOrigin.rotation * leftData.rotationOffset;

					m_DirectSelected = true;
				}
				else if (hasRight && rightHeld)
				{
					var rayOrigin = rightData.rayOrigin;
					var grabbedObject = rightData.grabbedObject;
					grabbedObject.position = rayOrigin.position + rayOrigin.rotation * rightData.positionOffset;
					grabbedObject.rotation = rayOrigin.rotation * rightData.rotationOffset;

					m_DirectSelected = true;
				}
			}

			if (m_DirectSelectInput.selectLeft.wasJustReleased)
				DropObject(Node.LeftHand);

			if (m_DirectSelectInput.selectRight.wasJustReleased)
				DropObject(Node.RightHand);
		}

		if (hasObject || m_DirectSelected)
			return;

		setInputBlocked(false);

		if (m_SelectionTransforms != null && m_SelectionTransforms.Length > 0)
		{
			if (m_TransformInput.pivotMode.wasJustPressed) // Switching center vs pivot
				SwitchPivotMode();

			if (m_TransformInput.pivotRotation.wasJustPressed) // Switching global vs local
				SwitchPivotRotation();

			if (m_TransformInput.manipulatorType.wasJustPressed)
				SwitchManipulator();

			var manipulator = m_CurrentManipulator.GetComponent<IManipulator>();
			if (manipulator != null && !manipulator.dragging)
			{
				UpdateManipulatorSize();
				UpdateCurrentManipulator();
			}

			var deltaTime = Time.unscaledDeltaTime;
			var manipulatorTransform = m_CurrentManipulator.transform;
			manipulatorTransform.position = Vector3.Lerp(manipulatorTransform.position, m_TargetPosition, kLazyFollowTranslate * deltaTime);

			if (m_PivotRotation == PivotRotation.Local) // Manipulator does not rotate when in global mode
				manipulatorTransform.rotation = Quaternion.Slerp(manipulatorTransform.rotation, m_TargetRotation, kLazyFollowRotate * deltaTime);

			foreach (var t in m_SelectionTransforms)
			{
				t.rotation = Quaternion.Slerp(t.rotation, m_TargetRotation * m_RotationOffsets[t], kLazyFollowRotate * deltaTime);

				if (m_PivotMode == PivotMode.Center) // Rotate the position offset from the manipulator when rotating around center
				{
					m_PositionOffsetRotation = Quaternion.Slerp(m_PositionOffsetRotation, m_TargetRotation * Quaternion.Inverse(m_StartRotation), kLazyFollowRotate * deltaTime);
					t.position = manipulatorTransform.position + m_PositionOffsetRotation * m_PositionOffsets[t];
				}
				else
					t.position = manipulatorTransform.position + m_PositionOffsets[t];

				t.localScale = Vector3.Lerp(t.localScale, Vector3.Scale(m_TargetScale, m_ScaleOffsets[t]), kLazyFollowTranslate * deltaTime);
			}
		}
	}

	IEnumerator UpdateViewerPivot(Transform playerHead)
	{
		var viewerPivot = U.Camera.GetViewerPivot();

		var components = viewerPivot.GetComponentsInChildren<SmoothMotion>();
		foreach (var smoothMotion in components)
		{
			smoothMotion.enabled = false;
		}

		var mainCamera = U.Camera.GetMainCamera().transform;
		var startPosition = viewerPivot.position;
		var startRotation = viewerPivot.rotation;

		var rotationDiff = U.Math.YawConstrainRotation(Quaternion.Inverse(mainCamera.rotation) * playerHead.rotation);
		var cameraDiff = viewerPivot.position - mainCamera.position;
		cameraDiff.y = 0;
		var rotationOffset = rotationDiff * cameraDiff - cameraDiff;

		var endPosition = viewerPivot.position + (playerHead.position - mainCamera.position) + rotationOffset;
		var endRotation = viewerPivot.rotation * rotationDiff;
		var startTime = Time.realtimeSinceStartup;
		var diffTime = 0f;

		while (diffTime < kViewerPivotTransitionTime)
		{
			diffTime = Time.realtimeSinceStartup - startTime;
			var t = diffTime / kViewerPivotTransitionTime;
			viewerPivot.position = Vector3.Lerp(startPosition, endPosition, t);
			viewerPivot.rotation = Quaternion.Lerp(startRotation, endRotation, t);
			yield return null;
		}

		viewerPivot.position = endPosition;
		playerHead.parent = mainCamera;
		playerHead.localRotation = Quaternion.identity;
		playerHead.localPosition = Vector3.zero;

		foreach (var smoothMotion in components)
		{
			smoothMotion.enabled = true;
		}
	}

	public void DropHeldObject(Transform obj)
	{
		var grabDataCopy = new Dictionary<Node, GrabData>(m_GrabData);
		foreach (var grabData in grabDataCopy)
		{
			if (grabData.Value.grabbedObject == obj)
				DropObject(grabData.Key);
		}
	}

	void DropObject(Node inputNode)
	{
		var grabbedObject = m_GrabData[inputNode].grabbedObject;
		if (grabbedObject.tag == "VRPlayer")
			StartCoroutine(UpdateViewerPivot(grabbedObject));

		m_GrabData.Remove(inputNode);
	}


	private void Translate(Vector3 delta)
	{
		m_TargetPosition += delta;
	}

	private void Rotate(Quaternion delta)
	{
		m_TargetRotation = delta * m_TargetRotation;
	}

	private void Scale(Vector3 delta)
	{
		m_TargetScale += delta;
	}

	private void UpdateSelectionBounds()
	{
		Bounds? newBounds = null;
		foreach (var selectedObj in m_SelectionTransforms)
		{
			var renderers = selectedObj.GetComponentsInChildren<Renderer>();
			foreach (var r in renderers)
			{
				if (Mathf.Approximately(r.bounds.extents.sqrMagnitude, 0f)) // Necessary because Particle Systems have renderer components with center and extents (0,0,0)
					continue;

				if (newBounds.HasValue)
					// Only use encapsulate after the first renderer, otherwise bounds will always encapsulate point (0,0,0)
					newBounds.Value.Encapsulate(r.bounds);
				else
					newBounds = r.bounds;
			}
		}

		// If we haven't encountered any Renderers, return bounds of (0,0,0) at the center of the selected objects
		if (newBounds == null)
		{
			var bounds = new Bounds();
			foreach (var selectedObj in m_SelectionTransforms)
				bounds.center += selectedObj.transform.position / m_SelectionTransforms.Length;
			newBounds = bounds;
		}

		m_SelectionBounds = newBounds.Value;
	}

	private void UpdateManipulatorSize()
	{
		var camera = U.Camera.GetMainCamera();
		var distance = Vector3.Distance(camera.transform.position, m_CurrentManipulator.transform.position);
		m_CurrentManipulator.transform.localScale = Vector3.one * distance * kBaseManipulatorSize;
	}

	private GameObject CreateManipulator(GameObject prefab)
	{
		var go = U.Object.Instantiate(prefab, transform, active: false);
		var manipulator = go.GetComponent<IManipulator>();
		manipulator.translate = Translate;
		manipulator.rotate = Rotate;
		manipulator.scale = Scale;
		return go;
	}

	private void UpdateCurrentManipulator()
	{
		if (m_SelectionTransforms.Length <= 0)
			return;

		UpdateSelectionBounds();
		m_CurrentManipulator.SetActive(true);
		var manipulatorTransform = m_CurrentManipulator.transform;
		manipulatorTransform.position = m_PivotMode == PivotMode.Pivot ? m_SelectionTransforms[0].position : m_SelectionBounds.center;
		manipulatorTransform.rotation = m_PivotRotation == PivotRotation.Global ? Quaternion.identity : m_SelectionTransforms[0].rotation;
		m_TargetPosition = manipulatorTransform.position;
		m_TargetRotation = manipulatorTransform.rotation;
		m_StartRotation = m_TargetRotation;
		m_PositionOffsetRotation = Quaternion.identity;
		m_TargetScale = Vector3.one;

		// Save the initial position, rotation, and scale realtive to the manipulator
		m_PositionOffsets.Clear();
		m_RotationOffsets.Clear();
		m_ScaleOffsets.Clear();
		foreach (var t in m_SelectionTransforms)
		{
			m_PositionOffsets.Add(t, t.position - manipulatorTransform.position);
			m_ScaleOffsets.Add(t, t.localScale);
			m_RotationOffsets.Add(t, Quaternion.Inverse(manipulatorTransform.rotation) * t.rotation);
		}
	}

	private void SwitchPivotMode()
	{
		m_PivotMode = m_PivotMode == PivotMode.Pivot ? PivotMode.Center : PivotMode.Pivot;
		UpdateCurrentManipulator();
	}

	private void SwitchPivotRotation()
	{
		m_PivotRotation = m_PivotRotation == PivotRotation.Global ? PivotRotation.Local : PivotRotation.Global;
		UpdateCurrentManipulator();
	}

	private void SwitchManipulator()
	{
		foreach (var manipulator in m_AllManipulators)
			manipulator.SetActive(false);

		// Go to the next manipulator type in the list
		m_CurrentManipulatorIndex = (m_CurrentManipulatorIndex + 1) % m_AllManipulators.Count;
		m_CurrentManipulator = m_AllManipulators[m_CurrentManipulatorIndex];
		UpdateCurrentManipulator();
	}
}
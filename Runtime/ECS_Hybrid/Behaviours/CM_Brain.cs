﻿using Cinemachine.ECS;
using Cinemachine.Utility;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;

namespace Cinemachine.ECS_Hybrid
{
    [DisallowMultipleComponent]
#if UNITY_2018_3_OR_NEWER
    [ExecuteAlways]
#else
    [ExecuteInEditMode]
#endif
    [AddComponentMenu("Cinemachine/CM_Brain")]
    [SaveDuringPlay]
    public class CM_Brain : MonoBehaviour
    {
        /// <summary>
        /// When enabled, the current camera and blend will be indicated in the game window, for debugging.
        /// </summary>
        [Tooltip("When enabled, the current camera and blend will be indicated in the game "
            + "window, for debugging")]
        public bool m_ShowDebugText = false;

        /// <summary>
        /// When enabled, shows the camera's frustum in the scene view.
        /// </summary>
        [Tooltip("When enabled, the camera's frustum will be shown at all times in the scene view")]
        public bool m_ShowCameraFrustum = true;

        /// <summary>
        /// When enabled, the cameras will always respond in real-time to user input and damping,
        /// even if the game is running in slow motion
        /// </summary>
        [Tooltip("When enabled, the cameras will always respond in real-time to user input and damping, "
            + "even if the game is running in slow motion")]
        public bool m_IgnoreTimeScale = false;

        /// <summary>
        /// If set, this object's Y axis will define the worldspace Up vector for all the
        /// virtual cameras.  This is useful in top-down game environments.  If not set, Up is
        /// worldspace Y.
        /// </summary>
        [Tooltip("If set, this object's Y axis will define the worldspace Up vector for all the "
            + "virtual cameras.  This is useful for instance in top-down game environments.  "
            + "If not set, Up is worldspace Y.  Setting this appropriately is important, because "
            + "Virtual Cameras don't like looking straight up or straight down.")]
        public Transform m_WorldUpOverride;

        /// <summary>
        /// The blend which is used if you don't explicitly define a blend between two Virtual Cameras.
        /// </summary>
        [CinemachineBlendDefinitionProperty]
        [Tooltip("The blend that is used in cases where you haven't explicitly defined a blend "
            + "between two Virtual Cameras")]
        public CinemachineBlendDefinition m_DefaultBlend
            = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 2f);

        /// <summary>
        /// This is the asset which contains custom settings for specific blends.
        /// </summary>
        [Tooltip("This is the asset that contains custom settings for blends between specific "
            + "virtual cameras in your scene")]
        public CinemachineBlenderSettings m_CustomBlends = null;

        /// <summary>
        /// Get the Unity Camera that is attached to this GameObject.  This is the camera
        /// that will be controlled by the brain.
        /// </summary>
        public Camera OutputCamera
        {
            get
            {
                if (m_OutputCamera == null && !Application.isPlaying)
                    m_OutputCamera = GetComponent<Camera>();
                return m_OutputCamera;
            }
        }
        private Camera m_OutputCamera = null; // never use directly - use accessor

        /// <summary>Event with a CM_Brain parameter</summary>
        [Serializable] public class BrainEvent : UnityEvent<CM_Brain> {}

        /// <summary>Event with a ICinemachineCamera parameter</summary>
        [Serializable] public class VcamActivatedEvent : UnityEvent<ICinemachineCamera, ICinemachineCamera> {}

        /// <summary>This event will fire whenever a virtual camera goes live and there is no blend</summary>
        [Tooltip("This event will fire whenever a virtual camera goes live and there is no blend")]
        public BrainEvent m_CameraCutEvent = new BrainEvent();

        /// <summary>This event will fire whenever a virtual camera goes live.  If a blend is involved,
        /// then the event will fire on the first frame of the blend</summary>
        [Tooltip("This event will fire whenever a virtual camera goes live.  If a blend is involved, then the event will fire on the first frame of the blend.")]
        public VcamActivatedEvent m_CameraActivatedEvent = new VcamActivatedEvent();

        /// <summary>This event will fire after a brain updates its Camera</summary>
        public static BrainEvent CameraUpdatedEvent = new BrainEvent();

        /// <summary>
        /// API for the Unity Editor.
        /// Show this camera no matter what.  This is static, and so affects all Cinemachine brains.
        /// </summary>
        public static ICinemachineCamera SoloCamera
        {
            get { return mSoloCamera; }
            set
            {
                if (value != null && !CinemachineCore.Instance.IsLive(value))
                    value.OnTransitionFromCamera(null, Vector3.up, Time.deltaTime);
                mSoloCamera = value;
            }
        }

        /// <summary>API for the Unity Editor.</summary>
        /// <returns>Color used to indicate that a camera is in Solo mode.</returns>
        public static Color GetSoloGUIColor() { return Color.Lerp(Color.red, Color.yellow, 0.8f); }

        /// <summary>Get the default world up for the virtual cameras.</summary>
        public Vector3 DefaultWorldUp
            { get { return (m_WorldUpOverride != null)
                ? m_WorldUpOverride.up : Vector3.up; } }

        /// <summary>Get the default world orientation for the virtual cameras.</summary>
        public Quaternion DefaultWorldOrientation
            { get { return (m_WorldUpOverride != null)
                ? m_WorldUpOverride.rotation : Quaternion.identity; } }

        private static ICinemachineCamera mSoloCamera;

        private void OnEnable()
        {
            // Make sure there is a first stack frame
            if (mFrameStack.Count == 0)
                mFrameStack.Add(new BrainFrame());

            m_OutputCamera = GetComponent<Camera>();
            sActiveBrains.Insert(0, this);
            CinemachineDebug.OnGUIHandlers -= OnGuiHandler;
            CinemachineDebug.OnGUIHandlers += OnGuiHandler;
        }

        private void OnDisable()
        {
            CinemachineDebug.OnGUIHandlers -= OnGuiHandler;
            sActiveBrains.Remove(this);
            mFrameStack.Clear();
        }

        private void OnGuiHandler()
        {
            if (!m_ShowDebugText)
                CinemachineDebug.ReleaseScreenPos(this);
            else
            {
                // Show the active camera and blend
                var sb = CinemachineDebug.SBFromPool();
                Color color = GUI.color;
                sb.Length = 0;
                sb.Append("CM ");
                sb.Append(gameObject.name);
                sb.Append(": ");
                if (SoloCamera != null)
                {
                    sb.Append("SOLO ");
                    GUI.color = GetSoloGUIColor();
                }

                if (IsBlending)
                    sb.Append(ActiveBlend.Description());
                else
                {
                    ICinemachineCamera vcam = ActiveVirtualCamera;
                    if (vcam == null)
                        sb.Append("(none)");
                    else
                    {
                        sb.Append("[");
                        sb.Append(vcam.Name);
                        sb.Append("]");
                    }
                }
                string text = sb.ToString();
                Rect r = CinemachineDebug.GetScreenPos(this, text, GUI.skin.box);
                GUI.Label(r, text, GUI.skin.box);
                GUI.color = color;
                CinemachineDebug.ReturnToPool(sb);
            }
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (CinemachineDebug.OnGUIHandlers != null)
                CinemachineDebug.OnGUIHandlers();
        }
#endif

        private void LateUpdate()
        {
            float deltaTime = GetEffectiveDeltaTime(false);
            UpdateFrame0(deltaTime);
            UpdateCurrentLiveCameras();

            // Choose the active vcam and apply it to the Unity camera
            ProcessActiveCamera(deltaTime);
        }

#if UNITY_EDITOR
        /// This is only needed in editor mode to force timeline to call OnGUI while
        /// timeline is up and the game is not running, in order to allow dragging
        /// the composer guide in the game view.
        private void OnPreCull()
        {
            if (!Application.isPlaying)
            {
                // Note: this call will cause any screen canvas attached to the camera
                // to be painted one frame out of sync.  It will only happen in the editor when not playing.
                ProcessActiveCamera(GetEffectiveDeltaTime(false));
            }
        }
#endif

        /// <summary>List of all active CinemachineBrains.</summary>
        private static List<CM_Brain> sActiveBrains = new List<CM_Brain>();

        /// <summary>Access the array of active CinemachineBrains in the scene</summary>
        public static int BrainCount { get { return sActiveBrains.Count; } }

        /// <summary>Access the array of active CinemachineBrains in the scene
        /// without gebnerating garbage</summary>
        /// <param name="index">Index of the brain to access, range 0-BrainCount</param>
        /// <returns>The brain at the specified index</returns>
        public static CM_Brain GetActiveBrain(int index)
        {
            return sActiveBrains[index];
        }

        private float GetEffectiveDeltaTime(bool fixedDelta)
        {
            if (SoloCamera != null)
                return Time.unscaledDeltaTime;

            if (!Application.isPlaying)
            {
                for (int i = mFrameStack.Count - 1; i > 0; --i)
                {
                    var frame = mFrameStack[i];
                    if (frame.Active)
                        return frame.TimeOverrideExpired ? -1 : frame.deltaTimeOverride;
                }
                return -1;
            }
            if (m_IgnoreTimeScale)
                return fixedDelta ? Time.fixedDeltaTime : Time.unscaledDeltaTime;
            return fixedDelta ? Time.fixedDeltaTime * Time.timeScale : Time.deltaTime;
        }

        /// <summary>
        /// Get the current active virtual camera.
        /// </summary>
        public ICinemachineCamera ActiveVirtualCamera
        {
            get
            {
                if (SoloCamera != null)
                    return SoloCamera;
                return mCurrentLiveCameras.DeepCamB();
            }
        }

        /// <summary>
        /// Is there a blend in progress?
        /// </summary>
        public bool IsBlending { get { return ActiveBlend != null; } }

        /// <summary>
        /// Get the current blend in progress.  Returns null if none.
        /// </summary>
        public CinemachineBlend ActiveBlend
        {
            get
            {
                if (SoloCamera != null)
                    return null;
                if (mCurrentLiveCameras.CamA == null || mCurrentLiveCameras.IsComplete)
                    return null;
                return mCurrentLiveCameras;
            }
        }

        private class BrainFrame
        {
            public int id;
            public CinemachineBlend blend = new CinemachineBlend(null, null, BlendCurve.Default, 0, 0);
            public bool Active { get { return blend.IsValid; } }

            // Working data - updated every frame
            public CinemachineBlend workingBlend = new CinemachineBlend(null, null, BlendCurve.Default, 0, 0);
            public BlendSourceVirtualCamera workingBlendSource = new BlendSourceVirtualCamera(null);

            // Used by Timeline Preview for overriding the current value of deltaTime
            public float deltaTimeOverride;
            public float timeOfOverride;
            public bool TimeOverrideExpired
            {
                get { return Time.realtimeSinceStartup - timeOfOverride > Time.maximumDeltaTime; }
            }
        }

        // Current game state is always frame 0, overrides are subsequent frames
        private List<BrainFrame> mFrameStack = new List<BrainFrame>();
        private int mNextFrameId = 1;

        /// Get the frame index corresponding to the ID
        private int GetBrainFrame(int withId)
        {
            int count = mFrameStack.Count;
            for (int i = mFrameStack.Count - 1; i > 0; --i)
                if (mFrameStack[i].id == withId)
                    return i;
            // Not found - add it
            mFrameStack.Add(new BrainFrame() { id = withId });
            return mFrameStack.Count - 1;
        }

        // Current Brain State - result of all frames.  Blend camB is "current" camera always
        CinemachineBlend mCurrentLiveCameras = new CinemachineBlend(null, null, BlendCurve.Default, 0, 0);

        /// <summary>
        /// This API is specifically for Timeline.  Do not use it.
        /// Override the current camera and current blend.  This setting will trump
        /// any in-game logic that sets virtual camera priorities and Enabled states.
        /// This is the main API for the timeline.
        /// </summary>
        /// <param name="overrideId">Id to represent a specific client.  An internal
        /// stack is maintained, with the most recent non-empty override taking precenence.
        /// This id must be > 0.  If you pass -1, a new id will be created, and returned.
        /// Use that id for subsequent calls.  Don't forget to
        /// call ReleaseCameraOverride after all overriding is finished, to
        /// free the OverideStack resources.</param>
        /// <param name="camA"> The camera to set, corresponding to weight=0</param>
        /// <param name="camB"> The camera to set, corresponding to weight=1</param>
        /// <param name="weightB">The blend weight.  0=camA, 1=camB</param>
        /// <param name="deltaTime">override for deltaTime.  Should be Time.FixedDelta for
        /// time-based calculations to be included, -1 otherwise</param>
        /// <returns>The oiverride ID.  Don't forget to call ReleaseCameraOverride
        /// after all overriding is finished, to free the OverideStack resources.</returns>
        internal int SetCameraOverride(
            int overrideId,
            ICinemachineCamera camA, ICinemachineCamera camB,
            float weightB, float deltaTime)
        {
            if (overrideId < 0)
                overrideId = mNextFrameId++;

            BrainFrame frame = mFrameStack[GetBrainFrame(overrideId)];
            frame.deltaTimeOverride = deltaTime;
            frame.timeOfOverride = Time.realtimeSinceStartup;
            frame.blend.CamA = camA;
            frame.blend.CamB = camB;
            frame.blend.BlendCurve = BlendCurve.Linear;
            frame.blend.Duration = 1;
            frame.blend.TimeInBlend = weightB;

            return overrideId;
        }

        /// <summary>
        /// This API is specifically for Timeline.  Do not use it.
        /// Release the resources used for a camera override client.
        /// See SetCameraOverride.
        /// </summary>
        /// <param name="overrideId">The ID to released.  This is the value that
        /// was returned by SetCameraOverride</param>
        internal void ReleaseCameraOverride(int overrideId)
        {
            for (int i = mFrameStack.Count - 1; i > 0; --i)
            {
                if (mFrameStack[i].id == overrideId)
                {
                    mFrameStack.RemoveAt(i);
                    return;
                }
            }
        }

        ICinemachineCamera mActiveCameraPreviousFrame;
        private void ProcessActiveCamera(float deltaTime)
        {
            var activeCamera = ActiveVirtualCamera;
            if (activeCamera != null)
            {
                // Has the current camera changed this frame?
                if (activeCamera != mActiveCameraPreviousFrame)
                {
                    // Notify incoming camera of transition
                    activeCamera.OnTransitionFromCamera(
                        mActiveCameraPreviousFrame, DefaultWorldUp, deltaTime);
                    if (m_CameraActivatedEvent != null)
                        m_CameraActivatedEvent.Invoke(activeCamera, mActiveCameraPreviousFrame);

                    // If we're cutting without a blend, send an event
                    if (m_CameraCutEvent != null && !IsBlending)
                        m_CameraCutEvent.Invoke(this);
                }
                // Apply the vcam state to the Unity camera
                PushStateToUnityCamera(
                    SoloCamera != null ? SoloCamera.State : mCurrentLiveCameras.State);
            }
            mActiveCameraPreviousFrame = activeCamera;
        }

        private void UpdateFrame0(float deltaTime)
        {
            // Update the in-game frame (frame 0)
            BrainFrame frame = mFrameStack[0];

            // Are we transitioning cameras?
            var activeCamera = TopCameraFromPriorityQueue();
            var outGoingCamera = frame.blend.CamB;
            if (activeCamera != outGoingCamera)
            {
                // Do we need to create a game-play blend?
                if (activeCamera != null && activeCamera.IsValid
                    && outGoingCamera != null && outGoingCamera.IsValid && deltaTime >= 0)
                {
                    // Create a blend (time will be 0 if a cut)
                    var blendDef = LookupBlend(outGoingCamera, activeCamera);
                    if (blendDef.m_Time > 0)
                    {
                        if (frame.blend.IsComplete)
                            frame.blend.CamA = outGoingCamera;  // new blend
                        else // chain to existing blend
                            frame.blend.CamA = new BlendSourceVirtualCamera(
                                new CinemachineBlend(
                                    frame.blend.CamA, frame.blend.CamB,
                                    frame.blend.BlendCurve, frame.blend.Duration, frame.blend.TimeInBlend));

                        frame.blend.BlendCurve = blendDef.BlendCurve;
                        frame.blend.Duration = blendDef.m_Time;
                        frame.blend.TimeInBlend = 0;
                    }
                }
                // Set the current active camera
                frame.blend.CamB = activeCamera;
            }

            // Advance the current blend (if any)
            if (frame.blend.CamA != null)
            {
                frame.blend.TimeInBlend += (deltaTime >= 0) ? deltaTime : frame.blend.Duration;
                if (frame.blend.IsComplete)
                {
                    // No more blend
                    frame.blend.CamA = null;
                    frame.blend.Duration = 0;
                    frame.blend.TimeInBlend = 0;
                }
            }
        }

        private void UpdateCurrentLiveCameras()
        {
            // Resolve the current working frame states in the stack
            int lastActive = 0;
            for (int i = 0; i < mFrameStack.Count; ++i)
            {
                BrainFrame frame = mFrameStack[i];
                if (i == 0 || frame.Active)
                {
                    frame.workingBlend.CamA = frame.blend.CamA;
                    frame.workingBlend.CamB = frame.blend.CamB;
                    frame.workingBlend.BlendCurve = frame.blend.BlendCurve;
                    frame.workingBlend.Duration = frame.blend.Duration;
                    frame.workingBlend.TimeInBlend = frame.blend.TimeInBlend;
                    if (i > 0 && !frame.blend.IsComplete)
                    {
                        if (frame.workingBlend.CamA == null)
                        {
                            if (mFrameStack[lastActive].blend.IsComplete)
                                frame.workingBlend.CamA = mFrameStack[lastActive].blend.CamB;
                            else
                            {
                                frame.workingBlendSource.Blend = mFrameStack[lastActive].workingBlend;
                                frame.workingBlend.CamA = frame.workingBlendSource;
                            }
                        }
                        else if (frame.workingBlend.CamB == null)
                        {
                            if (mFrameStack[lastActive].blend.IsComplete)
                                frame.workingBlend.CamB = mFrameStack[lastActive].blend.CamB;
                            else
                            {
                                frame.workingBlendSource.Blend = mFrameStack[lastActive].workingBlend;
                                frame.workingBlend.CamB = frame.workingBlendSource;
                            }
                        }
                    }
                    lastActive = i;
                }
            }
            var workingBlend = mFrameStack[lastActive].workingBlend;
            mCurrentLiveCameras.CamA = workingBlend.CamA;
            mCurrentLiveCameras.CamB = workingBlend.CamB;
            mCurrentLiveCameras.BlendCurve = workingBlend.BlendCurve;
            mCurrentLiveCameras.Duration = workingBlend.Duration;
            mCurrentLiveCameras.TimeInBlend = workingBlend.TimeInBlend;
        }

        /// <summary>
        /// True if the ICinemachineCamera the current active camera,
        /// or part of a current blend, either directly or indirectly because its parents are live.
        /// </summary>
        /// <param name="vcam">The camera to test whether it is live</param>
        /// <returns>True if the camera is live (directly or indirectly)
        /// or part of a blend in progress.</returns>
        public bool IsLive(ICinemachineCamera vcam)
        {
            if (SoloCamera == vcam)
                return true;
            if (mCurrentLiveCameras.Uses(vcam))
                return true;

            ICinemachineCamera parent = vcam.ParentCamera;
            while (parent != null && parent.IsLiveChild(vcam))
            {
                if (mCurrentLiveCameras.Uses(parent))
                    return true;
                vcam = parent;
                parent = vcam.ParentCamera;
            }
            return false;
        }

        /// <summary>
        /// The current state applied to the unity camera (may be the result of a blend)
        /// </summary>
        public CameraState CurrentCameraState { get; private set; }

        /// <summary>
        /// Get the highest-priority Enabled ICinemachineCamera
        /// that is visible to my camera.  Culling Mask is used to test visibility.
        /// </summary>
        private ICinemachineCamera TopCameraFromPriorityQueue()
        {
            var prioritySystem = World.Active.GetExistingManager<CM_VcamPrioritySystem>();
            if (prioritySystem != null)
            {
                Camera outputCamera = OutputCamera;
                int mask = outputCamera == null ? ~0 : outputCamera.cullingMask;
                var queue = prioritySystem.GetPriorityQueueNow();
                for (int i = 0; i < queue.Length; ++i)
                {
                    var e = queue[i];
                    if ((mask & (1 << e.vcamPriority.vcamLayer)) != 0)
                        return CM_EntityVcam.GetEntityVcam(e.entity);
                }
            }
            return null;
        }

        /// <summary>
        /// Create a blend curve for blending from one ICinemachineCamera to another.
        /// If there is a specific blend defined for these cameras it will be used, otherwise
        /// a default blend will be created, which could be a cut.
        /// </summary>
        private CinemachineBlendDefinition LookupBlend(
            ICinemachineCamera fromKey, ICinemachineCamera toKey)
        {
            // Get the blend curve that's most appropriate for these cameras
            CinemachineBlendDefinition blend = m_DefaultBlend;
            if (m_CustomBlends != null)
            {
                string fromCameraName = (fromKey != null) ? fromKey.Name : string.Empty;
                string toCameraName = (toKey != null) ? toKey.Name : string.Empty;
                blend = m_CustomBlends.GetBlendForVirtualCameras(
                        fromCameraName, toCameraName, blend);
            }
            return blend;
        }

        /// <summary> Apply a cref="CameraState"/> to the game object</summary>
        private void PushStateToUnityCamera(CameraState state)
        {
            CurrentCameraState = state;
            if ((state.BlendHint & CameraState.BlendHintValue.NoPosition) == 0)
                transform.position = state.FinalPosition;
            if ((state.BlendHint & CameraState.BlendHintValue.NoOrientation) == 0)
                transform.rotation = state.FinalOrientation;
            if ((state.BlendHint & CameraState.BlendHintValue.NoLens) == 0)
            {
                Camera cam = OutputCamera;
                if (cam != null)
                {
                    cam.nearClipPlane = state.Lens.NearClipPlane;
                    cam.farClipPlane = state.Lens.FarClipPlane;
                    cam.fieldOfView = state.Lens.FieldOfView;
                    if (cam.orthographic)
                        cam.orthographicSize = state.Lens.OrthographicSize;
#if UNITY_2018_2_OR_NEWER
                    else
                    {
                        cam.usePhysicalProperties = state.Lens.IsPhysicalCamera;
                        cam.lensShift = state.Lens.LensShift;
                    }
#endif
                }
            }
            if (CameraUpdatedEvent != null)
                CameraUpdatedEvent.Invoke(this);
        }
    }
}

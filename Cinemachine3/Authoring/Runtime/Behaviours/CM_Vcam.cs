using UnityEngine;
using Unity.Transforms;
using Cinemachine;
using Unity.Cinemachine.Common;
using Unity.Entities;

namespace Unity.Cinemachine3.Authoring
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Cinemachine/CM_Vcam")]
    public class CM_Vcam : CM_VcamBase
    {
        /// <summary>Object for the camera children wants to move with (the body target)</summary>
        [Tooltip("Object for the camera children wants to move with (the body target).")]
        [NoSaveDuringPlay]
        public Transform followTarget = null;

        /// <summary>Object for the camera children to look at (the aim target)</summary>
        [Tooltip("Object for the camera children to look at (the aim target).")]
        [NoSaveDuringPlay]
        public Transform lookAtTarget = null;

        /// <summary>Specifies the LensSettings of this Virtual Camera.
        /// These settings will be transferred to the Unity camera when the vcam is live.</summary>
        [Tooltip("Specifies the lens properties of this Virtual Camera.  This generally mirrors "
            + "the Unity Camera's lens settings, and will be used to drive the Unity camera when "
            + "the vcam is active.")]
        [LensSettingsProperty]
        public LensSettings lens = LensSettings.Default;

        /// <summary>API for the editor, to make the dragging of position handles behave better.</summary>
        public bool UserIsDragging { get; set; }

        protected override void PushValuesToEntityComponents()
        {
            base.PushValuesToEntityComponents();

            var goh = new GameObjectEntityHelper(transform, true);
            goh.EnsureTransformCompliance();
            goh.SafeSetComponentData(new CM_VcamLens
            {
                fov = lens.Orthographic ? lens.OrthographicSize : lens.FieldOfView,
                nearClip = lens.NearClipPlane,
                farClip = lens.FarClipPlane,
                dutch = lens.Dutch,
                lensShift = lens.LensShift
            });

            var th = new GameObjectEntityHelper(followTarget, true);
            th.EnsureTransformCompliance();
            th.SafeAddComponentData(new CM_Target());
            goh.SafeSetComponentData(new CM_VcamFollowTarget{ target = th.Entity });

            th = new GameObjectEntityHelper(lookAtTarget, true);
            th.EnsureTransformCompliance();
            th.SafeAddComponentData(new CM_Target());
            goh.SafeSetComponentData(new CM_VcamLookAtTarget{ target = th.Entity });
        }
    }
}

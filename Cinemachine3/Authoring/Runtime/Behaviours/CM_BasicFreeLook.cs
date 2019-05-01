﻿using UnityEngine;
using Unity.Entities;
using Unity.Cinemachine.Common;

namespace Unity.Cinemachine3.Authoring
{
    /// <summary>
    /// Simple FreeLook version of the virtual camera, just spline-driven orbital position
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [SaveDuringPlay]
    [AddComponentMenu("Cinemachine/CM_BasicFreeLook")]
    public class CM_BasicFreeLook : CM_Vcam
    {
        public CM_InputAxisDriver horizontalInput;
        public CM_InputAxisDriver verticalInput;
        public CM_InputAxisDriver radialInput;

        protected virtual void Reset()
        {
            horizontalInput = new CM_InputAxisDriver
            {
                multiplier = -0.4f,
                accelTime = 0.2f,
                decelTime = 0.2f,
                name = "Mouse X",
            };
            verticalInput = new CM_InputAxisDriver
            {
                multiplier = 1,
                accelTime = 0.2f,
                decelTime = 0.2f,
                name = "Mouse Y",
            };
            radialInput = new CM_InputAxisDriver
            {
                multiplier = 0.25f,
                accelTime = 0.2f,
                decelTime = 0.2f,
                name = "Mouse ScrollWheel",
            };
        }

        protected override void OnValidate()
        {
            horizontalInput.Validate();
            verticalInput.Validate();
            radialInput.Validate();
            base.OnValidate();
        }

        protected override void Update()
        {
            base.Update();

            if (HasComponent<CM_VcamOrbital>())
            {
                var orbital = SafeGetComponentData<CM_VcamOrbital>();

                // Update our axes
                bool changed = horizontalInput.Update(Time.deltaTime, ref orbital.horizontalAxis);
                changed |= verticalInput.Update(Time.deltaTime, ref orbital.verticalAxis);
                changed |= radialInput.Update(Time.deltaTime, ref orbital.radialAxis);
                if (changed)
                {
                    orbital.horizontalAxis.CancelRecentering();
                    orbital.verticalAxis.CancelRecentering();
                    orbital.radialAxis.CancelRecentering();
                }
                SafeSetComponentData(orbital);
            }
        }
    }
}

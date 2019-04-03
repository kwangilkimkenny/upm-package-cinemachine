﻿using UnityEngine;
using Cinemachine.ECS;
using Unity.Mathematics;
using System;

namespace Cinemachine.ECS_Hybrid
{
    [DisallowMultipleComponent]
    [SaveDuringPlay]
    public class CM_PathProxy : CM_ComponentProxyBase<CM_Path>
    {
        /// <summary>This class holds the settings that control how the path
        /// will appear in the editor scene view.  The path is not visible in the game view</summary>
        [DocumentationSorting(DocumentationSortingAttribute.Level.UserRef)]
        [Serializable] public struct Appearance
        {
            [Tooltip("The color of the path itself when it is active in the editor")]
            public Color pathColor;
            [Tooltip("The color of the path itself when it is inactive in the editor")]
            public Color inactivePathColor;
            [Tooltip("The width of the railroad-tracks that are drawn to represent the path")]
            [Range(0f, 10f)]
            public float width;
        }

        /// <summary>The settings that control how the path
        /// will appear in the editor scene view.</summary>
        [Tooltip("The settings that control how the path will appear in the editor scene view.")]
        public Appearance appearance;

        private void OnValidate()
        {
            var v = Value;
            v.resolution = math.max(1, v.resolution);
            Value = v;
        }

        private void Reset()
        {
            Value = new CM_Path
            {
                looped = false,
                resolution = 10
            };
            appearance = new Appearance
            {
                pathColor = Color.green,
                inactivePathColor = Color.gray,
                width = 0.2f
            };
        }
    }
}

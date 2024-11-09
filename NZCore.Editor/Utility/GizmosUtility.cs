// <copyright project="NZCore.Editor" file="GizmosUtility.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;
using UnityEngine;

namespace NZCore.Editor
{
    public static class GizmosUtility
    {
        public static void DrawWireCapsule(Vector3 point1, Vector3 point2, float radius)
        {
            Vector3 upOffset = point2 - point1;
            Vector3 up = upOffset.Equals(default) ? Vector3.up : upOffset.normalized;
            Quaternion orientation = Quaternion.FromToRotation(Vector3.up, up);
            Vector3 forward = orientation * Vector3.forward;
            Vector3 right = orientation * Vector3.right;
            // z axis
            Handles.DrawWireArc(point2, forward, right, 180, radius);
            Handles.DrawWireArc(point1, forward, right, -180, radius);
            Handles.DrawLine(point1 + right * radius, point2 + right * radius);
            Handles.DrawLine(point1 - right * radius, point2 - right * radius);
            // x axis
            Handles.DrawWireArc(point2, right, forward, -180, radius);
            Handles.DrawWireArc(point1, right, forward, 180, radius);
            Handles.DrawLine(point1 + forward * radius, point2 + forward * radius);
            Handles.DrawLine(point1 - forward * radius, point2 - forward * radius);
            // y axis
            Handles.DrawWireDisc(point2, up, radius);
            Handles.DrawWireDisc(point1, up, radius);
        }
    }
}
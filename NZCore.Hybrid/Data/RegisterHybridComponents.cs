// <copyright project="NZCore.Hybrid" file="RegisterHybridComponents.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;
using UnityEngine;

#if ENTITIES_1_4_0
[assembly:RegisterUnityEngineComponentType(typeof(Camera))]
[assembly:RegisterUnityEngineComponentType(typeof(Animator))]
#endif
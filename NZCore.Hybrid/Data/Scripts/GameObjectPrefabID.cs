// <copyright project="NZCore.Hybrid" file="GameObjectPrefabID.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine;
using UnityEngine.Serialization;

namespace NZCore.Hybrid
{
    public class GameObjectPrefabID : MonoBehaviour
    {
#if UNITY_6000_4_OR_NEWER
        public EntityId PrefabId;
#else
        public int PrefabId;
#endif
    }
}
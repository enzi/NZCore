// <copyright project="NZCore.Editor" file="ScriptableObjectConverter.cs" version="1.0.0">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NZCore
{
    [ExecuteInEditMode]
    public abstract class ScriptableObjectConverterBase<SOType> : MonoBehaviour
        where SOType : ScriptableObject
    {
        public bool ForceUpdate;

        public List<string> ScriptableObjects;

        public void GatherScriptableObjects()
        {
            ScriptableObjects = new List<string>();

            //Debug.Log($"Find t: {typeof(SOType)}");
            var guids = AssetDatabase.FindAssets("t: " + typeof(SOType));

            Array.Sort(guids);

            foreach (var guid in guids)
            {
                ScriptableObjects.Add(guid);
            }
        }

        public void Update()
        {
            if (!ForceUpdate)
                return;

            ForceUpdate = false;
            GatherScriptableObjects();
        }
    }
}
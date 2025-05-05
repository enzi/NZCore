// <copyright project="NZCore.Editor" file="DomainReloadUtility.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;

namespace NZCore.Editor.Utility
{
    public static class DomainReloadUtility
    {
        [MenuItem("Tools/NZCore/Trigger Domain Reload")]
        public static void TriggerDomainReload()
        {
            EditorUtility.RequestScriptReload();
        }
    }
}
// <copyright project="NZCore.Editor" file="AssemblyUtility.cs">
// Copyright (c) 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_6000_4_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace NZCore.Editor
{
    public static class AssemblyUtility
    {
        public static IReadOnlyList<Assembly> GetLoadedAssemblies()
        {
#if UNITY_6000_4_OR_NEWER
            return CurrentAssemblies.GetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }
    }
}

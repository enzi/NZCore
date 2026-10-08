// <copyright project="NZCore.Editor" file="SourceGeneratorScopeBuildFilter.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace NZCore.Editor
{
    /// <summary>
    /// Source generator scope assemblies (asmdefs named *.SourceGenerators) only exist so their analyzers apply to the
    /// assemblies that reference them. They have to be compiled for players too, otherwise the generators don't run in
    /// player compiles, but they contain no code and nothing references them at runtime, so they are not shipped.
    /// </summary>
    internal class SourceGeneratorScopeBuildFilter : IFilterBuildAssemblies
    {
        private const string ScopeAssemblySuffix = ".SourceGenerators.dll";

        // Generator attributes are [Conditional("UNITY_EDITOR")], so player assemblies never reference them.
        private const string AttributesAssemblySuffix = ".SourceGenerator.Attributes.dll";

        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            return assemblies
                   .Where(assembly => !assembly.EndsWith(ScopeAssemblySuffix, StringComparison.Ordinal) &&
                                      !assembly.EndsWith(AttributesAssemblySuffix, StringComparison.Ordinal))
                   .ToArray();
        }
    }
}
// <copyright project="MVVM.SourceGenerator" file="PropertyNotificationGenerator.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

[Generator]
public class PropertyNotificationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Filter for properties with NotifyChanged attribute
        var propertyDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsNotifyChangedProperty(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine with compilation
        var compilationAndProperties = context.CompilationProvider.Combine(propertyDeclarations.Collect());

        // Generate source
        context.RegisterSourceOutput(compilationAndProperties,
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static bool IsNotifyChangedProperty(SyntaxNode node)
    {
        return node is PropertyDeclarationSyntax property &&
               property.AttributeLists.Any(list =>
                   list.Attributes.Any(attr =>
                       attr.Name.ToString().Contains("NotifyValueChanged") ||
                       attr.Name.ToString().Contains("NotifyPropertyChanged")));
    }

    private static PropertyDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        return (PropertyDeclarationSyntax)context.Node;
    }

    private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<PropertyDeclarationSyntax> properties)
    {
        var notifyValueChangedAttributeSymbol = compilation.GetTypeByMetadataName("NZCore.MVVM.NotifyValueChangedAttribute");
        var notifyPropertyChangedAttributeSymbol = compilation.GetTypeByMetadataName("NZCore.MVVM.NotifyPropertyChangedAttribute");

        var currentCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        try
        {
            // Group properties by class with attribute information
            var classesByType =
                new Dictionary<INamedTypeSymbol,
                    List<(PropertyDeclarationSyntax property, IPropertySymbol propertySymbol, bool isValueChanged, bool isPropertyChanged)>>(
                    SymbolEqualityComparer.Default);

            foreach (var property in properties)
            {
                var semanticModel = compilation.GetSemanticModel(property.SyntaxTree);

                if (semanticModel.GetDeclaredSymbol(property) is not IPropertySymbol propertySymbol)
                    continue;

                // Check which attributes are present
                var attributes = propertySymbol.GetAttributes();
                var isValueChanged = attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, notifyValueChangedAttributeSymbol));
                var isPropertyChanged = attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, notifyPropertyChangedAttributeSymbol));

                if (property.Parent is ClassDeclarationSyntax classDeclaration)
                {
                    var containingType = semanticModel.GetDeclaredSymbol(classDeclaration);
                    if (containingType != null)
                    {
                        if (!classesByType.ContainsKey(containingType))
                            classesByType[containingType] = new List<(PropertyDeclarationSyntax, IPropertySymbol, bool, bool)>();

                        classesByType[containingType].Add((property, propertySymbol, isValueChanged, isPropertyChanged));
                    }
                }
            }

            // Generate extension methods for each class
            foreach (var classGroup in classesByType)
            {
                var containingType = classGroup.Key;
                var propertyInfos = classGroup.Value;

                // Generate extension methods for the class
                var extensionSource = GenerateExtensionMethods(containingType, propertyInfos);
                if (extensionSource != null)
                {
                    var extensionFileName = $"{containingType.ContainingNamespace}.{containingType.Name}.PropertyNotificationExtensions.gen.cs";
                    context.AddSource(extensionFileName, extensionSource);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = currentCulture;
        }
    }

    private static string GenerateExtensionMethods(INamedTypeSymbol containingType, List<(PropertyDeclarationSyntax property, IPropertySymbol propertySymbol, bool isValueChanged, bool isPropertyChanged)> propertyInfos)
    {
        var namespaceName = containingType.ContainingNamespace.ToDisplayString();
        var className = containingType.Name;
        var fullTypeName = $"{namespaceName}.{className}";

        var extensionMethods = new List<string>();

        foreach (var (property, propertySymbol, isValueChanged, isPropertyChanged) in propertyInfos)
        {
            var propertyName = propertySymbol.Name;
            var propertyType = propertySymbol.Type.ToDisplayString();

            // Generate NotifyValueChanged method (old + new values)
            if (isValueChanged)
            {
                var methodName = $"On{propertyName}ValueChanged";

                var extensionMethod = $@"        /// <summary>
        /// Subscribes to value changes of the {propertyName} property (receives old and new values).
        /// </summary>
        /// <param name=""model"">The model to subscribe to.</param>
        /// <param name=""handler"">The handler to call when the property changes.</param>
        /// <returns>An IDisposable to unsubscribe from the event.</returns>
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public static global::System.IDisposable {methodName}(this {fullTypeName} model, global::System.Action<{propertyType}, {propertyType}> handler)
        {{
            if (model == null) throw new global::System.ArgumentNullException(nameof(model));
            if (handler == null) throw new global::System.ArgumentNullException(nameof(handler));

            global::NZCore.MVVM.PropertyValueChangedHandler wrapper = (propertyName, oldValue, newValue) =>
            {{
                if (propertyName == nameof({fullTypeName}.{propertyName}) &&
                    oldValue is {propertyType} oldVal &&
                    newValue is {propertyType} newVal)
                {{
                    handler(oldVal, newVal);
                }}
            }};

            model.PropertyValueChanged += wrapper;
            return new global::NZCore.MVVM.UnsubscribeAction(() => model.PropertyValueChanged -= wrapper);
        }}";

                extensionMethods.Add(extensionMethod);
            }

            // Generate NotifyPropertyChanged method (new value only)
            if (isPropertyChanged)
            {
                var methodName = $"On{propertyName}PropertyChanged";

                var extensionMethod = $@"        /// <summary>
        /// Subscribes to changes of the {propertyName} property (receives new value only).
        /// </summary>
        /// <param name=""model"">The model to subscribe to.</param>
        /// <param name=""handler"">The handler to call when the property changes.</param>
        /// <returns>An IDisposable to unsubscribe from the event.</returns>
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public static global::System.IDisposable {methodName}(this {fullTypeName} model, global::System.Action<{propertyType}> handler)
        {{
            if (model == null) throw new global::System.ArgumentNullException(nameof(model));
            if (handler == null) throw new global::System.ArgumentNullException(nameof(handler));

            global::NZCore.MVVM.PropertyValueChangedHandler wrapper = (propertyName, oldValue, newValue) =>
            {{
                if (propertyName == nameof({fullTypeName}.{propertyName}) &&
                    newValue is {propertyType} newVal)
                {{
                    handler(newVal);
                }}
            }};

            model.PropertyValueChanged += wrapper;
            return new global::NZCore.MVVM.UnsubscribeAction(() => model.PropertyValueChanged -= wrapper);
        }}";

                extensionMethods.Add(extensionMethod);
            }
        }

        if (extensionMethods.Count == 0)
            return null;

        var source = $@"// <auto-generated />
#nullable enable

namespace {namespaceName}
{{
    /// <summary>
    /// Extension methods for property-specific subscriptions on {className}.
    /// </summary>
    [global::System.Runtime.CompilerServices.CompilerGenerated]
    public static class {className}PropertyNotificationExtensions
    {{
{string.Join(Environment.NewLine + Environment.NewLine, extensionMethods)}
    }}
}}
";

        return source;
    }
}
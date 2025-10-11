// <copyright project="MVVM.SourceGenerator" file="ObservableObjectGenerator.cs">
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
public class ObservablePropertyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Filter for classes with ObservableObject attribute
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsObservablePropertyField(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate source
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static bool IsObservablePropertyField(SyntaxNode node)
    {
        return node is FieldDeclarationSyntax field &&
               field.AttributeLists.Any(list =>
                   list.Attributes.Any(attr =>
                       attr.Name.ToString().Contains("ObservableProperty")));
    }

    private static FieldDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        return (FieldDeclarationSyntax) context.Node;
    }

    private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<FieldDeclarationSyntax> fields)
    {
        var observablePropertyAttributeSymbol = compilation.GetTypeByMetadataName("NZCore.MVVM.ObservablePropertyAttribute");
        var alsoNotifyAttributeSymbol = compilation.GetTypeByMetadataName("NZCore.MVVM.AlsoNotifyAttribute");
        
        var currentCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        try
        {
            // Group fields by class to generate both properties and extension methods
            var classesByType =
                new Dictionary<INamedTypeSymbol,
                    List<(FieldDeclarationSyntax field, VariableDeclaratorSyntax variable, IFieldSymbol fieldSymbol)>>(
                    SymbolEqualityComparer.Default);

            foreach (var field in fields)
            {
                var semanticModel = compilation.GetSemanticModel(field.SyntaxTree);

                foreach (var variable in field.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol)
                        continue;
                    
                    if (field.Parent is ClassDeclarationSyntax classDeclaration)
                    {
                        var containingType = semanticModel.GetDeclaredSymbol(classDeclaration);
                        if (containingType != null)
                        {
                            if (!classesByType.ContainsKey(containingType))
                                classesByType[containingType] = new List<(FieldDeclarationSyntax, VariableDeclaratorSyntax, IFieldSymbol)>();
                            
                            classesByType[containingType].Add((field, variable, fieldSymbol));
                        }
                    }
                }
            }

            // Generate property and extension methods for each class
            foreach (var classGroup in classesByType)
            {
                var containingType = classGroup.Key;
                var fieldInfos = classGroup.Value;

                foreach (var (field, variable, fieldSymbol) in fieldInfos)
                {
                    var semanticModel = compilation.GetSemanticModel(field.SyntaxTree);
                    var source = GenerateSourceObservableProperty(field, variable, fieldSymbol, alsoNotifyAttributeSymbol, semanticModel);
                    
                    if (source != null)
                    {
                        var propertyName = GetPropertyName(fieldSymbol.Name);
                        var fileName = $"{containingType.ContainingNamespace}.{containingType.Name}.{propertyName}.gen.cs";
                        context.AddSource(fileName, source);
                    }
                }

                // Generate extension methods for the class
                var extensionSource = GenerateExtensionMethods(containingType, fieldInfos);
                if (extensionSource != null)
                {
                    var extensionFileName = $"{containingType.ContainingNamespace}.{containingType.Name}.Extensions.gen.cs";
                    context.AddSource(extensionFileName, extensionSource);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = currentCulture;
        }
    }

    private static string GenerateSourceObservableProperty(FieldDeclarationSyntax fieldSyntax, VariableDeclaratorSyntax variable,
        IFieldSymbol fieldSymbol, INamedTypeSymbol alsoNotifyChangeForAttributeSymbol, SemanticModel semanticModel)
    {
        if (fieldSyntax.Parent is not ClassDeclarationSyntax classDeclaration)
            return null;
        
        var alsoNotifyAttributes = fieldSymbol.GetAttributes()
            .Where(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, alsoNotifyChangeForAttributeSymbol))
            .SelectMany(ad => ad.ConstructorArguments.Select(ca => ca.Value?.ToString()))
            .Where(s => s != null)
            .ToArray();

        // Get ObservablePropertyAttribute and check for ReportOldValue parameter
        var observablePropertyAttribute = fieldSymbol.GetAttributes()
            .FirstOrDefault(ad => ad.AttributeClass?.Name == "ObservablePropertyAttribute");

        var reportOldValue = false;
        if (observablePropertyAttribute?.ConstructorArguments.Length > 0)
        {
            reportOldValue = (bool)(observablePropertyAttribute.ConstructorArguments[0].Value ?? false);
        }
        
        var fieldName = fieldSymbol.Name;
        var propertyName = GetPropertyName(fieldName);
        var typeInfo = semanticModel.GetTypeInfo(fieldSyntax.Declaration.Type);
        if (typeInfo.Type == null)
            return null;
        
        var fieldType = typeInfo.Type.ToDisplayString();
        var className = classDeclaration.Identifier.Text;
        var isNullableEnabled = IsNullableEnabled(fieldSyntax, semanticModel);
        var nullableDirective = isNullableEnabled ? "#nullable enable\n\n" : "";
        var isFieldNullable = fieldSymbol.NullableAnnotation == NullableAnnotation.Annotated;
        
        if (isNullableEnabled && isFieldNullable && !fieldType.EndsWith("?"))
            fieldType += "?";
        
        var containingType = fieldSymbol.ContainingType;
        var containingTypeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        var namespaceName = containingTypeSymbol?.ContainingNamespace.ToDisplayString();
        
        var setPropertyCall = reportOldValue
            ? $"SetProperty(ref {fieldName}, value, out var oldValue)"
            : $"SetProperty(ref {fieldName}, value)";

        var notifyOtherProperties = alsoNotifyAttributes.Length > 0
            ? $@"if ({setPropertyCall})
                {{
{string.Join(Environment.NewLine, alsoNotifyAttributes.Select(o => $"                    OnPropertyChanged(nameof({o}));"))}
                }}"
            : $"{setPropertyCall};";
        
        var source = $@"{nullableDirective}namespace {namespaceName}
{{
    {containingType.DeclaredAccessibility.ToString().ToLower()} {(containingType.IsStatic ? "static " : (containingType.IsAbstract ? "abstract " : ""))}partial class {className}
    {{
        [global::System.Runtime.CompilerServices.CompilerGenerated]
#if UNITY_2023_2_OR_NEWER
        [global::Unity.Properties.CreateProperty]
#endif
        public {fieldType} {propertyName}
        {{
            get => {fieldName};
            set
            {{
                {notifyOtherProperties}
            }}
        }}
    }}
}}
";
        
        return source;
    }

    private static string GenerateExtensionMethods(INamedTypeSymbol containingType, List<(FieldDeclarationSyntax field, VariableDeclaratorSyntax variable, IFieldSymbol fieldSymbol)> fieldInfos)
    {
        var namespaceName = containingType.ContainingNamespace.ToDisplayString();
        var className = containingType.Name;
        var fullTypeName = $"{namespaceName}.{className}";

        var extensionMethods = new List<string>();

        foreach (var (field, variable, fieldSymbol) in fieldInfos)
        {
            var fieldName = fieldSymbol.Name;
            var propertyName = GetPropertyName(fieldName);
            var fieldType = fieldSymbol.Type.ToDisplayString();

            // Check if this field has ReportOldValue = true
            var observablePropertyAttribute = fieldSymbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == "ObservablePropertyAttribute");

            var reportOldValue = false;
            if (observablePropertyAttribute?.ConstructorArguments.Length > 0)
            {
                reportOldValue = (bool)(observablePropertyAttribute.ConstructorArguments[0].Value ?? false);
            }

            var methodName = $"On{propertyName}Changed";

            var extensionMethod = reportOldValue
                ? $@"        /// <summary>
        /// Subscribes to changes of the {propertyName} property.
        /// </summary>
        /// <param name=""model"">The model to subscribe to.</param>
        /// <param name=""handler"">The handler to call when the property changes.</param>
        /// <returns>An IDisposable to unsubscribe from the event.</returns>
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public static global::System.IDisposable {methodName}(this {fullTypeName} model, global::System.Action<{fieldType}, {fieldType}> handler)
        {{
            if (model == null) throw new global::System.ArgumentNullException(nameof(model));
            if (handler == null) throw new global::System.ArgumentNullException(nameof(handler));

            global::NZCore.MVVM.PropertyValueChangedHandler wrapper = (propertyName, oldValue, newValue) =>
            {{
                if (propertyName == nameof({fullTypeName}.{propertyName}) &&
                    oldValue is {fieldType} oldVal &&
                    newValue is {fieldType} newVal)
                {{
                    handler(oldVal, newVal);
                }}
            }};

            model.PropertyValueChanged += wrapper;
            return new global::NZCore.MVVM.UnsubscribeAction(() => model.PropertyValueChanged -= wrapper);
        }}"
                : $@"        /// <summary>
        /// Subscribes to changes of the {propertyName} property.
        /// </summary>
        /// <param name=""model"">The model to subscribe to.</param>
        /// <param name=""handler"">The handler to call when the property changes.</param>
        /// <returns>An IDisposable to unsubscribe from the event.</returns>
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        public static global::System.IDisposable {methodName}(this {fullTypeName} model, global::System.Action<{fieldType}> handler)
        {{
            if (model == null) throw new global::System.ArgumentNullException(nameof(model));
            if (handler == null) throw new global::System.ArgumentNullException(nameof(handler));

            global::System.ComponentModel.PropertyChangedEventHandler wrapper = (sender, e) =>
            {{
                if (e.PropertyName == nameof({fullTypeName}.{propertyName}))
                {{
                    handler(model.{propertyName});
                }}
            }};

            model.PropertyChanged += wrapper;
            return new global::NZCore.MVVM.UnsubscribeAction(() => model.PropertyChanged -= wrapper);
        }}";

            extensionMethods.Add(extensionMethod);
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
    public static partial class {className}Extensions
    {{
{string.Join(Environment.NewLine + Environment.NewLine, extensionMethods)}
    }}
}}
";

        return source;
    }

    private static bool IsNullableEnabled(SyntaxNode node, SemanticModel semanticModel)
    {
        var compilation = semanticModel.Compilation;
        var syntaxTree = node.SyntaxTree;
        return compilation.GetSemanticModel(syntaxTree).GetNullableContext(node.SpanStart).AnnotationsEnabled();
    }

    private static string GetPropertyName(string fieldName)
    {
        // Remove m_ prefix if present
        if (fieldName.StartsWith("m_"))
        {
            fieldName = fieldName.Substring(2);
        }
        
        if (fieldName.StartsWith("_"))
        {
            fieldName = fieldName.Substring(1);
        }

        // Capitalize first letter
        if (fieldName.Length > 0)
        {
            return char.ToUpper(fieldName[0]) + fieldName.Substring(1);
        }

        return fieldName;
    }
}
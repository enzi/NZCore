// <copyright project="NZCore" file="TypeHashHelper.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity;
using Unity.Entities;

namespace NZCore.Helper
{
    public static class TypeHashIgnoreSafety
    {
        const ulong kFNV1A64OffsetBasis = 14695981039346656037;

        public static ulong HashType(Type type, bool verbose = false)
        {
            if (verbose)
                Debug.Log($"Hashing {type.Name}");

            var hash = HashTypeName(type, verbose);
            // If we shouldn't walk the type's fields just return the type name hash.
            // UnityEngine objects have their own serialization mechanism so exclude hashing their internals
            if (type.IsArray || type.IsPointer || type.IsPrimitive || type.IsEnum || (TypeManager.UnityEngineObjectType?.IsAssignableFrom(type) == true))
            {
                return hash;
            }

            if (type.ContainsGenericParameters)
                throw new ArgumentException($"'{type}' contains open generic parameters. Generic types must have all generic parameters specified to closed types when calculating stable type hashes");

            // Only non-pod and non-unityengine types could possibly have a version attribute
            hash = TypeHash.CombineFNV1A64(hash, HashVersionAttribute(type, verbose));
            if (verbose)
                Debug.Log($"HashVersionAttribute: {hash}");

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            ulong fieldsLength = (ulong)fields.Length;
            ulong fieldIndex = 0;
            for (ulong i = 0; i < fieldsLength; i++)
            {
                var field = fields[i];
                // statics have no effect on data layout
                if (field.IsStatic)
                    continue;
                if (field.Name.Contains("Safety"))
                    continue;

                var fieldType = field.FieldType;
                ulong fieldTypeHash = 0;

                // Managed components can have circular definitions, so when looking at managed fields only hash
                // the name and field index, but do not cache the result -- we still want the hash for managed fields
                // to be calculated from more than just the name since we do handle managed component serialization
                // where field layout is important to identify via the hash alone
                if (fieldType.IsClass)
                {
                    fieldTypeHash = HashTypeName(fieldType, verbose);
                    if (verbose)
                        Debug.Log($"Step fieldType {fieldTypeHash}");
                }
                else
                {
                    fieldTypeHash = HashType(fieldType, verbose);
                    if (verbose)
                        Debug.Log($"Step fieldType {fieldTypeHash}");
                }

                fieldTypeHash = TypeHash.CombineFNV1A64(fieldTypeHash, fieldIndex);

                if (verbose)
                    Debug.Log($"Step fieldIndex {fieldTypeHash}");

                var offset = field.GetCustomAttribute<FieldOffsetAttribute>();
                if (offset != null)
                {
                    fieldTypeHash = TypeHash.CombineFNV1A64(fieldTypeHash, (ulong)offset.Value);
                    if (verbose)
                        Debug.Log($"Step offset {fieldTypeHash}");
                }

                hash = TypeHash.CombineFNV1A64(hash, fieldTypeHash);
                if (verbose)
                    Debug.Log($"Step{2 + fieldIndex}: {hash}");

                fieldIndex++;
            }

            // TODO: Enable this. Currently IL2CPP gives totally inconsistent results to Mono.
            /*
            if (type.StructLayoutAttribute != null && !type.StructLayoutAttribute.IsDefaultAttribute())
            {
                var explicitSize = type.StructLayoutAttribute.Size;
                if (explicitSize > 0)
                    hash = CombineFNV1A64(hash, (ulong)explicitSize);

                // Todo: Enable this. We cannot support Pack at the moment since a type's Packing will
                // change based on its field's explicit packing which will fail for Tiny mscorlib
                // as it's not in sync with dotnet
                // var packingSize = type.StructLayoutAttribute.Pack;
                // if (packingSize > 0)
                //     hash = CombineFNV1A64(hash, (ulong)packingSize);
            }
            */

            return hash;
        }

        private static ulong HashTypeName(Type type, bool verbose = false)
        {
            ulong hash = HashNamespace(type, verbose);
            hash = TypeHash.CombineFNV1A64(hash, TypeHash.FNV1A64(type.Name));
            hash = TypeHash.CombineFNV1A64(hash, TypeHash.FNV1A64(type.Assembly.GetName().Name));

            foreach (var ga in type.GenericTypeArguments)
            {
                hash = TypeHash.CombineFNV1A64(hash, HashTypeName(ga));
                if (verbose)
                    Debug.Log($"HashTypeName {ga.Name} - {hash}");
            }

            return hash;
        }

        private static ulong HashNamespace(Type type, bool verbose = false)
        {
            var hash = kFNV1A64OffsetBasis;

            // System.Reflection and Cecil don't report namespaces the same way so do an alternative:
            // Find the namespace of an un-nested parent type, then hash each of the nested children names
            if (type.IsNested)
            {
                hash = TypeHash.CombineFNV1A64(hash, HashNamespace(type.DeclaringType, verbose));
                if (verbose)
                    Debug.Log($"HashNamespace {type.DeclaringType.Name} - {hash}");
                hash = TypeHash.CombineFNV1A64(hash, TypeHash.FNV1A64(type.DeclaringType.Name));

                if (verbose)
                    Debug.Log($"HashNamespace {type.DeclaringType.Name} - {hash}");
            }
            else if (!string.IsNullOrEmpty(type.Namespace))
            {
                hash = TypeHash.CombineFNV1A64(hash, TypeHash.FNV1A64(type.Namespace));
                if (verbose)
                    Debug.Log($"HashNamespace {type.Namespace} - {hash}");
            }

            return hash;
        }

        private static ulong HashVersionAttribute(Type type, bool verbose = false)
        {
            int version = 0;

            var versionAttribute = type.GetCustomAttribute<TypeManager.TypeVersionAttribute>(true);
            if (versionAttribute != null)
            {
                version = versionAttribute.TypeVersion;
            }

            return TypeHash.FNV1A64(version);
        }
    }
}
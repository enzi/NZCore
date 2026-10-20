// <copyright project="NZCore" file="EntityQueryBuilderExtensions.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Collections;
using Unity.Entities;

namespace NZCore
{
    // Unity 7's EntitiesModule uses C# 11 ref-safety rules, so the builder returned by WithAll(ref list) is treated as capturing the list
    // and can't be chained into Build(ref state). Taking the list by value keeps the ref inside this call, which keeps chaining possible.
    public static class EntityQueryBuilderExtensions
    {
        public static EntityQueryBuilder WithAll(this EntityQueryBuilder builder, NativeList<ComponentType> componentTypes)
        {
            builder.WithAll(ref componentTypes);
            return builder;
        }

        public static EntityQueryBuilder WithAny(this EntityQueryBuilder builder, NativeList<ComponentType> componentTypes)
        {
            builder.WithAny(ref componentTypes);
            return builder;
        }

        public static EntityQueryBuilder WithNone(this EntityQueryBuilder builder, NativeList<ComponentType> componentTypes)
        {
            builder.WithNone(ref componentTypes);
            return builder;
        }
    }
}
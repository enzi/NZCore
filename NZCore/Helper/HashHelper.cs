// <copyright project="NZCore" file="HashHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities;

namespace NZCore
{
    public static class HashHelper
    {
        public static int GetEntityAndByteHash(Entity entity, byte type)
        {
            const int prime = 31;

            var result = prime + entity.GetHashCode();
            result = prime * result + type;
            return result;
        }

        public static int GetEntityAndIntegerHash(Entity entity, int type)
        {
            const int prime = 31;

            var result = prime + entity.GetHashCode();
            result = prime * result + type;
            return result;
        }

        public static int GetEntityAndSpellIdHash(Entity entity, int spellId)
        {
            const int prime = 31;

            var result = prime + entity.GetHashCode();
            result = prime * result + spellId;
            return result;
        }

        public static int GetSourceTargetHash(Entity source, Entity target)
        {
            const int prime = 31;

            bool side = source.Index < target.Index;

            var result = prime + (side ? source.Index : target.Index);
            result = prime * result + (side ? target.Index : source.Index);
            return result;
        }

        public static int GetHashFromInts(int a, int b)
        {
            unchecked
            {
                return (a.GetHashCode() * 397) ^ b.GetHashCode();
            }
        }
    }
}
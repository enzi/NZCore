// <copyright project="NZCore" file="HashHelper.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using Hash128 = UnityEngine.Hash128; 
using Unity.Entities;

namespace NZCore
{
    public static class HashHelper
    {
        private const int PrimeNumber = 397;
        
        public static int GetEntityAndByteHash(Entity entity, byte type)
        {
            var result = PrimeNumber + entity.GetHashCode();
            result = PrimeNumber * result + type;
            return result;
        }

        public static int GetEntityAndIntegerHash(Entity entity, int type)
        {
            var result = PrimeNumber + entity.GetHashCode();
            result = PrimeNumber * result + type;
            return result;
        }

        public static int GetEntityAndSpellIdHash(Entity entity, int spellId)
        {
            var result = PrimeNumber + entity.GetHashCode();
            result = PrimeNumber * result + spellId;
            return result;
        }

        public static int GetSourceTargetHash(Entity source, Entity target)
        {
            bool side = source.Index < target.Index;

            var result = PrimeNumber + (side ? source.Index : target.Index);
            result = PrimeNumber * result + (side ? target.Index : source.Index);
            return result;
        }

        public static int GetHashFromInts(int a, int b)
        {
            unchecked
            {
                return (a.GetHashCode() * PrimeNumber) ^ b.GetHashCode();
            }
        }

        public static Hash128 GenerateHash128()
        {
            return Hash128.Compute(Guid.NewGuid().ToByteArray());
        }
    }
}
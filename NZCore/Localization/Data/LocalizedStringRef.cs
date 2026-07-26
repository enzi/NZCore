// <copyright project="AeonWeaver.Data" file="LocalizedStringRef.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine.Localization;

namespace AeonWeaver.Data
{
    public struct LocalizedStringRef : IEquatable<LocalizedStringRef>
    {
        public Guid TableReference;
        public long EntryReference;

        public readonly LocalizedString AsLocalizedString() => this;

        public static implicit operator LocalizedString(LocalizedStringRef ls)
        {
            return new LocalizedString(ls.TableReference, ls.EntryReference);
        }

        public readonly string GetLocalizedString() => AsLocalizedString().GetLocalizedString();

        public bool Equals(LocalizedStringRef other)
        {
            return TableReference.Equals(other.TableReference) && EntryReference == other.EntryReference;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizedStringRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TableReference, EntryReference);
        }

        public bool IsValid()
        {
            return TableReference != Guid.Empty && EntryReference != 0;
        }
    }
}
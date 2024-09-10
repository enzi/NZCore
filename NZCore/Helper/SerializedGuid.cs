// <copyright project="NZCore" file="SerializedGuid.cs" version="0.1">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine;

namespace NZCore
{
    [Serializable]
    public struct SerializableGuid : ISerializationCallbackReceiver
    {
        private Guid internalGuid;

        [SerializeField] private string serializedGuid;

        public SerializableGuid(Guid internalGuid)
        {
            this.internalGuid = internalGuid;
            serializedGuid = null;
        }

        public override bool Equals(object obj)
        {
            return obj is SerializableGuid guid && internalGuid.Equals(guid.internalGuid);
        }

        public override int GetHashCode()
        {
            return -1324198676 + internalGuid.GetHashCode();
        }

        public void OnAfterDeserialize()
        {
            try
            {
                internalGuid = Guid.Parse(serializedGuid);
            }
            catch
            {
                internalGuid = Guid.Empty;
                Debug.LogWarning($"Attempted to parse invalid GUID string '{serializedGuid}'. GUID will set to System.Guid.Empty");
            }
        }

        public void OnBeforeSerialize()
        {
            serializedGuid = internalGuid.ToString();
        }

        public override string ToString() => internalGuid.ToString();

        public static bool operator ==(SerializableGuid a, SerializableGuid b) => a.internalGuid == b.internalGuid;
        public static bool operator !=(SerializableGuid a, SerializableGuid b) => a.internalGuid != b.internalGuid;
        public static implicit operator SerializableGuid(Guid guid) => new(guid);
        public static implicit operator Guid(SerializableGuid serializable) => serializable.internalGuid;
        public static implicit operator SerializableGuid(string serializedGuid) => new(Guid.Parse(serializedGuid));
        public static implicit operator string(SerializableGuid serializedGuid) => serializedGuid.ToString();
    }
}
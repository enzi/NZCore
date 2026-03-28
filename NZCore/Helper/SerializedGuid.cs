// <copyright project="NZCore" file="SerializedGuid.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace NZCore
{
    [Serializable]
    public struct SerializableGuid : ISerializationCallbackReceiver
    {
        private Guid _internalGuid;

        [FormerlySerializedAs("serializedGuid")] [SerializeField]
        private string _serializedGuid;

        public SerializableGuid(Guid internalGuid)
        {
            this._internalGuid = internalGuid;
            _serializedGuid = null;
        }

        public override bool Equals(object obj) => obj is SerializableGuid guid && _internalGuid.Equals(guid._internalGuid);

        public override int GetHashCode() => -1324198676 + _internalGuid.GetHashCode();

        public void OnAfterDeserialize()
        {
            try
            {
                _internalGuid = Guid.Parse(_serializedGuid);
            }
            catch
            {
                _internalGuid = Guid.Empty;
                Debug.LogWarning($"Attempted to parse invalid GUID string '{_serializedGuid}'. GUID will set to System.Guid.Empty");
            }
        }

        public void OnBeforeSerialize()
        {
            _serializedGuid = _internalGuid.ToString();
        }

        public override string ToString() => _internalGuid.ToString();

        public static bool operator ==(SerializableGuid a, SerializableGuid b) => a._internalGuid == b._internalGuid;
        public static bool operator !=(SerializableGuid a, SerializableGuid b) => a._internalGuid != b._internalGuid;
        public static implicit operator SerializableGuid(Guid guid) => new(guid);
        public static implicit operator Guid(SerializableGuid serializable) => serializable._internalGuid;
        public static implicit operator SerializableGuid(string serializedGuid) => new(Guid.Parse(serializedGuid));
        public static implicit operator string(SerializableGuid serializedGuid) => serializedGuid.ToString();
    }
}
// <copyright project="GraphToolkit.Runtime" file="Model.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NZCore.Graph
{
    [Serializable]
    public abstract class Model
    {
        [SerializeField, HideInInspector] 
        private Hash128 _guid;
        [SerializeField, HideInInspector] 
        private ModelVersion _version;

        public Hash128 Guid
        {
            get
            {
                if (!_guid.isValid)
                {
                    CreateNewGuid();
                }

                return _guid;
            }
        }

        public ModelVersion Version => _version;
        public virtual IEnumerable<Model> Dependencies => Enumerable.Empty<Model>();

        protected Model()
        {
            CreateNewGuid();
        }

        protected Model(Hash128 guid)
        {
            _guid = guid;
        }

        private void CreateNewGuid()
        {
            _guid = HashHelper.GenerateHash128();
        }
    }
}
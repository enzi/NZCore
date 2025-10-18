// <copyright project="GraphToolkit.Runtime" file="Model.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.MVVM
{
    [Serializable]
    public abstract class Model
    {
        [SerializeField] 
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

        public IServiceProvider Container;
        public virtual IEnumerable<Model> Dependencies => Enumerable.Empty<Model>();
        public virtual void Cleanup() { }
        public virtual void ClearCache() { }

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
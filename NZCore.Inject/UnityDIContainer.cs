// <copyright project="Assembly-CSharp" file="UnityDIContainer.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine;

namespace NZCore.Inject
{
    /// <summary>
    /// Unity-specific DI container implementation.
    /// </summary>
    public class UnityDIContainer : MonoBehaviour
    {
        private static UnityDIContainer _instance;
        private DIContainer _container;

        /// <summary>
        /// Gets the singleton instance of the UnityDIContainer.
        /// </summary>
        public static UnityDIContainer Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("DI Container");
                    _instance = go.AddComponent<UnityDIContainer>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Gets the DI container.
        /// </summary>
        public IDIContainer Container => _container;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            _container = new DIContainer();
            
            // Register the container itself
            _container.RegisterSingleton<IDIContainer>(_container);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _container.Dispose();
            }
        }
    }
}
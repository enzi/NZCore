// <copyright project="NZCore" file="CreateUIRootSingleton.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Threading.Tasks;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public interface IUICreator
    {
        Task<bool> CreateInterface(VisualElement root, VisualElement mainButtonsRoot);

        Task<bool> RegisterInMainButtons()
        {
            return Task.FromResult(true);
        }
    }

    //[ExecuteInEditMode]
    [RequireComponent(typeof(UIDocument))]
    public class CreateUIRootSingleton : MonoBehaviour
    {
        //private const string KEY_MAINBUTTONSCONTAINER = "MainButtonsContainer";

        private UIDocument _uiDocument;
        private VisualElement _rootVE;
        private VisualElement _dragContainer;
        private VisualElement _dragImage;
        private VisualElement _mainButtonsContainer;

        public void OnEnable()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            _uiDocument = GetComponent<UIDocument>();
            _rootVE = _uiDocument.rootVisualElement.Q<VisualElement>("root");
            _dragContainer = _uiDocument.rootVisualElement.Q<VisualElement>("dragContainer");
            _dragImage = _dragContainer.Q<VisualElement>("dragImage");
            _mainButtonsContainer = _rootVE.Q<VisualElement>("mainButtonsContainer");

            var ent = em.CreateEntity();
            em.AddComponentData(ent, new UIRootSingleton()
            {
                rootDocument = _uiDocument,
                root = _rootVE,
                dragElement = _dragContainer,
                dragImage = _dragImage,
                mainButtonsContainer = _mainButtonsContainer
            });

            CreateInterface();
        }

        private void OnDisable()
        {
            // todo: NullException
            //_uiDocument.rootVisualElement.Remove(_rootVE);
            //_uiDocument.rootVisualElement.Remove(_dragContainer);
        }

        private async void CreateInterface()
        {
            //Debug.Log("Creating interface");

            var creators = GetComponentsInChildren<IUICreator>();

            foreach (var uiCreator in creators)
            {
                await uiCreator.CreateInterface(_rootVE, _mainButtonsContainer);
                await uiCreator.RegisterInMainButtons();
            }
        }
    }
}
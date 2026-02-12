// <copyright project="NZCore.UI" file="WeakImage.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_6000
namespace NZCore.UI
{
    [UxmlElement]
    public partial class WeakImage : VisualElement
    {
        private WeakObjectReference<Sprite> spriteReference;
        private bool isLoading;
        private bool isLoaded;

        public WeakObjectReference<Sprite> SpriteReference
        {
            get => spriteReference;
            set
            {
                if (spriteReference.Equals(value))
                    return;

                Release();
                spriteReference = value;
                RequestLoad();
            }
        }

        public WeakImage()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (spriteReference.IsValidBurst() && !isLoaded)
            {
                RequestLoad();
            }
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Release();
        }

        private void RequestLoad()
        {
            if (!spriteReference.IsValidBurst() || isLoading)
                return;

            spriteReference.LoadAsync();
            isLoading = true;
            schedule.Execute(CheckLoadStatus).Every(16);
        }

        private void CheckLoadStatus()
        {
            if (!isLoading)
                return;

            if (spriteReference.LoadingStatus != ObjectLoadingStatus.Completed)
                return;

            isLoading = false;
            isLoaded = true;

            var sprite = spriteReference.Result;
            if (sprite != null)
            {
                style.backgroundImage = new StyleBackground(sprite);
            }
        }

        private void Release()
        {
            if (isLoaded && spriteReference.IsValidBurst())
            {
                RuntimeContentManager.ReleaseObjectAsync(spriteReference.GetInternalId());
            }

            isLoading = false;
            isLoaded = false;
            style.backgroundImage = StyleKeyword.None;
        }
    }
}
#endif
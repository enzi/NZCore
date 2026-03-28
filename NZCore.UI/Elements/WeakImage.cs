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
        private WeakObjectReference<Sprite> _spriteReference;
        private bool _isLoading;
        private bool _isLoaded;

        public WeakObjectReference<Sprite> SpriteReference
        {
            get => _spriteReference;
            set
            {
                if (_spriteReference.Equals(value))
                {
                    return;
                }

                Release();
                _spriteReference = value;
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
            if (_spriteReference.IsValidBurst() && !_isLoaded)
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
            if (!_spriteReference.IsValidBurst() || _isLoading)
            {
                return;
            }

            _spriteReference.LoadAsync();
            _isLoading = true;
            schedule.Execute(CheckLoadStatus).Every(16);
        }

        private void CheckLoadStatus()
        {
            if (!_isLoading)
            {
                return;
            }

            if (_spriteReference.LoadingStatus != ObjectLoadingStatus.Completed)
            {
                return;
            }

            _isLoading = false;
            _isLoaded = true;

            var sprite = _spriteReference.Result;
            if (sprite != null)
            {
                style.backgroundImage = new StyleBackground(sprite);
            }
        }

        private void Release()
        {
            if (_isLoaded && _spriteReference.IsValidBurst())
            {
                RuntimeContentManager.ReleaseObjectAsync(_spriteReference.GetInternalId());
            }

            _isLoading = false;
            _isLoaded = false;
            style.backgroundImage = StyleKeyword.None;
        }
    }
}
#endif
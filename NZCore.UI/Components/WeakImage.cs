// <copyright project="NZCore.UI" file="WeakBackgroundImage.cs">
// Copyright © 2026 Thomas Enzenebner. All rights reserved.
// </copyright>

#if UNITY_6000_7_OR_NEWER
using Unity.Entities.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI
{
    /// <summary>
    /// Shows a weakly referenced sprite as the background image of the element it is attached to.
    /// The sprite is loaded while the element is attached to a panel and released when it detaches.
    /// Set Sprite from code or bind it with ${component:WeakBackgroundImage}.Sprite.
    /// </summary>
    [VisualElementComponent]
    public partial struct WeakImage
    {
        public WeakObjectReference<Sprite> Sprite;

        private WeakObjectReference<Sprite> _loaded;

        [OnComponentAdded]
        private static void OnAdded(ref WeakImage self, VisualElement owner)
        {
            if (owner.panel != null)
            {
                Load(ref self, owner);
            }
        }

        [OnComponentChanged]
        private static void OnChanged(ref WeakImage self, VisualElement owner)
        {
            if (self.Sprite.Equals(self._loaded))
            {
                return;
            }

            Release(ref self, owner);

            if (owner.panel != null)
            {
                Load(ref self, owner);
            }
        }

        [OnComponentRemoved]
        private static void OnRemoved(ref WeakImage self, VisualElement owner) => Release(ref self, owner);

        [RegisterCallback(typeof(AttachToPanelEvent))]
        private static void OnAttach(ref WeakImage self, VisualElement owner, AttachToPanelEvent evt) => Load(ref self, owner);

        [RegisterCallback(typeof(DetachFromPanelEvent))]
        private static void OnDetach(ref WeakImage self, VisualElement owner, DetachFromPanelEvent evt) => Release(ref self, owner);

        [ReleaseComponentResources]
        private static void ReleaseResources(ref WeakImage self)
        {
            if (self._loaded.IsValidBurst())
            {
                self._loaded.Release();
                self._loaded = default;
            }
        }

        private static void Load(ref WeakImage self, VisualElement owner)
        {
            if (!self.Sprite.IsValidBurst())
            {
                return;
            }

            var sprite = self.Sprite;
            sprite.LoadAsync();
            self._loaded = sprite;

            // Until is evaluated after every tick, it stops the poll once the load is resolved
            owner.schedule.Execute(() => { }).Until(() => TryApply(owner, sprite));
        }

        private static bool TryApply(VisualElement owner, WeakObjectReference<Sprite> sprite)
        {
            // stale poll, the component was removed or moved on to another load
            if (!owner.HasComponent<WeakImage>() || !owner.GetComponent<WeakImage>()._loaded.Equals(sprite))
            {
                return true;
            }

            switch (sprite.LoadingStatus)
            {
                case ObjectLoadingStatus.Completed:
                {
                    owner.style.backgroundImage = new StyleBackground(sprite.Result);
                    return true;
                }
                case ObjectLoadingStatus.Error:
                {
                    return true;
                }
                default:
                {
                    return false;
                }
            }
        }

        private static void Release(ref WeakImage self, VisualElement owner)
        {
            if (!self._loaded.IsValidBurst())
            {
                return;
            }

            ReleaseResources(ref self);
            owner.style.backgroundImage = StyleKeyword.Null;
        }
    }
}
#endif
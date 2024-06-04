using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NZCore.UIToolkit
{
    public static class UIToolkitExtensions
    {
        private const string visibleClass = "visible";
        private const string hiddenClass = "hidden";

        public static bool IsVisible(this VisualElement visualElement)
        {
            return visualElement.resolvedStyle.display == DisplayStyle.Flex;
        }

        public static bool ToggleVisualElement(this VisualElement visualElement)
        {
            if (visualElement == null)
                return false;

            if (visualElement.resolvedStyle.display == DisplayStyle.Flex)
            {
                visualElement.RemoveFromClassList(visibleClass);
                visualElement.AddToClassList(hiddenClass);
                return false;
            }
            else
            {
                visualElement.RemoveFromClassList(hiddenClass);
                visualElement.AddToClassList(visibleClass);
                return true;
            }
        }


        public static void ShowVisualElement(this VisualElement visualElement, bool state)
        {
            if (visualElement == null)
                return;

            if (state)
            {
                if (visualElement.ClassListContains(visibleClass))
                    return;

                visualElement.RemoveFromClassList(hiddenClass);
                visualElement.AddToClassList(visibleClass);
            }
            else
            {
                if (visualElement.ClassListContains(hiddenClass))
                    return;

                visualElement.RemoveFromClassList(visibleClass);
                visualElement.AddToClassList(hiddenClass);
            }
        }

        public static VisualElement CloneTreeSingle(this VisualTreeAsset vta, VisualElement target, bool visible = true)
        {
            vta.CloneTree(target, out int firstElementIndex, out int _);
            var ve = target.ElementAt(firstElementIndex);
            ve.AddToClassList(visible ? visibleClass : hiddenClass);

            return ve;
        }

        public static List<T> GetActiveElements<T>(this VisualElement ve)
            where T : VisualElement
        {
            List<T> tmp = new List<T>();
            ve.Query<T>().ForEach((T element) =>
            {
                if (element.resolvedStyle.display == DisplayStyle.Flex)
                    tmp.Add(element);
            });

            return tmp;
        }

        public static void SetFlexDirection(this ListView listView, FlexDirection direction, Wrap wrapMode = Wrap.NoWrap)
        {
            var scrollContainer = listView.Q<VisualElement>("unity-content-container");
            scrollContainer.style.flexWrap = new StyleEnum<Wrap>(wrapMode);
            scrollContainer.style.flexDirection = new StyleEnum<FlexDirection>(direction);
        }

        public static void SetFlexDirection(this ScrollView scrollView, FlexDirection direction, Wrap wrapMode = Wrap.NoWrap)
        {
            var scrollContainer = scrollView.Q<VisualElement>("unity-content-container");
            scrollContainer.style.flexWrap = new StyleEnum<Wrap>(wrapMode);
            scrollContainer.style.flexDirection = new StyleEnum<FlexDirection>(direction);
        }
    }
}
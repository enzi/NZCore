// <copyright project="NZCore" file="UIToolkitExtensions.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        /// <summary>
        /// This method expects a VisualTreeAsset that has one root node
        /// and discards the TemplateContainer
        /// </summary>
        public static VisualElement CloneSingleTree(this VisualTreeAsset vta, VisualElement target, bool visible = true)
        {
            vta.CloneTree(target, out int firstElementIndex, out int _);
            var ve = target.ElementAt(firstElementIndex);
            ve.AddToClassList(visible ? visibleClass : hiddenClass);

            return ve;
        }

        /// <summary>
        /// This method expects a VisualTreeAsset that has one root node
        /// and discards the TemplateContainer
        /// </summary>
        public static VisualElement CloneSingleTree(this VisualTreeAsset vta, bool visible = true)
        {
            var tmp = vta.Instantiate();

            if (tmp.childCount != 1)
            {
                Debug.LogError($"{vta.name} has more than 1 root node!");
                return null;
            }

            var ve = tmp.ElementAt(0);
            ve.AddToClassList(visible ? visibleClass : hiddenClass);

            var vea = vta.visualElementAssets[0];
            vea.AssignClassListFromAssetToElement(ve);
            vea.AssignStyleSheetFromAssetToElement(ve);
            return ve;
        }

        private static void AssignClassListFromAssetToElement(
            this VisualElementAsset asset,
            VisualElement element)
        {
            if (asset.classes == null)
            {
                return;
            }

            foreach (var className in asset.classes)
            {
                element.AddToClassList(className);
            }
        }

        private static void AssignStyleSheetFromAssetToElement(
            this VisualElementAsset asset,
            VisualElement element)
        {
            if (asset.hasStylesheetPaths)
            {
                foreach (var sheetPath in asset.stylesheetPaths)
                {
                    element.AddStyleSheetPath(sheetPath);
                }
            }

            if (!asset.hasStylesheets)
            {
                return;
            }

            foreach (var styleSheet in asset.stylesheets)
            {
                if (styleSheet != null)
                {
                    element.styleSheets.Add(styleSheet);
                }
            }
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
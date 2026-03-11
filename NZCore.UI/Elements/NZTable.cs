// <copyright project="NZCore.UI" file="NZTable.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI.Elements
{
    public class NZTable : VisualElement
    {
        public bool OddState;

        private NZCellSettings[] headerSettings;

        public Color HeaderColor = new(0.29f, 0.29f, 0.29f);
        public Color RowColor2 = new(0.18f, 0.18f, 0.18f);
        public Color RowColor1 = new(0.12f, 0.12f, 0.12f);

        private List<NZRow> rows = new();

        public NZTable(bool oddState = false)
        {
            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Column);
            OddState = oddState;
        }

        public void SetColumnSettings(params NZCellSettings[] settings)
        {
            headerSettings = settings;
        }

        public VisualElement AddHeader()
        {
            var row = CreateRow(HeaderColor);

            for (var i = 0; i < headerSettings.Length; i++)
            {
                var settings = headerSettings[i];
                var cell = new NZRowCell(settings, 0);
                cell.Add(new Label { text = settings.HeaderName });
                row.Add(cell);
            }

            return row;
        }

        public VisualElement AddRow(string[] values, int depth = 0)
        {
            var row = CreateRow(GetOddStateColor());
            rows.Add(row);

            for (var i = 0; i < values.Length; i++)
            {
                var cell = new NZRowCell(headerSettings[i], depth);
                cell.Add(new Label { text = values[i] });
                row.Add(cell);
            }

            return row;
        }

        public VisualElement AddRow(VisualElement[] values, int depth = 0)
        {
            var row = CreateRow(GetOddStateColor());
            rows.Add(row);

            for (var i = 0; i < values.Length; i++)
            {
                var element = values[i];
                if (element == null)
                {
                    Debug.LogError($"AddRow - Element at position {i} is null!");
                    continue;
                }

                var cell = new NZRowCell(headerSettings[i], depth);
                cell.Add(element);
                row.Add(cell);
            }

            return row;
        }

        public NZRow AddSpanRow(VisualElement element)
        {
            var row = CreateRow(GetOddStateColor());
            rows.Add(row);

            var cell = new NZRowCell(new NZCellSettings { Percent = 100 }, 0);
            cell.Add(element);
            row.Add(cell);

            return row;
        }

        public NZRow CreateRow(Color backgroundColor)
        {
            var row = new NZRow(backgroundColor);
            OddState = !OddState;

            Add(row);
            return row;
        }

        private Color GetOddStateColor() => OddState ? RowColor2 : RowColor1;

        public void ClearTable()
        {
            foreach (var row in rows)
            {
                hierarchy.Remove(row);
            }

            rows.Clear();
        }
    }

    public class NZRow : VisualElement
    {
        public NZRow(Color backgroundColor)
        {
            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
            style.justifyContent = new StyleEnum<Justify>(Justify.FlexStart);
            style.backgroundColor = backgroundColor;

            style.minHeight = 25;
        }
    }

    public class NZRowCell : VisualElement
    {
        public NZRowCell(NZCellSettings settings, int depth)
        {
            style.width = settings.Percent < 0 ? new StyleLength(StyleKeyword.Auto) : Length.Percent(settings.Percent);
            style.justifyContent = new StyleEnum<Justify>(Justify.Center);

            if (settings.DepthDependent)
            {
                style.paddingLeft = depth * settings.DepthPadding;
            }

            if (settings.AlignItems != Align.Auto)
            {
                style.alignItems = settings.AlignItems;
            }
        }
    }

    public class NZCellSettings
    {
        public string HeaderName;
        public float Percent;
        public float DepthPadding;
        public bool DepthDependent;
        public Align AlignItems;
    }
}
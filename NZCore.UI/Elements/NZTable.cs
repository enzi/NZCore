// <copyright project="NZCore" file="NZTable.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.UI.Elements
{
    public class NZColumnSettings
    {
        public string HeaderName;
        public float Percent;
        public bool DepthDependent;
        public float DepthPadding;
    }
    
    public class NZTable : VisualElement
    {
        private readonly List<NZRow> rows = new();

        public bool OddState;

        private NZColumnSettings[] headerSettings;

        public NZTable(bool oddState = false)
        {
            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Column);
            OddState = oddState;
        }
        
        public void SetColumnSettings(params NZColumnSettings[] settings)
        {
            headerSettings = settings;
        }

        public void AddHeaderFromSettings()
        {
            NZRow row = new NZRow(OddState);
            OddState = !OddState;
            
            for (var i = 0; i < headerSettings.Length; i++)
            {
                var settings = headerSettings[i];
                var cell = new NZRowCell(settings, 0);
                cell.Add(new Label() { text = settings.HeaderName});
                row.Add(cell);
            }
            
            rows.Add(row);
            Add(row);
        }

        public void AddRow(string[] values, int depth = 0)
        {
            NZRow row = new NZRow(OddState);
            OddState = !OddState;
            
            for (int i = 0; i < values.Length; i++)
            {
                var cell = new NZRowCell(headerSettings[i], depth);
                cell.Add(new Label() { text = values[i]});
                row.Add(cell);
            }
            
            rows.Add(row);
            Add(row);
        }

        public void AddRow(VisualElement[] values, int depth = 0)
        {
            NZRow row = new NZRow(OddState);
            OddState = !OddState;
            
            for (int i = 0; i < values.Length; i++)
            {
                var element = values[i];
                if (element == null)
                {
                    Debug.LogError($"AddRow - Element at position {i} is null!");
                    return;
                }

                var cell = new NZRowCell(headerSettings[i], depth);
                cell.Add(element);
                row.Add(cell);
            }
            
            rows.Add(row);
            Add(row);
        }
        
        public void AddSpanRow(params VisualElement[] values)
        {
            NZRow row = new NZRow(OddState);
            OddState = !OddState;
            
            // row.style.borderBottomColor = new StyleColor(Color.black);
            // row.style.borderTopColor = new StyleColor(Color.black);
            // row.style.borderLeftColor = new StyleColor(Color.black);
            // row.style.borderRightColor = new StyleColor(Color.black);
            //
            // row.style.borderBottomWidth = new StyleFloat(3);
            // row.style.borderTopWidth = new StyleFloat(3);
            // row.style.borderLeftWidth = new StyleFloat(3);
            // row.style.borderRightWidth = new StyleFloat(3);
            
            
            
            for (int i = 0; i < values.Length; i++)
            {
                var element = values[i];
                if (element == null)
                {
                    Debug.LogError($"AddRow - Element at position {i} is null!");
                    return;
                }

                var cell = new NZRowCell(new NZColumnSettings() { Percent = 100 }, 0);
                cell.Add(element);
                row.Add(cell);
            }
            
            rows.Add(row);
            Add(row);
        }
    }
    
    internal class NZRow : VisualElement
    {
        public List<NZRowCell> RowCells;

        public NZRow(bool odd)
        {
            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
            style.justifyContent = new StyleEnum<Justify>(Justify.FlexStart);

            style.minHeight = 25;

            var c1 = new Color(0.18f, 0.18f, 0.18f);
            var c2 = new Color(0.12f, 0.12f, 0.12f);
            
            style.backgroundColor = odd ? c2 : c1;
        }
    }

    internal class NZRowCell : VisualElement
    {
        public NZRowCell(NZColumnSettings settings, int depth)
        {
            style.width = settings.Percent < 0 ? new StyleLength(StyleKeyword.Auto) : Length.Percent(settings.Percent);
            style.justifyContent = new StyleEnum<Justify>(Justify.Center);

            if (settings.DepthDependent)
            {
                style.paddingLeft = depth * settings.DepthPadding;
            }
        }
    }
}
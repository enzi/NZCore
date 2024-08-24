// <copyright project="NZCore" file="NZTable.cs" version="0.1">
// Copyright © 2024 EnziSoft. All rights reserved.
// </copyright>

using System.Collections.Generic;
using UnityEngine.UIElements;

namespace NZCore.UI.Elements
{
    public class NZColumnSettings
    {
        public string HeaderName;
        public float Percent;
    }
    
    public class NZTable : VisualElement
    {
        private readonly List<NZTableColumn> columns = new();

        public NZTable()
        {
            style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        }

        public void SetColumnHeaders(params NZColumnSettings[] headerSettings)
        {
            columns.Clear();
            Clear();

            for (var i = 0; i < headerSettings.Length; i++)
            {
                var headerSetting = headerSettings[i];
                NZTableColumn c = new NZTableColumn(headerSetting);

                c.ColumnId = i;
                columns.Add(c);
                Add(c);
            }
        }

        public void AddRow(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                var cell = new NZCell();
                var lbl = new Label() { text = values[i] };
                cell.AddContent(lbl);
                
                columns[i].AddRow(cell);
            }
        }

        public void AddRow(params VisualElement[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                var cell = new NZCell();
                cell.AddContent(values[i]);
                
                columns[i].AddRow(cell);
            }
        }
    }
    
    internal class NZCellHeader : NZCell
    {
        public NZCellHeader(string headerName)
        {
            var lbl = new Label() { text = headerName };
            
            lbl.pickingMode = PickingMode.Ignore;

            style.width = Length.Percent(100);
            style.maxHeight = 50;

            AddContent(lbl);
        }
    }

    internal class NZCell : VisualElement
    {
        private VisualElement Content;
        
        public NZCell()
        {
            style.width = Length.Percent(100);
        }

        public void AddContent(VisualElement content)
        {
            Content = content;
            
            Add(Content);
        }
    }

    internal class NZTableColumn : VisualElement
    {
        private readonly List<NZCell> rows = new();
        public int ColumnId;

        public NZTableColumn(NZColumnSettings settings)
        {
            style.width = Length.Percent(settings.Percent);
            
            NZCell header = new NZCellHeader(settings.HeaderName);
            
            rows.Add(header);
            Add(header);
        }

        public void AddRow(NZCell row)
        {
            rows.Add(row);
            Add(row);
            
        }
        
        public void AddRow(string text)
        {
            var lbl = new Label() { text = text };

            var cell = new NZCell();
            cell.AddContent(lbl);
            
            rows.Add(cell);
            Add(cell);
            
        }
    }
}
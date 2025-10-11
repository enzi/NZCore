// <copyright project="NZCore.MVVM" file="ObservableObject.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace NZCore.MVVM
{
    public class ObservableObjectAttribute : Attribute { }
    public class ObservablePropertyAttribute : Attribute
    {
        public bool ReportOldValue { get; }

        public ObservablePropertyAttribute(bool reportOldValue = false)
        {
            ReportOldValue = reportOldValue;
        }
    }
    
    public class AlsoNotifyAttribute : Attribute
    {
        public AlsoNotifyAttribute(string propertyName) { }
    }
}
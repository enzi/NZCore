// <copyright project="ObservableObjectTest" file="Attributes.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;

namespace ObservableObjectTest;

public class ObservableObjectAttribute : Attribute { }
public class ObservablePropertyAttribute : Attribute
{
    public bool ReportOldValue { get; }

    public ObservablePropertyAttribute(bool reportOldValue = false)
    {
        ReportOldValue = reportOldValue;
    }
}

public class AlsoNotifyChangeForAttribute : Attribute
{
    public AlsoNotifyChangeForAttribute(string propertyName) { }
}
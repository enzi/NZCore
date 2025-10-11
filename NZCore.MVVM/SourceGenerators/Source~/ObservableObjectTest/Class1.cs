using System;
using Nested.Test.Namespace;

namespace ObservableObjectTest;

[ObservableObject]
public partial class MainViewModel
{
    [ObservableProperty]
    int test1;

    [ObservableProperty(true)]  // Should generate oldValue + newValue
    int test1WithOldValue;

    [ObservableProperty(false)] // Should generate newValue only
    int test1WithoutOldValue;

    [ObservableProperty]
    [AlsoNotifyChangeFor(nameof(TestMessage))]
    Vector2 test2;
    
    public string TestMessage => $"Test passed: {test2}";

    public void SetProperty<T>(ref T obj, T prev)
    {
        
    }
}

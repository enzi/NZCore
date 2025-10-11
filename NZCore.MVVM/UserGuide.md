# NZCore.MVVM & NZCore.Inject User Guide

A complete guide to building Unity applications with dependency injection and MVVM architecture.

## Table of Contents
- [Quick Start](#quick-start)
- [Dependency Injection (NZCore.Inject)](#dependency-injection-nzcoreinject)
- [MVVM Framework (NZCore.MVVM)](#mvvm-framework-nzcoremvvm)
- [Integration Patterns](#integration-patterns)
- [Best Practices](#best-practices)

## Quick Start

### 1. Setup DI Container
```csharp
using NZCore.Inject;
using NZCore.MVVM.Factory;

// Create container
var container = new DIContainer();

// Register services
container.Register<IUserService, UserService>(ServiceLifetime.Singleton);
container.Register<IViewFactory, ViewFactory>(ServiceLifetime.Singleton);

// Register ViewModels
container.Register<MainViewModel, MainViewModel>(ServiceLifetime.Transient);
```

### 2. Create Your First ViewModel
```csharp
using NZCore.MVVM;
using NZCore.MVVM.Core;
using UnityEngine.UIElements;

public class UserViewModel : ViewModel
{
    private string _name = "John";
    private int _age = 25;

    public string Name 
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value);
    }

    protected override void OnCreateView()
    {
        var nameField = new TextField("Name:") { bindingPath = nameof(Name) };
        var ageField = new IntegerField("Age:") { bindingPath = nameof(Age) };
        
        Add(nameField);
        Add(ageField);
        
        base.OnCreateView(); // Sets up data binding
    }
}
```

### 3. Create and Display
```csharp
// Create ViewModel using factory
var factory = container.Resolve<IViewFactory>();
var userVM = factory.CreateViewModel<UserViewModel>(container);

// Add to UI hierarchy
rootVisualElement.Add(userVM);
```

## Dependency Injection (NZCore.Inject)

### Service Lifetimes
- **Transient**: New instance every time
- **Scoped**: One instance per scope
- **Singleton**: One instance globally

### Registration Patterns

#### Interface to Implementation
```csharp
container.Register<IDataService, DatabaseService>(ServiceLifetime.Singleton);
container.Register<ILogger, FileLogger>(ServiceLifetime.Scoped);
```

#### Factory Registration
```csharp
container.Register<IConfiguration>(ServiceLifetime.Singleton, c => 
    new AppConfiguration(c.Resolve<IConfigLoader>()));
```

#### Instance Registration
```csharp
var config = new AppConfig { ApiUrl = "https://api.example.com" };
container.RegisterSingleton<IAppConfig>(config);
```

### Service Resolution
```csharp
// Generic resolution
var userService = container.Resolve<IUserService>();

// Type-based resolution
var service = container.Resolve(typeof(IUserService));
```

### Scoped Containers
```csharp
// Create child scope
using var scope = container.CreateScope();

// Register scoped services
scope.Register<ICurrentUser, CurrentUser>(ServiceLifetime.Scoped);

// Resolve from scope
var currentUser = scope.Resolve<ICurrentUser>();
```

## MVVM Framework (NZCore.MVVM)

### View Hierarchy
```
ViewModel (base for all views)
├── RootView (manages child views with scoped DI)
└── ChildView (managed by RootView)
```

### Core Components

#### Observable Properties
```csharp
// Using SetProperty (recommended approach)
private string _title;
public string Title
{
    get => _title;
    set => SetProperty(ref _title, value);
}

private string _description;
public string Description
{
    get => _description;
    set => SetProperty(ref _description, value);
}
```

#### Observable Models
```csharp
using NZCore.MVVM.Core;

public class UserModel : ObservableModel
{
    private string _firstName;
    private string _lastName;

    public string FirstName
    {
        get => _firstName;
        set => SetProperty(ref _firstName, value);
    }

    public string LastName
    {
        get => _lastName;
        set => SetProperty(ref _lastName, value);
    }

    // Computed property with dependency tracking
    public string FullName => $"{FirstName} {LastName}";

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        base.OnPropertyChanged(propertyName);
        
        if (propertyName == nameof(FirstName) || propertyName == nameof(LastName))
        {
            OnPropertyChanged(nameof(FullName));
        }
    }
}
```

#### Observable Collections
```csharp
using NZCore.MVVM.Collections;

public class TodoListViewModel : ViewModel
{
    public ObservableCollection<TodoItem> Items { get; } = new();

    private void AddItem()
    {
        Items.Add(new TodoItem { Text = "New Task" });
        // UI updates automatically
    }
}
```

### RootView Pattern
```csharp
public class MainRootView : RootView
{
    private readonly IUserService _userService;

    public MainRootView(IUserService userService)
    {
        _userService = userService;
    }

    protected override void OnCreateView()
    {
        var header = new Label("Main View");
        Add(header);

        base.OnCreateView();
        LoadChildViews();
    }

    private void LoadChildViews()
    {
        var users = _userService.GetUsers();
        
        foreach (var user in users)
        {
            var userVM = GetService<IViewFactory>()
                .CreateViewModel<UserChildView>(ChildContainer);
            userVM.Model = user;
            
            AddChildView(userVM);
        }
    }
}
```

### ChildView Pattern
```csharp
public class UserChildView : ChildView
{
    protected override void OnCreateView()
    {
        // Access model data
        if (Model is UserModel user)
        {
            var nameLabel = new Label { bindingPath = nameof(user.FullName) };
            Add(nameLabel);
        }

        base.OnCreateView();
    }
}
```

### ViewModelManager Usage
```csharp
using NZCore.MVVM.Core;

// Setup
var viewManager = new ViewModelManager(container.Resolve<IViewFactory>());
var rootView = container.Resolve<MainRootView>();
viewManager.RegisterRootView(rootView);

// Get or create child view for model
var user = new UserModel();
var childView = viewManager.GetView<UserChildView>(user, rootView);

// Create ViewModel from model (with type inference)
var userVM = viewManager.CreateViewModel(user, container);
// Creates UserViewModel if UserModel -> UserViewModel naming pattern exists
```

## Integration Patterns

### Unity UI Toolkit Data Binding
```csharp
protected override void OnCreateView()
{
    // UXML template with binding paths
    var visualTree = Resources.Load<VisualTreeAsset>("UserView");
    visualTree.CloneTree(this);

    // Programmatic binding
    var nameField = this.Q<TextField>("name-field");
    nameField.bindingPath = nameof(Name);

    base.OnCreateView(); // Establishes dataSource = this
}
```

### UXML Template Example
```xml
<ui:UXML>
    <ui:TextField name="name-field" binding-path="Name" label="Name:" />
    <ui:IntegerField name="age-field" binding-path="Age" label="Age:" />
    <ui:Label text="{binding FullName}" name="display-name" />
</ui:UXML>
```

### Command Pattern
```csharp
using NZCore.MVVM.Commands;

public class UserViewModel : ViewModel
{
    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }

    public UserViewModel()
    {
        SaveCommand = new RelayCommand(Save, CanSave);
        LoadCommand = new AsyncCommand(LoadAsync);
    }

    private void Save() => GetService<IUserService>().Save(Model);
    private bool CanSave() => !string.IsNullOrEmpty(Name);
    private async Task LoadAsync() => Model = await GetService<IUserService>().LoadAsync();
}
```

### Service Injection in ViewModels
```csharp
public class DataViewModel : ViewModel
{
    private readonly IDataService _dataService;
    private readonly ILogger _logger;

    // Constructor injection
    public DataViewModel(IDataService dataService, ILogger logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    protected override void OnInitialize()
    {
        // Service resolution
        var config = GetService<IConfiguration>();
        
        // Load data
        LoadData();
    }
}
```

## Best Practices

### 1. Service Registration
```csharp
// Register in order: Infrastructure -> Services -> ViewModels
public static class ServiceRegistration
{
    public static void RegisterServices(IDIContainer container)
    {
        // Infrastructure
        container.Register<ILogger, ConsoleLogger>(ServiceLifetime.Singleton);
        container.Register<IViewFactory, ViewFactory>(ServiceLifetime.Singleton);
        
        // Business Services
        container.Register<IUserService, UserService>(ServiceLifetime.Scoped);
        container.Register<IDataService, ApiDataService>(ServiceLifetime.Singleton);
        
        // ViewModels
        container.Register<MainRootView, MainRootView>(ServiceLifetime.Transient);
        container.Register<UserViewModel, UserViewModel>(ServiceLifetime.Transient);
    }
}
```

### 2. ViewModel Lifecycle
```csharp
public class MyViewModel : ViewModel
{
    protected override void OnInitialize()
    {
        // Setup subscriptions, load initial data
        var eventBus = GetService<IEventBus>();
        eventBus.Subscribe<UserUpdated>(OnUserUpdated);
    }

    protected override void OnCreateView()
    {
        // Create UI elements
        CreateUserInterface();
        base.OnCreateView(); // Always call base
    }

    protected override void OnDispose()
    {
        // Cleanup subscriptions
        GetService<IEventBus>().Unsubscribe<UserUpdated>(OnUserUpdated);
        base.OnDispose();
    }
}
```

### 3. Memory Management
```csharp
// Use 'using' with scoped containers
using (var scope = container.CreateScope())
{
    var viewModel = scope.Resolve<MyViewModel>();
    // Use viewModel
} // Automatically disposed

// Dispose ViewModels when removing from UI
private void RemoveView(ViewModel viewModel)
{
    parent.Remove(viewModel);
    viewModel.Dispose(); // Important!
}
```

### 4. Error Handling
```csharp
public class RobustViewModel : ViewModel
{
    protected override void OnInitialize()
    {
        try
        {
            var service = GetService<IRequiredService>();
            InitializeWithService(service);
        }
        catch (Exception ex)
        {
            GetService<ILogger>().LogError($"Failed to initialize {GetType().Name}", ex);
            ShowErrorState();
        }
    }
}
```

### 5. Testing
```csharp
[Test]
public void ViewModel_PropertyChange_NotifiesCorrectly()
{
    // Arrange
    var container = new DIContainer();
    container.Register<IUserService, MockUserService>(ServiceLifetime.Singleton);
    
    var viewModel = new UserViewModel();
    viewModel.Initialize(container);
    
    bool propertyChanged = false;
    viewModel.PropertyChanged += (s, e) => 
        propertyChanged = e.PropertyName == nameof(UserViewModel.Name);
    
    // Act
    viewModel.Name = "Test";
    
    // Assert
    Assert.IsTrue(propertyChanged);
    Assert.AreEqual("Test", viewModel.Name);
}
```

### 6. Performance Tips
- Use `SetProperty<T>()` for property change notifications
- Implement `INotifyCollectionChanged` for dynamic lists
- Create ViewModels on-demand, not all at startup
- Dispose unused ViewModels promptly
- Use scoped containers for temporary view hierarchies

This guide covers the essential patterns for building robust Unity applications with NZCore.MVVM and NZCore.Inject. The frameworks handle the complex lifecycle management while providing clean, testable architecture.
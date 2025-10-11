// <copyright project="NZCore.MVVM" file="DICommand.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;
using IServiceProvider = NZCore.Inject.IServiceProvider;

namespace NZCore.MVVM
{
    /// <summary>
    /// A command that supports dependency injection for its execution logic.
    /// </summary>
    public class DICommand : ICommand
    {
        private readonly IServiceProvider _container;
        private readonly Func<IServiceProvider, object, bool> _canExecute;
        private readonly Action<IServiceProvider, object> _execute;

        /// <summary>
        /// Initializes a new instance of the DICommand class.
        /// </summary>
        /// <param name="container">The Service Provider for resolving dependencies.</param>
        /// <param name="execute">The execution logic that receives the container and parameter.</param>
        /// <param name="canExecute">The execution status logic that receives the container and parameter.</param>
        public DICommand(IServiceProvider container, Action<IServiceProvider, object> execute, 
            Func<IServiceProvider, object, bool> canExecute = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Initializes a new instance of the DICommand class with no parameter.
        /// </summary>
        /// <param name="container">The DI container for resolving dependencies.</param>
        /// <param name="execute">The execution logic that receives the container.</param>
        /// <param name="canExecute">The execution status logic that receives the container.</param>
        public DICommand(IServiceProvider container, Action<IServiceProvider> execute, 
            Func<IServiceProvider, bool> canExecute = null)
            : this(container, 
                execute != null ? (c, _) => execute(c) : null,
                canExecute != null ? (c, _) => canExecute(c) : null)
        {
        }

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns>True if this command can be executed; otherwise, false.</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke(_container, parameter) ?? true;
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        public void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                _execute(_container, parameter);
            }
        }

        /// <summary>
        /// Raises the CanExecuteChanged event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// An asynchronous command that supports dependency injection for its execution logic.
    /// </summary>
    public class AsyncDICommand : ICommand
    {
        private readonly IServiceProvider _container;
        private readonly Func<IServiceProvider, object, Task> _execute;
        private readonly Func<IServiceProvider, object, bool> _canExecute;
        private bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the AsyncDICommand class.
        /// </summary>
        /// <param name="container">The DI container for resolving dependencies.</param>
        /// <param name="execute">The asynchronous execution logic that receives the container and parameter.</param>
        /// <param name="canExecute">The execution status logic that receives the container and parameter.</param>
        public AsyncDICommand(IServiceProvider container, Func<IServiceProvider, object, Task> execute, 
            Func<IServiceProvider, object, bool> canExecute = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Initializes a new instance of the AsyncDICommand class with no parameter.
        /// </summary>
        /// <param name="container">The DI container for resolving dependencies.</param>
        /// <param name="execute">The asynchronous execution logic that receives the container.</param>
        /// <param name="canExecute">The execution status logic that receives the container.</param>
        public AsyncDICommand(IServiceProvider container, Func<IServiceProvider, Task> execute, 
            Func<IServiceProvider, bool> canExecute = null)
            : this(container, 
                execute != null ? (c, _) => execute(c) : null,
                canExecute != null ? (c, _) => canExecute(c) : null)
        {
        }

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Gets a value indicating whether the command is currently executing.
        /// </summary>
        public bool IsExecuting => _isExecuting;

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns>True if this command can be executed; otherwise, false.</returns>
        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(_container, parameter) ?? true);
        }

        /// <summary>
        /// Executes the command asynchronously.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        public void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    RaiseCanExecuteChanged();

                    _execute(_container, parameter);
                }
                catch (Exception ex)
                {
                    OnExecutionFailed(ex);
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Executes the command asynchronously and returns the task.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteAsync(object parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    RaiseCanExecuteChanged();

                    await _execute(_container, parameter);
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Raises the CanExecuteChanged event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Called when command execution fails with an exception.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        protected virtual void OnExecutionFailed(Exception exception)
        {
            // Override in derived classes to provide custom error handling
        }
    }
}
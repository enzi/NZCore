// <copyright project="NZCore.MVVM" file="AsyncCommand.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;

namespace NZCore.MVVM
{
    /// <summary>
    /// A command that supports asynchronous execution.
    /// </summary>
    public class AsyncCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the AsyncCommand class.
        /// </summary>
        /// <param name="execute">The asynchronous execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public AsyncCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Initializes a new instance of the AsyncCommand class with no parameter.
        /// </summary>
        /// <param name="execute">The asynchronous execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public AsyncCommand(Func<Task> execute, Func<bool> canExecute = null)
            : this(execute != null ? _ => execute() : null, canExecute != null ? _ => canExecute() : null)
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
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
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

                    _execute(parameter);
                }
                catch (Exception ex)
                {
                    // Handle exceptions - you might want to add logging or error handling here
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

                    await _execute(parameter);
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

    /// <summary>
    /// A generic asynchronous command that supports strongly-typed parameters.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    public class AsyncCommand<T> : ICommand
    {
        private readonly Func<T, Task> _execute;
        private readonly Func<T, bool> _canExecute;
        private bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the AsyncCommand class.
        /// </summary>
        /// <param name="execute">The asynchronous execution logic.</param>
        /// <param name="canExecute">The execution status logic.</param>
        public AsyncCommand(Func<T, Task> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
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
            if (_isExecuting)
                return false;

            if (parameter is T typedParameter)
            {
                return _canExecute?.Invoke(typedParameter) ?? true;
            }

            // If parameter is null and T is a reference type or nullable, allow it
            if (parameter == null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null))
            {
                return _canExecute?.Invoke(default(T)) ?? true;
            }

            return false;
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

                    if (parameter is T typedParameter)
                    {
                        _execute(typedParameter);
                    }
                    else if (parameter == null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null))
                    {
                        _execute(default(T));
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions - you might want to add logging or error handling here
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

                    if (parameter is T typedParameter)
                    {
                        await _execute(typedParameter);
                    }
                    else if (parameter == null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null))
                    {
                        await _execute(default(T));
                    }
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
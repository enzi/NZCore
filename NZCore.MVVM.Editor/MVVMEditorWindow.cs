// <copyright project="Assembly-CSharp" file="MVVMEditorWindow.cs">
// Copyright © 2025 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEditor;

namespace NZCore.MVVM.Editor
{
    public abstract class MVVMEditorApplication : EditorWindow
    {
        protected MVVMApplication app;
        protected IViewFactory viewFactory;

        private void CreateGUI()
        {
            app = new MVVMApplication();
            viewFactory = app.GetService<IViewFactory>();
            
            CreateView();
        }

        protected abstract void CreateView();
    }
}
using System.Collections.Generic;
using System.Linq;
using NZCore.AssetManagement;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NZCore.Editor
{
    [CustomEditor(typeof(ScriptableObjectWithAutoID), true)]
    [CanEditMultipleObjects]
    public class AutoIDEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            serializedObject.FillDefaultInspector(root, true);
            
            Button btn = new Button(Click_CodeGen)
            {
                text = "Codegen Enum"
            };

            root.Add(btn);

            return root;
        }

        private void Click_CodeGen()
        {
            RunDidChangeOnAssetType((ScriptableObjectWithAutoID)target);
        }

        private static void RunDidChangeOnAssetType(ScriptableObjectWithAutoID target)
        {
            var targetType = target.GetType();
            Debug.Log($"Generating code for {targetType.Name} ...");

            var assetPaths = AssetDatabase.FindAssets($"t:{targetType.Name}")
             .Select(AssetDatabase.GUIDToAssetPath)
             .ToList();

            List<ScriptableObjectWithAutoID> allAssets = new List<ScriptableObjectWithAutoID>();

            foreach (string assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);    

                if (asset == null)  
                    continue;
                
                allAssets.Add((ScriptableObjectWithAutoID) asset);
            }

            target.ChangeProcessor.DidChange(allAssets);

            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.None);
            
            Debug.Log($"{targetType.Name} enum was created!");
        }
    }
}
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public abstract class ScriptableObjectConverterBase<SOType> : MonoBehaviour
    where SOType : ScriptableObject
{
    //[HideInInspector]
    public List<string> scriptableObjects;

    public bool autoLoad = false;
    public bool update = false;
    
    public void GatherScriptableObjects()
    {
        scriptableObjects = new List<string>();
        
        if (!autoLoad)
            return; 

        //Debug.Log($"Find t: {typeof(SOType)}");
        var guids = AssetDatabase.FindAssets("t: " + typeof(SOType));

        foreach (var guid in guids)
        {
            scriptableObjects.Add(guid);
        }
    }

    public void Update()
    {
        if (update)
        {
            update = false;
            GatherScriptableObjects();
        }
    }
}

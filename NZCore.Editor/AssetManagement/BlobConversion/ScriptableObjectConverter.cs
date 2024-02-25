using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public abstract class ScriptableObjectConverterBase<SOType> : MonoBehaviour
    where SOType : ScriptableObject
{
    //[HideInInspector]
    public List<string> ScriptableObjects;

    public bool AutoLoad;
    public bool ForceUpdate;
    
    public void GatherScriptableObjects()
    {
        ScriptableObjects = new List<string>();
        
        if (!AutoLoad)
            return; 

        //Debug.Log($"Find t: {typeof(SOType)}");
        var guids = AssetDatabase.FindAssets("t: " + typeof(SOType));

        foreach (var guid in guids)
        {
            ScriptableObjects.Add(guid);
        }
    }

    public void Update()
    {
        if (!ForceUpdate) 
            return;
        
        ForceUpdate = false;
        GatherScriptableObjects();
    }
}

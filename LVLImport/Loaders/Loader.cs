using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public abstract class Loader {

    // Used by different loader classes
    protected static Container container = null;

    protected bool SaveAssets = false;
    protected string SaveDirectory = null;



    public bool SetSave(string prefix, string folderName)
    {
        if (folderName == null || folderName == "" || prefix == null || !prefix.StartsWith("Assets/"))
        {
            SaveAssets = false;
            return false;
        }

        if (!prefix.EndsWith("/"))
        {
            prefix = prefix + "/";
        }

        prefix = prefix + folderName;

        string[] pathLevels = prefix.Split('/');
        string pathAccum = "Assets";

        for (int i = 1; i < pathLevels.Length; i++)
        {
            string curPath = pathAccum + "/" + pathLevels[i];
            if (!AssetDatabase.IsValidFolder(curPath))
            {
                AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(pathAccum, pathLevels[i]));
            }
            pathAccum = curPath;
        }

        SaveDirectory = pathAccum;
        SaveAssets = true;

        return true;
    }


    public static void ResetAllLoaders()
    {
        TextureLoader.Instance.SaveAssets = false;
        TextureLoader.Instance.ResetDB();

        MaterialLoader.Instance.SaveAssets = false;
        MaterialLoader.Instance.ResetDB();

        ModelLoader.Instance.SaveAssets = false;
        ModelLoader.Instance.ResetDB();

        AnimationLoader.Instance.SaveAssets = false;
        AnimationLoader.Instance.ResetDB();

        ClassLoader.Instance.SaveAssets = false;
        ClassLoader.Instance.ResetDB();

        WorldLoader.Instance.SaveAssets = false;
    }


    public virtual float GetProgress(out string desc)
    {
        desc = "";
        return 1.0f;
    }

    public virtual bool IterateBatch()
    {
        return true;
    }

    public virtual void SetBatch(Level[] levels)
    {
        return;
    }



    public static bool SetGlobalContainer(Container lvlContainer)
    {
    	container = lvlContainer;
    	return true;
    }

    public static bool FreeGlobalContainer()
    {
        if (container == null) return false;

        container.Delete();
        container = null;
        return true;
    }
}


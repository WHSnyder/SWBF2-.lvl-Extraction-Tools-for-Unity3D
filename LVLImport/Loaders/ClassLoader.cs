using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Utils;


public class ClassLoader : Loader {

    public static ClassLoader Instance { get; private set; } = null;

    static ClassLoader()
    {
        Instance = new ClassLoader();
    }

    public Dictionary<string, GameObject> classObjectDatabase = new Dictionary<string, GameObject>();



    public const uint GEOMETRYNAME = 1204317002;
    public const uint ATTACHODF = 2849035403;
    public const uint ATTACHTOHARDPOINT = 1005041674;
    public const uint ANIMATIONNAME = 2555738718;
    public const uint ANIMATION = 3779456605;
    public const uint SOLDIERCOLLISION = 0x5dfdc07f;
    public const uint ORDNANCECOLLISION = 0xfb2bdf07;


    public void ResetDB()
    {
        classObjectDatabase.Clear();
    }


    public void DeleteAll()
    {
    }




    public string GetBaseClassName(string name)
    {
        var ecWrapper = container.Get<EntityClass>(name);

        if (ecWrapper == null)
        {
            return "";
        }

        return ecWrapper.BaseClassName;
    }


    private bool IsStaticObjectClass(EntityClass ec)
    {
        switch (ec.BaseClassName)
        {
        case "door":
        case "animatedprop":                  
        case "animatedbuilding":
            return false;
        default:
            return true;            
        }
    }


    /*
    Temporary solution until I get to recording the default properties of
    each base class.  That'll come with ODF -> Scriptable Object conversion.
    This loads the most relevant dependencies of a given ODF. 
    */

    public GameObject LoadGeneralClass(string name, bool tryMakeStatic = false)
    {
        if (name == null || name == "") return null;

        //Check if ODF already loaded
        if (classObjectDatabase.ContainsKey(name))
        {
            GameObject duplicate = null;
            if (SaveAssets)
            {
                duplicate = PrefabUtility.InstantiatePrefab(classObjectDatabase[name]) as GameObject;
            }
            else 
            {
                duplicate = UnityEngine.Object.Instantiate(classObjectDatabase[name]);     
            }

            if (duplicate == null)
            {
                return null;
            }

            duplicate.transform.localPosition = Vector3.zero;
            duplicate.transform.localRotation = Quaternion.identity;
            duplicate.transform.localScale = new Vector3(1.0f,1.0f,1.0f);
            return duplicate;
        }

        var ecWrapper = container.Get<EntityClass>(name);
        if (ecWrapper == null)
        {
            Debug.LogWarningFormat("\tObject class: {0} not defined in loaded levels...", name);
            return null;
        }

        List<uint> properties;
        List<string> values;
        
        try {
            if (!ecWrapper.GetOverriddenProperties(out uint[] p_, out string[] v_))
            {
                Debug.LogWarningFormat("\tFailed to load object class: {0}", name);
                return null;
            }
            properties = new List<uint>(p_);
            values = new List<string>(v_);
        } catch
        {
            Debug.LogWarningFormat("\tFailed to load object class: {0}", name);
            return null;
        }


        GameObject obj = new GameObject(name);

        GameObject lastAttached = null;
        string lastAttachedName = "";

        HashSet<string> ordinanceColliders = new HashSet<string>();

        string currentAnimationSet = "";

        int geomNameIndex;
        string geometryName = "";
        if ((geomNameIndex = properties.FindIndex(a => a == GEOMETRYNAME)) != -1)
        {
            geometryName = values[geomNameIndex];
            try {
                if (!ModelLoader.Instance.AddModelComponents(obj, geometryName))
                {
                    Debug.LogWarningFormat("Failed to load model {1} used by object {0}", name, geometryName);
                    return obj;
                }

                if (tryMakeStatic && IsStaticObjectClass(ecWrapper))
                {
                    obj.isStatic = true;
                    foreach (var tx in UnityUtils.GetChildTransforms(obj.transform))
                    {
                        tx.gameObject.isStatic = true;
                    }
                }
            }
            catch 
            {
                Debug.LogWarningFormat("Failed to load model {1} used by object {0}", name, geometryName);
                return obj;
            }
        }






        for (int i = 0; i < properties.Count; i++)
        {
            uint property = properties[i];
            string propertyValue = values[i];

            switch (property)
            {
                // Refers to an animation bank, for now we just get all the bank's clips
                // and attach them as a legacy Animation component.
                case ANIMATIONNAME:

                    currentAnimationSet = propertyValue;

                    var clips = AnimationLoader.Instance.LoadAnimationBank(propertyValue, obj.transform);
                    Animation animComponent = obj.GetComponent<Animation>();

                    if (animComponent == null)
                    {
                        animComponent = obj.AddComponent<Animation>();
                    }

                    foreach (var curClip in clips)
                    {
                        animComponent.AddClip(curClip, curClip.name);
                        animComponent.wrapMode = WrapMode.Once;                        
                    }

                    break;

                // Refers to specific animations for specific purposes (see animatedprop)
                case ANIMATION:
                    break;

                /*
                case GEOMETRYNAME:

                    geometryName = propertyValue;

                    try {
                        if (!ModelLoader.Instance.AddModelComponents(obj, geometryName))
                        {
                            Debug.LogError(String.Format("\tFailed to load model used by: {0}", name));
                            return obj;
                        }
                    }
                    catch 
                    {
                        return obj;
                    }
                    break;
                */

                case ATTACHODF:
                    lastAttachedName = propertyValue; //LoadGeneralClass(propertyValue);
                    break;

                // TODO: Hardpoint children are frequently missing...
                case ATTACHTOHARDPOINT:

                    lastAttached = LoadGeneralClass(lastAttachedName, tryMakeStatic);
                    if (lastAttached == null) break;


                    var childTx = UnityUtils.FindChildTransform(obj.transform, propertyValue);

                    if (childTx == null)
                    {
                        Debug.LogWarningFormat("\t{0}: Couldn't find hardpoint {1}", name, propertyValue);
                        lastAttached.transform.SetParent(obj.transform, false);
                    }
                    else 
                    {
                        lastAttached.transform.SetParent(childTx, false);
                    }

                    break;

                // Some collider primitives don't have proper masks, so their purpose is
                // listed here.  I think this was a BF1 holdover.  I chose ordinance masking
                // as it is most accurate.
                case ORDNANCECOLLISION:
                    ordinanceColliders.Add(propertyValue);
                    break;

                default:
                    break;
            }
        }

        ModelLoader.Instance.AddCollisionComponents(obj, geometryName, ordinanceColliders);

        if (SaveAssets)
        {
            // This breaks when called inside an AssetEditing block...
            classObjectDatabase[name] = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, SaveDirectory + "/" + obj.name + ".prefab", InteractionMode.UserAction);
        }
        else
        {
            classObjectDatabase[name] = obj;
        }

        return obj;
    }


    

    public void DeleteAndClearDB()
    {
        foreach (string objname in classObjectDatabase.Keys)
        {
            try
            {
                PrefabUtility.UnpackPrefabInstance(classObjectDatabase[objname], PrefabUnpackMode.Completely, InteractionMode.UserAction);
                UnityEngine.Object.DestroyImmediate(classObjectDatabase[objname]);
            }catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }
        classObjectDatabase.Clear();
    }


}

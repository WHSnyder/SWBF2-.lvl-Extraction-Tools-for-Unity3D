using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.InteropServices.WindowsRuntime;

using UnityEngine;

using LibSWBF2.Types;
using LibSWBF2.Wrappers;

using UVector2 = UnityEngine.Vector2;
using UVector3 = UnityEngine.Vector3;


public static class UnityUtils {

    /*
    Credits: https://gamedev.stackexchange.com/a/183962/138651
    Gets bounding box of gameobject and all children.
    */

    public static Bounds GetMaxBounds(GameObject g) {
        var renderers = g.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(g.transform.position, UVector3.zero);
        var b = renderers[0].bounds;
        foreach (Renderer r in renderers) {
           b.Encapsulate(r.bounds);
        }
        return b;
    }


    /*
    Recursively descends transform hierarchy in search of childName.
    */

    public static Transform FindChildTransform(Transform trans, string childName)
    {
        for (int j = 0; j < trans.childCount; j++)
        {
            var curTransform = trans.GetChild(j);

            if (curTransform.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
            {
                return curTransform;
            }

            var t = FindChildTransform(curTransform, childName);

            if (t != null)
            {
                return t;
            }
        }

        return null;
    }



    public static List<Transform> GetChildTransforms(Transform curTx)
    {
        List<Transform> txs = new List<Transform>();
        for (int j = 0; j < curTx.childCount; j++)
        {
            var curChild = curTx.GetChild(j);
            txs.Add(curChild);
            txs.AddRange(GetChildTransforms(curChild));
        }

        return txs;
    }    




    //Reversed libSWBF2 GLM transform decomp + munge conversion leads to this mess.
    //Perhaps quat components are switched up in lib's XFRM -> Quat converter
	public static UnityEngine.Quaternion QuatFromLibWorld(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(-vec.Z, vec.W, -vec.X, vec.Y);
    }
    //''
    public static UVector3 Vec3FromLibWorld(LibSWBF2.Types.Vector3 vec)
    {
        return new UVector3(vec.X,vec.Y,-vec.Z);
    }


    public static UnityEngine.Quaternion QuatFromLib(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(vec.X, vec.Y, vec.Z, vec.W);
    }

    public static UVector3 Vec3FromLib(LibSWBF2.Types.Vector3 vec)
    {
        return new UVector3(vec.X, vec.Y, vec.Z);
    }


    // For some reason lights don't get the same mungetime conversion as all other world objects do
    public static UnityEngine.Quaternion QuatFromLibLGT(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(vec.Y, vec.Z, vec.W, vec.X);   
    }


    public static UnityEngine.Color ColorFromLib(LibSWBF2.Types.Vector3 vec, bool ScaleDown = false)
    {
        if (ScaleDown)
        {
            return new UnityEngine.Color(vec.X/255.0f, vec.Y/255.0f, vec.Z/255.0f);
        }
        return new UnityEngine.Color(vec.X,vec.Y,vec.Z);
    }



    //Skeleton transform conversion to match vertex data handedness flip
    public static UnityEngine.Quaternion QuatFromLibSkel(LibSWBF2.Types.Vector4 vec)
    {
        return new UnityEngine.Quaternion(-vec.X, vec.Y, vec.Z, -vec.W);
    }

    //''
    public static UVector3 Vec3FromLibSkel(LibSWBF2.Types.Vector3 vec)
    {
        return new UVector3(-vec.X, vec.Y, vec.Z);
    }



    public static void ConvertSpaceAndFillVec3(UVector3[] vectorsIn, UVector3[] vectorsOut, int offset=0, bool flipX = true)
    {
        if (vectorsIn != null){
            for (int i = 0; i < vectorsIn.Length; i++)
            {
                UVector3 cur = vectorsIn[i];
                cur.x = flipX ? -cur.x : cur.x;
                vectorsOut[i + offset] = cur;
            }
        } 
    }


    public static ushort[] ReverseWinding(ushort[] indices)
    {
        ushort temp;
        for (int i = 0; i < indices.Length; i+=3)
        {
            temp = indices[i];
            indices[i] = indices[i+2];
            indices[i+2] = temp;
        }
        return indices;
    }




    /*
    Only parameter of note is fix.  This is used for broken skeletons
    (so far only example is Jabba from tat3.lvl) where 
    bone weight indices are off by 1.
    */

    public static void FillBoneWeights(VertexWeight[] libVertexWeights, BoneWeight1[] UBoneWeights, int offset, int fix = 0)
    {
        if (libVertexWeights != null)
        {
            for (int i = 0; i < libVertexWeights.Length; i++)
            {
                int windex = (int) libVertexWeights[i].index;
                float wvalue = libVertexWeights[i].weight;

                UBoneWeights[offset + i].boneIndex = windex + fix;
                UBoneWeights[offset + i].weight = wvalue;                                   
            }
        }
    }
}



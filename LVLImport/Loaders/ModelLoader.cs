using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;

using LibMaterial = LibSWBF2.Wrappers.Material;
using UMaterial = UnityEngine.Material;

public class ModelLoader : ScriptableObject {

    public static Dictionary<string, UMaterial> materialDataBase = new Dictionary<string, UMaterial>();
    public static UMaterial defaultMaterial = new UMaterial(Shader.Find("Standard"));

    public static void ResetDB()
    {
        materialDataBase.Clear();
    }


    public static UMaterial GetMaterial(LibMaterial mat)
    {
        string texName = mat.textures[0];
        uint matFlags = mat.materialFlags;

        if (texName == "")
        {
            return defaultMaterial;
        } 
        else 
        {
            string materialName = texName + "_" + matFlags.ToString();

            if (!materialDataBase.ContainsKey(materialName))
            {
                UMaterial material = new UMaterial(defaultMaterial);
                material.name = materialName;

                if (MaterialsUtils.IsCutout(matFlags))
                {
                    MaterialsUtils.SetRenderMode(ref material, 1);
                }
                else if (MaterialsUtils.IsTransparent(matFlags))
                {
                    MaterialsUtils.SetRenderMode(ref material, 3);
                }

                Texture2D importedTex = TextureLoader.ImportTexture(texName);
                if (importedTex != null)
                {
                    material.mainTexture = importedTex;
                }

                materialDataBase[materialName] = material;
            }

            return materialDataBase[materialName];
        }
    }


    private static bool AddHierarchy(ref GameObject newObject, Model model, out Dictionary<string, Transform> skeleton)
    {
        LibSWBF2.Wrappers.Bone[] hierarchy = model.GetSkeleton();
        Dictionary<string, Transform> hierarchyMap = new Dictionary<string, Transform>();

        foreach (var node in hierarchy)
        {
            var nodeTransform = new GameObject(node.name).transform;
            nodeTransform.localRotation = UnityUtils.QuatFromLibSkel(node.rotation);
            nodeTransform.localPosition = UnityUtils.Vec3FromLibSkel(node.location);
            hierarchyMap[node.name] = nodeTransform;
        }

        foreach (var node in hierarchy)
        {   
            if (node.parentName.Equals(""))
            {
                hierarchyMap[node.name].SetParent(newObject.transform, false);
            }
            else 
            {
                hierarchyMap[node.name].SetParent(hierarchyMap[node.parentName], false);   
            }
        }

        skeleton = hierarchyMap;
        return true;
    }



    private static int AddWeights(ref GameObject obj, Model model, ref Mesh mesh, bool broken = false)
    {
        var segments = model.GetSegments();

        int totalLength = (int) segments.Sum(item => item.GetVertexBufferLength());
        int txStatus = segments.Sum(item => item.IsPretransformed() ? 1 : 0);

        if (txStatus != 0 && txStatus != segments.Length)
        {
            Debug.LogError(String.Format("Model {0} has heterogeneous pretransformation!", model.Name));
            return 0;
        }

        byte bonesPerVert = (byte) (txStatus == 0 ? 3 : 1);  

        BoneWeight1[] weights = new BoneWeight1[totalLength * bonesPerVert];

        int dataOffset = 0;
        foreach (Segment seg in segments)
        {           
            //Debug.Log(String.Format("Model: {2}, Verts length: {0}, Weights length: {1}", libVerts.Length, libWeights.Length, modelName));
            UnityUtils.FillBoneWeights(seg.GetVertexWeights(), weights, dataOffset, broken ? -1 : 0);            
            dataOffset += (int) seg.GetVertexBufferLength() * bonesPerVert;
        }
        var weightsArray = new NativeArray<BoneWeight1>(weights, Allocator.Temp);

        byte[] bonesPerVertex = Enumerable.Repeat<byte>(bonesPerVert, totalLength).ToArray();
        var bonesPerVertexArray = new NativeArray<byte>(bonesPerVertex, Allocator.Temp);

        mesh.SetBoneWeights(bonesPerVertexArray, weightsArray);

        return (int) bonesPerVert;
    }




    public static bool AddModelComponentsHierarchical(ref GameObject newObject, Model model,
                                                    Dictionary<string, Transform> skeleton)
    {
        Dictionary<string, List<Segment>> segmentMap = new Dictionary<string, List<Segment>>();
        foreach (var segment in model.GetSegments())
        {
            string boneName = segment.GetBone();

            if (boneName.Equals("")) continue;

            if (!segmentMap.ContainsKey(boneName))
            {
                segmentMap[boneName] = new List<Segment>();
            }
            
            segmentMap[boneName].Add(segment);
        }


        foreach (string boneName in segmentMap.Keys)
        {
            GameObject boneObj = skeleton[boneName].gameObject;
            List<Segment> segments = segmentMap[boneName];

            Mesh mesh = new Mesh();
            UMaterial[] mats = new UMaterial[segments.Count];

            mesh.subMeshCount = segments.Count;

            int totalLength = 0;
            foreach (Segment seg in segments)
            {
                totalLength += (int) seg.GetVertexBufferLength();
            }

            Vector3[] positions = new Vector3[totalLength];
            Vector3[] normals = new Vector3[totalLength];
            Vector2[] texcoords = new Vector2[totalLength];
            int[] offsets = new int[segments.Count];

            int dataOffset = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                Segment seg = segments[i];

                // Handle material data
                mats[i] = GetMaterial(seg.GetMaterial());

                // Handle vertex data
                UnityUtils.ConvertSpaceAndFillVec3(seg.GetVertexBuffer(), positions, dataOffset, true);
                UnityUtils.ConvertSpaceAndFillVec3(seg.GetNormalsBuffer(), normals, dataOffset, true);
                UnityUtils.FillVec2(seg.GetUVBuffer(), texcoords, dataOffset);

                offsets[i] = dataOffset;

                dataOffset += (int) seg.GetVertexBufferLength();
            }

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0,texcoords);

            int j = 0;
            foreach (Segment seg in segments)
            {
                int[] rewound = UnityUtils.ReverseWinding(seg.GetIndexBuffer());
                mesh.SetTriangles(rewound, j, true, offsets[j]);
                j++;
            }

            MeshFilter filter = boneObj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = boneObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = mats;

            boneObj.transform.localScale = new UnityEngine.Vector3(1.0f,1.0f,1.0f);
        }

        return true;
    }



    public static bool AddModelComponents(ref GameObject newObject, string modelName)
    {   
        Model model = CentralLoader.GetModel(modelName);

        if (model == null)
        {
            Debug.Log(String.Format("ERROR: Failed to load model: {0}", modelName));
            return false;
        }

        if (!AddHierarchy(ref newObject, model, out Dictionary<string, Transform> skeleton))
        {
            return false;
        }

        if (model.HasNonTrivialHierarchy && !model.IsSkeletalMesh)
        {
            return AddModelComponentsHierarchical(ref newObject, model, skeleton);
        }

        Mesh mesh = new Mesh();

        Segment[] segments = model.GetSegments(); 
        UMaterial[] mats = new UMaterial[segments.Length];

        mesh.subMeshCount = segments.Length;

        int totalLength = (int) segments.Sum(item => item.GetVertexBufferLength());


        Vector3[] positions = new Vector3[totalLength];
        Vector3[] normals = new Vector3[totalLength];
        Vector2[] texcoords = new Vector2[totalLength];
        //BoneWeight1[] weights = new BoneWeight1[model.IsSkeletalMesh ? totalLength * 4 : 0];
        int[] offsets = new int[segments.Length];

        int dataOffset = 0;

        for (int i = 0; i < segments.Length; i++)
        {
            Segment seg = segments[i];

            // Handle material data
            mats[i] = GetMaterial(seg.GetMaterial());

            // Handle vertex data
            var libVerts = seg.GetVertexBuffer();

            UnityUtils.ConvertSpaceAndFillVec3(libVerts, positions, dataOffset, true);
            UnityUtils.ConvertSpaceAndFillVec3(seg.GetNormalsBuffer(), normals, dataOffset, true);
            UnityUtils.FillVec2(seg.GetUVBuffer(), texcoords, dataOffset);

            offsets[i] = dataOffset;

            dataOffset += (int) seg.GetVertexBufferLength();
        }

        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0,texcoords);

        if (model.IsSkeletalMesh)
        {
            /*
            if (!AddWeights(ref newObject, model, ref mesh, out bool isPretransformed))
            {
                Debug.Log("Failed to add weights....");
            }
            */
        }
        
        int j = 0;
        foreach (Segment seg in segments)
        {
            int[] rewound = UnityUtils.ReverseWinding(seg.GetIndexBuffer());
            mesh.SetTriangles(rewound, j, true, offsets[j]);
            j++;
        }

        if (model.IsSkeletalMesh)
        {
            int skinType = AddWeights(ref newObject, model, ref mesh, model.IsSkeletonBroken);
            if (skinType == 0)
            {
                //Debug.LogWarning("Failed to add weights....");
            }

            SkinnedMeshRenderer skinRenderer = newObject.AddComponent<SkinnedMeshRenderer>();
            LibSWBF2.Wrappers.Bone[] bonesSWBF = model.GetSkeleton();

            /*
            Set bones
            */
            Transform[] bones = new Transform[bonesSWBF.Length];
            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                //Debug.Log("\t\tSetting bindpose of " + curBoneSWBF.name + " Parent name = " + curBoneSWBF.parentName);
                bones[boneNum] = skeleton[curBoneSWBF.name];
                bones[boneNum].SetParent(curBoneSWBF.parentName != null && curBoneSWBF.parentName != "" && !curBoneSWBF.parentName.Equals(curBoneSWBF.name) ? skeleton[curBoneSWBF.parentName] : newObject.transform, false);
            }

            /*
            Set bindposes...
            */
            Matrix4x4[] bindPoses = new Matrix4x4[bonesSWBF.Length];
            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                if (skinType == 1)
                {
                    //For pretransformed skins...
                    bindPoses[boneNum] = Matrix4x4.identity;
                }
                else 
                {
                    bindPoses[boneNum] = bones[boneNum].worldToLocalMatrix * bones[0].parent.localToWorldMatrix;
                }
                //But what works for sarlacctentacle?
            }

            mesh.bindposes = bindPoses;

            skinRenderer.bones = bones;
            skinRenderer.sharedMesh = mesh;
            skinRenderer.sharedMaterials = mats;

            for (int boneNum = 0; boneNum < bonesSWBF.Length; boneNum++)
            {
                var curBoneSWBF = bonesSWBF[boneNum];
                skeleton[curBoneSWBF.name].localRotation = UnityUtils.QuatFromLibSkel(curBoneSWBF.rotation);
                skeleton[curBoneSWBF.name].localPosition = UnityUtils.Vec3FromLibSkel(curBoneSWBF.location);
            }
        }
        else
        {
            MeshFilter filter = newObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = newObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = mats;
        }


        CollisionMesh collMesh = model.GetCollisionMesh();
        if (collMesh != null)
        {
            uint[] indBuffer = collMesh.GetIndices();

            try {

                if (indBuffer.Length > 2)
                {
                    Mesh collMeshUnity = new Mesh();
                    collMeshUnity.vertices = UnityUtils.FloatToVec3Array(collMesh.GetVertices(), false);
                    
                    collMeshUnity.SetIndexBufferParams(indBuffer.Length, IndexFormat.UInt32);
                    collMeshUnity.SetIndexBufferData(indBuffer, 0, 0, indBuffer.Length);

                    MeshCollider meshCollider = newObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = collMeshUnity;
                }
            } 
            catch (Exception e)
            {
                Debug.Log(e.ToString() + " while creating mesh collider...");
                return false;
            }            
        }

        newObject.transform.localScale = new UnityEngine.Vector3(1.0f,1.0f,1.0f);

        return true;      
    }


    public static void ImportModels(Level level)
    {
        Model[] models = level.GetModels();
        
        foreach (Model model in models)
        {
            if (model.Name.Contains("LOWD")) continue;
            //GameObject newObject = ModelLoader.GameObjectFromModel(level, model);
        } 
    }
}

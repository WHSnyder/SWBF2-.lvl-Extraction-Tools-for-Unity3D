using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Enums;
using LibSWBF2.Utils;

using LibTerrain = LibSWBF2.Wrappers.Terrain;
using UMaterial = UnityEngine.Material;
using LibVec3 = LibSWBF2.Types.Vector3;
using ULight = UnityEngine.Light;



public class WorldLoader : Loader {

    public bool TerrainAsMesh = false;

    public static WorldLoader Instance { get; private set; } = null;
    static WorldLoader()
    {
        Instance = new WorldLoader();
    }

    private WorldLoader()
    {
        loadedSkydomes = new Dictionary<string, GameObject>();        
    }


    private Dictionary<string, GameObject> loadedSkydomes;


    public void Reset()
    {
        loadedSkydomes = new Dictionary<string, GameObject>();
    }



    class WorldLoadState
    {
        public World world;
        public Transform WorldRoot;
        public Transform InstancesRoot;
        
        public int InstIndex;
        public bool DoneTerrain, DoneLighting, DoneSkydome, DoneRegions;

        public WorldLoadState(World wld)
        {
            world = wld;
            WorldRoot = new GameObject(wld.name).transform;
            
            InstancesRoot = new GameObject("Instances").transform;
            InstancesRoot.parent = WorldRoot;

            InstIndex = 0;

            DoneTerrain = DoneLighting = DoneSkydome = DoneRegions = false;
        }

        public float GetProgress()
        {
            int numInstances = world.GetInstances().Length;
            float progress = .6f * (numInstances == 0 ? 1.0f : (float) InstIndex / (float) numInstances);

            progress += DoneTerrain ? 0.1f : 0.0f;
            progress += DoneLighting ? 0.1f : 0.0f;
            progress += DoneSkydome ? 0.1f : 0.0f;
            progress += DoneRegions ? 0.1f : 0.0f;

            return progress;
        }
    }


    class BatchLoadState
    {
        public List<WorldLoadState> WorldImports;
        public int WorldImportIndex;

        public BatchLoadState(List<WorldLoadState> wldImps)
        {
            WorldImports = wldImps;
            WorldImportIndex = 0;
        }
    }

    private BatchLoadState batch = null;


    public override void SetBatch(Level[] levels)
    {
        List<WorldLoadState> worldBatches = new List<WorldLoadState>();
        foreach (Level level in levels)
        {
            foreach (World wld in level.GetWrappers<World>())
            {
                worldBatches.Add(new WorldLoadState(wld));
            }
        }

        batch = new BatchLoadState(worldBatches);
    }


    public override float GetProgress(out string desc)
    {   
        if (batch.WorldImportIndex >= batch.WorldImports.Count)
        {
            desc = "";
            return 1.0f;
        }

        desc = batch.WorldImports[batch.WorldImportIndex].world.name;
        return batch.WorldImports[batch.WorldImportIndex].GetProgress();  
    }


    public override bool IterateBatch()
    {
        if (batch.WorldImportIndex >= batch.WorldImports.Count) return false;

        var curWorldLoadState = batch.WorldImports[batch.WorldImportIndex];
        var curWorld = curWorldLoadState.world;

        Transform worldRoot = curWorldLoadState.WorldRoot;
        Instance[] curInstances = curWorld.GetInstances();
        if (curWorldLoadState.InstIndex < curInstances.Length)
        {
            Instance curInst = curInstances[curWorldLoadState.InstIndex++];
            GameObject instObj = ImportInstance(curInst);
            if (instObj != null)
            {
                instObj.transform.parent = curWorldLoadState.InstancesRoot;
            }
            return true;    
        }

        if (!curWorldLoadState.DoneTerrain)
        {
            curWorldLoadState.DoneTerrain = true;
            var terrain = curWorld.GetTerrain();

            if (terrain != null)// && !LoadedTerrain)
            {
                GameObject terrainGameObject;
                if (TerrainAsMesh)
                {
                    terrainGameObject = ImportTerrainAsMesh(terrain, curWorld.name);
                }
                else 
                {
                    terrainGameObject = ImportTerrain(terrain, curWorld.name);
                }

                terrainGameObject.transform.parent = worldRoot;
                //LoadedTerrain = true;
            }
            return true;
        }

        if (!curWorldLoadState.DoneLighting)
        {
            curWorldLoadState.DoneLighting = true;
            var lightingRoots = ImportLights(container.FindConfig(ConfigType.Lighting, curWorld.name)); 
            foreach (var lightingRoot in lightingRoots)
            {
                lightingRoot.transform.parent = worldRoot;
            }
            return true;
        }

        if (!curWorldLoadState.DoneRegions)
        {
            curWorldLoadState.DoneRegions = true;
            var regionsRoot = ImportRegions(curWorld.GetRegions());
            regionsRoot.transform.parent = worldRoot;
            return true;
        }

        if (!curWorldLoadState.DoneSkydome)
        {
            if (!loadedSkydomes.ContainsKey(curWorld.skydomeName))
            {
                var skyRoot = ImportSkydome(container.FindConfig(ConfigType.Skydome, curWorld.skydomeName));
                if (skyRoot != null)
                {
                    skyRoot.transform.parent = worldRoot;
                }

                loadedSkydomes[curWorld.skydomeName] = skyRoot;
            }
            curWorldLoadState.DoneSkydome = true;
            return true;
        }

        return (++batch.WorldImportIndex < batch.WorldImports.Count); 
    }


    private GameObject ImportInstance(Instance inst)
    {
        string entityClassName = inst.entityClassName;
        string baseName = ClassLoader.Instance.GetBaseClassName(entityClassName);

        GameObject instanceObject = null;

        switch (baseName)
        {
            case "door":
            case "animatedprop":                  
            case "prop":
            case "building":
            case "destructablebuilding":
            case "armedbuilding":
            case "animatedbuilding":
            case "commandpost":
                instanceObject = ClassLoader.Instance.LoadGeneralClass(entityClassName,true);
                break;

            default:
                break; 
        }

        if (instanceObject == null)
        {
            return null;
        }

        if (!inst.name.Equals(""))
        {
            instanceObject.name = inst.name;
        }

        instanceObject.transform.rotation = UnityUtils.QuatFromLibWorld(inst.rotation);
        instanceObject.transform.position = UnityUtils.Vec3FromLibWorld(inst.position);
        instanceObject.transform.localScale = new Vector3(1.0f,1.0f,1.0f);
        
        return instanceObject;
    }



    private GameObject ImportTerrainAsMesh(LibTerrain terrain, string name)
    {
        Mesh terrainMesh = new Mesh();
        if (SaveAssets)
        {
            AssetDatabase.CreateAsset(terrainMesh, Path.Combine(SaveDirectory, name + "_terrain.mesh"));
        }

        terrainMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        terrainMesh.vertices = terrain.GetPositionsBuffer<Vector3>();
        terrainMesh.triangles = Array.ConvertAll(terrain.GetIndexBuffer(), s => ((int) s));
        terrainMesh.RecalculateNormals();


        GameObject terrainObj = new GameObject("Terrain");
        terrainObj.isStatic = true;

        MeshFilter filter = terrainObj.AddComponent<MeshFilter>();
        filter.sharedMesh = terrainMesh;
       
        UMaterial terrainMat = new UMaterial(Shader.Find("ConversionAssets/TerrainTest"));
        if (SaveAssets)
        {
            AssetDatabase.CreateAsset(terrainMat, Path.Combine(SaveDirectory, name + "_terrain.mat"));
        }

        MeshRenderer renderer = terrainObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMat;

        int i = 0;
        foreach (string texName in terrain.layerTextures)
        {
            Texture2D tex = TextureLoader.Instance.ImportTexture(texName);
            string layerTexName = "_LayerXXTex".Replace("XX",i.ToString());

            if (tex != null)
            {
                renderer.sharedMaterial.SetTexture(layerTexName, tex);  
            }
            i++;
        }

        terrain.GetBlendMap(out uint blendDim, out uint numLayers, out byte[] blendMapRaw);  
        
        for (i = 0; i < 4; i++)
        {
            Texture2D blendTex = new Texture2D((int) blendDim, (int) blendDim);

            Color[] colors = blendTex.GetPixels(0);

            for (int w = 0; w < blendDim; w++)
            {
                for (int h = 0; h < blendDim; h++)
                {
                    Color col = Color.black;
                    int baseIndex = (int) (numLayers * (w * blendDim + h));
                    int offset = i * 4;

                    for (int z = 0; z < 4; z++)
                    {
                        if (offset + z < numLayers)
                        {
                            col[z] = ((float) blendMapRaw[baseIndex + offset + z]) / 255.0f;  
                        }
                    }

                    colors[(blendDim - w - 1) * blendDim + h] = col;
                }
            } 

            blendTex.SetPixels(colors,0);
            blendTex.Apply();

            if (SaveAssets)
            {
                string blendSlicePath = SaveDirectory + "/blendmap_slice_" + i.ToString() + ".png";
                File.WriteAllBytes(blendSlicePath, blendTex.EncodeToPNG());
                AssetDatabase.ImportAsset(blendSlicePath, ImportAssetOptions.Default);
                blendTex = (Texture2D) AssetDatabase.LoadAssetAtPath(blendSlicePath, typeof(Texture2D));
            }

            renderer.sharedMaterial.SetTexture("_BlendMap" + i.ToString(), blendTex);
        }

        terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
        float bound = (float) (dim * dimScale);
        renderer.sharedMaterial.SetFloat("_XBound", bound);
        renderer.sharedMaterial.SetFloat("_ZBound", bound);

        if (SaveAssets)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(terrainObj, SaveDirectory + "/" + name + "_terrain.prefab", InteractionMode.UserAction);
        }

        terrainObj.transform.localScale = new UnityEngine.Vector3(1.0f,1.0f,-1.0f);
        return terrainObj;
    }




    private GameObject ImportTerrain(LibTerrain terrain, string name)
    {
        //Read heightmap
        terrain.GetHeightMap(out uint dim, out uint dimScale, out float[] heightsRaw);
        float floor = terrain.heightLowerBound;
        float ceiling = terrain.heightUpperBound;
        
        TerrainData terData = new TerrainData();

        if (SaveAssets)
        {
            AssetDatabase.CreateAsset(terData, SaveDirectory + "/" + name + "_terrain_data.asset");
        }

        terData.heightmapResolution = (int) dim + 1;
        terData.size = new Vector3(dim * dimScale, ceiling - floor, dim * dimScale);
        terData.baseMapResolution = 512;
        terData.SetDetailResolution(512, 8);

        float[,] heights = new float[dim,dim];
        bool[,] holes    = new bool[dim,dim];

        for (int x = 0; x < dim; x++)
        {
            for (int y = 0; y < dim; y++)
            {
                float h = heightsRaw[(dim - 1 - x) * dim + y];
                heights[x,y] = h < -0.1 ? 0 : h;
                holes[x,y] = h < -0.1 ? false : true;
            }
        }
        terData.SetHeights(0, 0, heights);
        terData.SetHoles(0,0,holes);
        

        //Get list of textures used
        List<Texture2D> terTextures = new List<Texture2D>();
        foreach (string texName in terrain.layerTextures)
        {
            Texture2D tex = TextureLoader.Instance.ImportTexture(texName);
            if (tex != null)
            {
                terTextures.Add(tex);  
            }
        }

        terrain.GetBlendMap(out uint blendDim, out uint numLayers, out byte[] blendMapRaw);  


        //Assign layers
        TerrainLayer[] terrainLayers = new TerrainLayer[numLayers];
        
        for (int i = 0; i < numLayers && i < terTextures.Count; i++)
        {
            TerrainLayer newLayer = new TerrainLayer();
            newLayer.diffuseTexture = terTextures[i];
            newLayer.tileSize = new Vector2(32,32);
            terrainLayers[i] = newLayer;
        }

        terData.SetTerrainLayersRegisterUndo(terrainLayers,"Undo");


        //Read blendmap
        float[,,] blendMap = new float[blendDim, blendDim, numLayers];

        for (int y = 0; y < blendDim; y++)
        {
            for (int x = 0; x < blendDim; x++)
            {
                int baseIndex = (int) (numLayers * (y * blendDim + x));
                for (int z = 0; z < numLayers; z++)
                {
                    blendMap[blendDim - y - 1,x,z] = ((float) blendMapRaw[baseIndex + z]) / 255.0f;    
                }
            }
        }

        terData.alphamapResolution = (int) blendDim;
        terData.SetAlphamaps(0, 0, blendMap);
        terData.SetBaseMapDirty();


        //Save terrain/create gameobj
        GameObject terrainObj = UnityEngine.Terrain.CreateTerrainGameObject(terData);
        int dimOffset = -1 * ((int) (dimScale * dim)) / 2;
        terrainObj.transform.position = new Vector3(dimOffset,floor,dimOffset);
        
        if (SaveAssets)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(terrainObj, SaveDirectory + "/" + name + "_terrain.prefab", InteractionMode.UserAction);
        }

        return terrainObj;
    }



    /*
    Lighting -- Still don't know why Z coord has to be reversed + Y coord slightly increased...
    */
    private List<GameObject> ImportLights(Config lightingConfig, bool SetAmbient=false)
    {
        List<GameObject> lightObjects = new List<GameObject>();

        if (lightingConfig == null) return lightObjects;


        GameObject globalLightsRoot = new GameObject("GlobalLights");
        lightObjects.Add(globalLightsRoot);

        string light1Name = "", light2Name = "";
        Field globalLighting = lightingConfig.GetField("GlobalLights");
        if (globalLighting != null)
        {
            Scope gl = globalLighting.scope;
            light1Name = gl.GetField("Light1").GetString();
            light2Name = gl.GetField("Light2").GetString();

            Color topColor = UnityUtils.ColorFromLib(gl.GetField("Top").GetVec3(), true);
            Color bottomColor = UnityUtils.ColorFromLib(gl.GetField("Bottom").GetVec3(), true);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientGroundColor = bottomColor;
            RenderSettings.ambientSkyColor = topColor;
        }


        List<Field> lightFields = lightingConfig.GetFields("Light");

        GameObject localLightsRoot = new GameObject("LocalLights");
        lightObjects.Add(localLightsRoot);

        foreach (Field light in lightFields) 
        {
            string lightName = light.GetString();
            Scope sl = light.scope;

            bool IsGlobal = String.Equals(lightName, light1Name, StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(lightName, light2Name, StringComparison.OrdinalIgnoreCase);


            GameObject lightObj = new GameObject(lightName);

            lightObj.transform.rotation = UnityUtils.QuatFromLibLGT(sl.GetVec4("Rotation"));

            LibVec3 lightPos = sl.GetVec3("Position");

            lightPos.Z *= -1.0f;
            lightPos.Y += .2f;
            lightObj.transform.position = UnityUtils.Vec3FromLibWorld(lightPos);


            ULight lightComp = lightObj.AddComponent<ULight>();
            lightComp.color = UnityUtils.ColorFromLib(sl.GetVec3("Color"));

            float ltype = sl.GetFloat("Type");
            float range = sl.GetFloat("Range");

            if (ltype == 2.0f)
            {   
                lightComp.type = UnityEngine.LightType.Point;
                lightComp.range = range;
                lightComp.intensity = 4.0f;           
            }
            else if (ltype == 3.0f)
            {
                lightComp.type = UnityEngine.LightType.Spot;
                lightComp.range = range;
                lightComp.spotAngle = sl.GetVec2("Cone").X * Mathf.Rad2Deg;   
                lightComp.intensity = IsGlobal ? 2.0f : 0.5f;
            }
            else if (ltype == 1.0f)
            {
                lightComp.type = UnityEngine.LightType.Directional;
                lightComp.intensity = IsGlobal ? 1.0f : 0.3f;
                //lightComp.range = light.range;
                //lightComp.spotAngle = light.spotAngles.X * Mathf.Rad2Deg;   
            }
            else 
            {
                Debug.LogWarning("Cant handle light type for " + light.name + " yet");
                continue;
            }

            if (IsGlobal)
            {
                lightObj.transform.SetParent(globalLightsRoot.transform,false);
            }
            else
            {
                lightObj.transform.SetParent(localLightsRoot.transform,false);
            }

        }

        return lightObjects;
    }


    private GameObject ImportSkydome(Config skydomeConfig)
    {
        if (skydomeConfig == null) return null;


        GameObject skyRoot = new GameObject("Skydome");

        //Import dome
        GameObject domeRoot = new GameObject("Dome");
        domeRoot.transform.parent = skyRoot.transform;
        
        Field domeInfo = skydomeConfig.GetField("DomeInfo");
        if (domeInfo != null)
        {
            Scope sDi = domeInfo.scope;

            //Havent decided re this yet
            Color ambient = UnityUtils.ColorFromLib(sDi.GetVec3("Ambient"));

            List<Field> domeModelFields = sDi.GetFields("DomeModel");
            foreach (Field domeModelField in domeModelFields)
            {
                Scope sD = domeModelField.scope;
                string geometryName = sD.GetString("Geometry");
                GameObject domeModelObj = new GameObject(geometryName);

                ModelLoader.Instance.AddModelComponents(domeModelObj, geometryName);
                try {
                    MaterialLoader.Instance.PatchMaterial(domeModelObj.transform.GetChild(0).gameObject, "skydome");
                } catch {}

                if (SaveAssets)
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(domeModelObj, SaveDirectory + "/dome_model_" + geometryName + ".prefab", InteractionMode.UserAction);
                }

                domeModelObj.transform.localScale = new Vector3(-300,300,300);
                domeModelObj.transform.parent = domeRoot.transform;
            }
        }


        //Import dome objects, one of each for now
        GameObject domeObjectsRoot = new GameObject("SkyObjects");
        domeObjectsRoot.transform.parent = skyRoot.transform;

        List<Field> domeObjectFields = skydomeConfig.GetFields("SkyObject");
        foreach (Field domeObjectField in domeObjectFields)
        {
            string geometryName = domeObjectField.scope.GetString("Geometry");
            GameObject domeObject = new GameObject(geometryName);

            ModelLoader.Instance.AddModelComponents(domeObject, geometryName);

            if (SaveAssets)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(domeObject, SaveDirectory + "/dome_object_" + geometryName + ".prefab", InteractionMode.UserAction);
            }

            domeObject.transform.parent = domeObjectsRoot.transform;
            domeObject.transform.localPosition = new Vector3(0, domeObjectField.scope.GetVec2("Height").X, 0);
        }

        return skyRoot;
    }
    


    private GameObject ImportRegions(Region[] regions)
    {
        GameObject regionsRoot = new GameObject("Regions");
        foreach (Region region in regions)
        {
            GameObject regionObj = new GameObject(region.name);
            regionObj.transform.position = UnityUtils.Vec3FromLibWorld(region.position);
            regionObj.transform.rotation = UnityUtils.QuatFromLibWorld(region.rotation);

            LibVec3 sz = region.size;

            //if (region.type.Equals("box"))
            //{
            BoxCollider coll = regionObj.AddComponent<BoxCollider>();
            coll.size = new Vector3(sz.X,sz.Y,sz.Z);
            coll.isTrigger = true;
            //}
            //else 
            //{
            //    CapsuleCollider coll = region.AddComponent<CapsuleCollider>();
            //    coll.height = new Vector3(sz.X,sz.Y,sz.Z);
            //}

            regionObj.transform.parent = regionsRoot.transform;
        }

        return regionsRoot;
    }


    public void ImportWorld(World world)
    {
        var wldImps = new List<WorldLoadState>();
        wldImps.Add(new WorldLoadState(world));
        batch = new BatchLoadState(wldImps);
        while (IterateBatch()){}
    }
}

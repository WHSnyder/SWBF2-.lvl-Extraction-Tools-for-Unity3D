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

using LibSWBF2.Types;
using LibVector2 = LibSWBF2.Types.Vector2;
using LibVector3 = LibSWBF2.Types.Vector3;
using LibVector4 = LibSWBF2.Types.Vector4;

using UVector2 = UnityEngine.Vector2;
using UVector3 = UnityEngine.Vector3;



using UMaterial = UnityEngine.Material;

// No DB yet, just testing with com_sfx_ord_flame...
public class EffectsLoader : Loader {

    public static EffectsLoader Instance { get; private set; } = null;
    static EffectsLoader()
    {
        Instance = new EffectsLoader();
    }


    public void ImportEffects(string[] names)
    {
        foreach (string name in names)
        {
            ImportEffect(name);
        }
    }


    public GameObject ImportEffect(string name)
    {
        Config fx = container.FindConfig(ConfigType.Effect, name);

        if (fx == null)
        { 
            Debug.LogWarningFormat("Effect {0} not found among loaded levels...", name);
            return null;
        }

        GameObject psObj = null;

        try 
        {
            psObj = ImportEffect(fx);
        }
        finally 
        {
            if (psObj != null) { psObj.name = name; }
        }

        return psObj;
    }


    // Creates a root empty for the effect and attaches top-level emitters (see UnpackNestedEmitters)
    // to child gameobjects.
    public GameObject ImportEffect(Config fxConfig)
    {
        GameObject fxObject = new GameObject(String.Format("0x{0:x}", fxConfig.name));

        foreach (Field emitter in UnpackNestedEmitters(fxConfig))
        {
            GameObject emitterObj = GetEmitter(emitter);
            if (emitterObj != null)
            {
                emitterObj.transform.parent = fxObject.transform;
            }
        }        

        return fxObject;
    }


    // Returns a new GameObject with the input emitter converted to a particle system and attached.
    private GameObject GetEmitter(Field emitter)
    {
        GameObject fxObject = new GameObject(emitter.GetString());

        ParticleSystem uEmitter = fxObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psR = fxObject.GetComponent<ParticleSystemRenderer>();

        Scope scEmitter = emitter.scope;
        
        var mainModule = uEmitter.main;

        float numParticles = scEmitter.GetVec2("MaxParticles").Y;

        var em = uEmitter.emission;
        em.enabled = true;
        em.rateOverTime = (numParticles == -1.0f ? 50.0f : numParticles);

        Scope scTransformer = scEmitter.GetField("Transformer").scope;
        Scope scSpawner = scEmitter.GetField("Spawner").scope;
        Scope scGeometry = scEmitter.GetField("Geometry").scope;


        Scope lSpread = null;
        try 
        {
            lSpread = scSpawner.GetField("Spread").scope;
        }
        catch 
        {
            GameObject.DestroyImmediate(fxObject);
            return null;
        }
        
        // Set starting position distribution
        var shapeModule = uEmitter.shape;
        shapeModule.shapeType = ParticleSystemShapeType.Box;
        SpreadToPositionAndScale(scSpawner, out UVector3 spreadScale, out UVector3 spreadPos);
        shapeModule.scale = spreadScale;
        shapeModule.position = spreadPos; 

        // Set starting velocity distribution
        var curves = SpreadToVelocityIntervals(scSpawner, out ParticleSystem.MinMaxCurve scaleCurve);
        var velModule = uEmitter.velocityOverLifetime;
        velModule.enabled = true;
        velModule.x = curves[0];
        velModule.y = curves[1];
        velModule.z = curves[2];
        velModule.speedModifier = scaleCurve;
        
        float lifeTime = scTransformer.GetFloat("LifeTime");
        mainModule.startLifetime = lifeTime;
        mainModule.duration = lifeTime;
        mainModule.startSize = scSpawner.GetVec3("Size").Z;
        mainModule.startRotation = AngleCurveFromVec(scSpawner.GetVec3("StartRotation"));


        //mainModule.startColor = GetSpawnerColorInterval(scSpawner);


        var colModule = uEmitter.colorOverLifetime;
        colModule.enabled = true;
        colModule.color = ColorTransformationToGradient(scTransformer, GetSpawnerColorInterval(scSpawner));



        var rotModule = uEmitter.rotationOverLifetime;
        rotModule.enabled = true;
        rotModule.z = AngleCurveFromVec(scSpawner.GetVec3("RotationVelocity"));

        UMaterial mat = new UMaterial(Shader.Find("Particles/Standard Unlit"));
        mat.mainTexture = TextureLoader.Instance.ImportTexture(scGeometry.GetString("Texture"));

        // Need to find a way of doing this without triggering the annoying GUI reset!
        // Ideally without editing out the shader's GUI ref...
        string mode = scGeometry.GetString("BlendMode");
        if (mode == "ADDITIVE")
        {
            mat.SetFloat("_Mode", 4.0f);
        }
        else 
        {
            mat.SetFloat("_Mode", 2.0f);
        }

        psR.sharedMaterial = mat;

        return fxObject;
    }


    // Get spawner's starting color interval
    private ParticleSystem.MinMaxGradient GetSpawnerColorInterval(Scope spawner)
    {
        Color minCol, maxCol;

        // Test if provided as HSV or RGB...
        if (spawner.GetField("Hue") != null)
        {
            var hRange = spawner.GetVec3("Hue");
            var sRange = spawner.GetVec3("Saturation");
            var vRange = spawner.GetVec3("Value");

            minCol = Color.HSVToRGB(hRange.Y/255.0f, sRange.Y/255.0f, vRange.Y/255.0f);
            maxCol = Color.HSVToRGB(hRange.Z/255.0f, sRange.Z/255.0f, vRange.Z/255.0f);
        }
        else
        {
            var rRange = spawner.GetVec3("Red");
            var gRange = spawner.GetVec3("Green");
            var bRange = spawner.GetVec3("Blue");

            minCol = new Color(rRange.Y/255.0f, gRange.Y/255.0f, bRange.Y/255.0f);
            maxCol = new Color(rRange.Z/255.0f, gRange.Z/255.0f, bRange.Z/255.0f);
        }

        var aRange = spawner.GetVec3("Alpha");
        minCol.a = aRange.Y/255.0f;
        maxCol.a = aRange.Z/255.0f;

        return new ParticleSystem.MinMaxGradient(minCol, maxCol);
    }



    // Get spawner's starting position properties as a box's scale + position
    private void SpreadToPositionAndScale(Scope spawner, out UVector3 scale, out UVector3 position)
    {
        Scope spreadScope = spawner.GetField("Spread").scope;
        Scope offsetScope = spawner.GetField("Offset").scope;

        var intervalX = offsetScope.GetVec2("PositionX");
        var intervalY = offsetScope.GetVec2("PositionY");
        var intervalZ = offsetScope.GetVec2("PositionZ");

        float convScale = 1.0f;
        float scaleX = Math.Abs(intervalX.Y - intervalX.X) / convScale;
        float scaleY = Math.Abs(intervalY.Y - intervalY.X) / convScale;
        float scaleZ = Math.Abs(intervalZ.Y - intervalZ.X) / convScale;

        float posX = (intervalX.Y + intervalX.X) / 2.0f;
        float posY = (intervalY.Y + intervalY.X) / 2.0f;
        float posZ = (intervalZ.Y + intervalZ.X) / 2.0f;

        scale = new UVector3(scaleX, scaleY, scaleZ);
        position = new UVector3(posX, posY, posZ);
    }


    // Get spawner's starting velocity distribution as list of component intervals.  
    // The out param scaleCurve is the "VelocityScale" property 
    private List<ParticleSystem.MinMaxCurve> SpreadToVelocityIntervals(Scope spawner, out ParticleSystem.MinMaxCurve scaleCurve) 
    {
        Scope spreadScope = spawner.GetField("Spread").scope;
        var vX = spreadScope.GetVec2("PositionX");
        var vY = spreadScope.GetVec2("PositionY");
        var vZ = spreadScope.GetVec2("PositionZ");

        var velScale = spawner.GetVec2("VelocityScale");
        scaleCurve = new ParticleSystem.MinMaxCurve(velScale.X, velScale.Y);

        var curveX = new ParticleSystem.MinMaxCurve(vX.X,vX.Y);
        var curveY = new ParticleSystem.MinMaxCurve(vY.X,vY.Y);
        var curveZ = new ParticleSystem.MinMaxCurve(vZ.X,vZ.Y);

        return new List<ParticleSystem.MinMaxCurve>(){curveX, curveY, curveZ};
    }


    // Fx files/configs/chunks have subsequent emitters as children.  This function just 
    // recursively unpacks and returns the emitters in a list.  If an emitter is a proper
    // child/subemitter, it will be in the "Geometry" scope.
    private List<Field> UnpackNestedEmitters(Config fxConfig)
    {
        List<Field> emitters = new List<Field>();

        Field curEmitter = fxConfig.GetField("ParticleEmitter");

        while (curEmitter != null)
        {
            emitters.Add(curEmitter);

            Field nextEmitter = curEmitter.scope.GetField("ParticleEmitter");
            if (nextEmitter != null)
            {
                curEmitter = nextEmitter;
            }
            else 
            {
                break;
            }
        }

        return emitters;
    }



    private ParticleSystem.MinMaxCurve CurveFromVec(LibVector2 minMax)
    {
        return new ParticleSystem.MinMaxCurve(minMax.X, minMax.Y);
    }

    private ParticleSystem.MinMaxCurve AngleCurveFromVec(LibVector3 minMax)
    {
        return new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad * minMax.Y, Mathf.Deg2Rad * minMax.Z);
    }



    public ParticleSystem.MinMaxGradient ColorTransformationToGradient(Scope transformerScope, ParticleSystem.MinMaxGradient initialGrad)
    {
        Gradient gradMin = new Gradient();
        Gradient gradMax = new Gradient();

        List<GradientColorKey> gradMinColKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> gradMinAlphaKeys = new List<GradientAlphaKey>();

        List<GradientColorKey> gradMaxColKeys = new List<GradientColorKey>();
        List<GradientAlphaKey> gradMaxAlphaKeys = new List<GradientAlphaKey>();

        Color minColLast = initialGrad.colorMin; //initialGrad.gradientMin.colorKeys[0].color;
        Color maxColLast = initialGrad.colorMax; //initialGrad.gradientMax.colorKeys[0].color;

        float minAlphaLast = minColLast.a; //initialGrad.gradientMin.alphaKeys[0].alpha;
        float maxAlphaLast = maxColLast.a; //initialGrad.gradientMax.alphaKeys[0].alpha;


        float lifeTime = transformerScope.GetFloat("LifeTime");
        float timeIndex = 0.0f;

        gradMinColKeys.Add(new GradientColorKey(minColLast, timeIndex));
        gradMaxColKeys.Add(new GradientColorKey(maxColLast, timeIndex));

        gradMinAlphaKeys.Add(new GradientAlphaKey(minAlphaLast, timeIndex));
        gradMaxAlphaKeys.Add(new GradientAlphaKey(maxAlphaLast, timeIndex));

        Field curKey = transformerScope.GetField("Color");
        while (curKey != null)
        {
            Scope scKey = curKey.scope;

            timeIndex = scKey.GetFloat("LifeTime") / lifeTime;

            Field keyMove, keyReach;
            if ((keyMove = scKey.GetField("Move")) != null)
            {
                var c = keyMove.GetVec4();
                Color col = new Color(c.X / 255.0f, c.Y / 255.0f, c.Z / 255.0f, 1.0f);

                minColLast += col;
                maxColLast += col;

                minAlphaLast = Mathf.Clamp(minAlphaLast + c.W / 255.0f, 0.0f, 1.0f);
                maxAlphaLast = Mathf.Clamp(maxAlphaLast + c.W / 255.0f, 0.0f, 1.0f);
            }
            else if ((keyReach = scKey.GetField("Reach")) != null)
            {
                var c = keyReach.GetVec4(); 
                Color col = new Color(c.X / 255.0f, c.Y / 255.0f, c.Z / 255.0f, 1.0f);

                minColLast = col;
                maxColLast = col;

                minAlphaLast = Mathf.Clamp(c.W / 255.0f, 0.0f, 1.0f);
                maxAlphaLast = Mathf.Clamp(c.W / 255.0f, 0.0f, 1.0f);
            }
            else 
            {
                return new ParticleSystem.MinMaxGradient(gradMin, gradMax);
            }

            gradMinColKeys.Add(new GradientColorKey(minColLast, timeIndex));
            gradMaxColKeys.Add(new GradientColorKey(maxColLast, timeIndex));

            gradMinAlphaKeys.Add(new GradientAlphaKey(minAlphaLast, timeIndex));
            gradMaxAlphaKeys.Add(new GradientAlphaKey(maxAlphaLast, timeIndex));

            curKey = scKey.GetField("Next");
        }

        gradMin.SetKeys(gradMinColKeys.ToArray(), gradMinAlphaKeys.ToArray());
        gradMax.SetKeys(gradMaxColKeys.ToArray(), gradMaxAlphaKeys.ToArray());

        return new ParticleSystem.MinMaxGradient(gradMin, gradMax);
    }
}

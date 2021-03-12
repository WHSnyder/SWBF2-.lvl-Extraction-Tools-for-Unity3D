using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class TextureLoader : Loader {

    public static TextureLoader Instance { get; private set; } = null;

    private Dictionary<string, Texture2D> texDataBase = new Dictionary<string, Texture2D>();


    static TextureLoader()
    {
        Instance = new TextureLoader();
    }


    public void ResetDB()
    {
        texDataBase.Clear();
    }

    public Texture2D ImportTexture(string name, bool mirror = false) 
    {
        string texPath = SaveDirectory + "/" + name + ".png";

        if (texDataBase.ContainsKey(name))
        {
            return texDataBase[name];
        }

        var tex = container.Get<LibSWBF2.Wrappers.Texture>(name);

        if (tex != null && tex.Height * tex.Width > 0)
        {
            Texture2D newTexture = new Texture2D(tex.Width, tex.Height, TextureFormat.RGBA32, false);
            newTexture.name = tex.Name;

            byte[] data = tex.GetBytesRGBA();
            data = mirror ? MirrorVertically(data, tex.Width, tex.Height, 4) : data;

            newTexture.LoadRawTextureData(data);
            newTexture.Apply();

            if (SaveAssets)
            {
                File.WriteAllBytes(texPath, newTexture.EncodeToPNG());
                // TODO: figure out how to save texture assets in an AssetEditing block without
                // lost refs after save/light bake...
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.Default);
                newTexture = (Texture2D) AssetDatabase.LoadAssetAtPath(texPath, typeof(Texture2D));
            }

            texDataBase[name] = newTexture;
            return newTexture;
        }
        else 
        {
            Debug.LogWarningFormat("Texture: {0} failed to load!", name);
            return null;
        }
    }


    static byte[] MirrorVertically(byte[] data, int width, int height, int stride)
    {
        int byteWidth = width * stride;
        byte[] mirrored = new byte[data.Length];
        for (int rowIdx = 0; rowIdx < height; ++rowIdx)
        {
            int rowReverseIdx = height - rowIdx - 1;
            Array.Copy(data, rowReverseIdx * byteWidth, mirrored, rowIdx * byteWidth, byteWidth);
        }
        return mirrored;
    }
}

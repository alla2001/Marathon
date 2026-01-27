using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates procedural textures for the AmazingWater shader
/// Attach to any GameObject and click the buttons in the inspector
/// </summary>
public class WaterTextureGenerator : MonoBehaviour
{
    [Header("Caustics Settings")]
    public int causticsSize = 512;
    public float causticsScale = 4f;
    public int causticsOctaves = 3;

    [Header("Foam Noise Settings")]
    public int foamSize = 512;
    public float foamScale = 8f;
    public int foamOctaves = 4;

    [Header("Normal Map Settings")]
    public int normalSize = 512;
    public float normalScale = 4f;
    public float normalStrength = 1f;

    [Header("Output")]
    public Texture2D generatedCaustics;
    public Texture2D generatedFoamNoise;
    public Texture2D generatedNormalMap;

    // Simplex noise implementation
    private static int[] perm = {
        151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,
        8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,
        35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,74,165,71,
        134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
        55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,
        18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,
        250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,
        189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
        172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,
        228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,
        107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,45,127,4,150,254,
        138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
    };

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 7;
        float u = h < 4 ? x : y;
        float v = h < 4 ? y : x;
        return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -2f * v : 2f * v);
    }

    private static float SimplexNoise(float x, float y)
    {
        const float F2 = 0.366025403f;
        const float G2 = 0.211324865f;

        float s = (x + y) * F2;
        float xs = x + s;
        float ys = y + s;
        int i = Mathf.FloorToInt(xs);
        int j = Mathf.FloorToInt(ys);

        float t = (i + j) * G2;
        float X0 = i - t;
        float Y0 = j - t;
        float x0 = x - X0;
        float y0 = y - Y0;

        int i1, j1;
        if (x0 > y0) { i1 = 1; j1 = 0; }
        else { i1 = 0; j1 = 1; }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1f + 2f * G2;
        float y2 = y0 - 1f + 2f * G2;

        int ii = i & 255;
        int jj = j & 255;

        float n0 = 0, n1 = 0, n2 = 0;

        float t0 = 0.5f - x0 * x0 - y0 * y0;
        if (t0 >= 0)
        {
            t0 *= t0;
            n0 = t0 * t0 * Grad(perm[(ii + perm[jj & 255]) & 255], x0, y0);
        }

        float t1 = 0.5f - x1 * x1 - y1 * y1;
        if (t1 >= 0)
        {
            t1 *= t1;
            n1 = t1 * t1 * Grad(perm[(ii + i1 + perm[(jj + j1) & 255]) & 255], x1, y1);
        }

        float t2 = 0.5f - x2 * x2 - y2 * y2;
        if (t2 >= 0)
        {
            t2 *= t2;
            n2 = t2 * t2 * Grad(perm[(ii + 1 + perm[(jj + 1) & 255]) & 255], x2, y2);
        }

        return 40f * (n0 + n1 + n2);
    }

    private float FBM(float x, float y, float scale, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += SimplexNoise(x * frequency * scale, y * frequency * scale) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return value / maxValue;
    }

    private float Voronoi(float x, float y, float scale)
    {
        x *= scale;
        y *= scale;

        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);
        float fx = x - ix;
        float fy = y - iy;

        float minDist = 1f;

        for (int j = -1; j <= 1; j++)
        {
            for (int i = -1; i <= 1; i++)
            {
                int cellX = ix + i;
                int cellY = iy + j;

                // Hash to get random point in cell
                float h = perm[(cellX & 255 + perm[cellY & 255]) & 255] / 255f;
                float h2 = perm[(cellX + 57 & 255 + perm[cellY + 73 & 255]) & 255] / 255f;

                float px = i + h - fx;
                float py = j + h2 - fy;
                float d = px * px + py * py;
                minDist = Mathf.Min(minDist, d);
            }
        }

        return Mathf.Sqrt(minDist);
    }

    public Texture2D GenerateCausticsTexture()
    {
        Texture2D tex = new Texture2D(causticsSize, causticsSize, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[causticsSize * causticsSize];

        for (int y = 0; y < causticsSize; y++)
        {
            for (int x = 0; x < causticsSize; x++)
            {
                float u = (float)x / causticsSize;
                float v = (float)y / causticsSize;

                // Create caustics pattern using voronoi
                float c1 = Voronoi(u, v, causticsScale);
                float c2 = Voronoi(u + 0.5f, v + 0.5f, causticsScale * 1.5f);

                // Combine and process
                float caustic = c1 * c2;
                caustic = 1f - caustic;
                caustic = Mathf.Pow(caustic, 3f);
                caustic = Mathf.Clamp01(caustic);

                // Add some noise variation
                float noise = (FBM(u, v, causticsScale * 2, causticsOctaves) + 1f) * 0.5f;
                caustic *= noise;

                pixels[y * causticsSize + x] = new Color(caustic, caustic, caustic, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        generatedCaustics = tex;
        return tex;
    }

    public Texture2D GenerateFoamNoiseTexture()
    {
        Texture2D tex = new Texture2D(foamSize, foamSize, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[foamSize * foamSize];

        for (int y = 0; y < foamSize; y++)
        {
            for (int x = 0; x < foamSize; x++)
            {
                float u = (float)x / foamSize;
                float v = (float)y / foamSize;

                // FBM noise for foam
                float noise = (FBM(u, v, foamScale, foamOctaves) + 1f) * 0.5f;

                // Add some cellular variation
                float cellular = Voronoi(u, v, foamScale * 0.5f);
                cellular = 1f - Mathf.Pow(cellular, 2f);

                float foam = noise * 0.7f + cellular * 0.3f;
                foam = Mathf.Clamp01(foam);

                pixels[y * foamSize + x] = new Color(foam, foam, foam, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        generatedFoamNoise = tex;
        return tex;
    }

    public Texture2D GenerateNormalMap()
    {
        Texture2D tex = new Texture2D(normalSize, normalSize, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[normalSize * normalSize];

        // First pass: generate height map
        float[] heights = new float[normalSize * normalSize];
        for (int y = 0; y < normalSize; y++)
        {
            for (int x = 0; x < normalSize; x++)
            {
                float u = (float)x / normalSize;
                float v = (float)y / normalSize;
                heights[y * normalSize + x] = (FBM(u, v, normalScale, 4) + 1f) * 0.5f;
            }
        }

        // Second pass: calculate normals
        for (int y = 0; y < normalSize; y++)
        {
            for (int x = 0; x < normalSize; x++)
            {
                int x1 = (x - 1 + normalSize) % normalSize;
                int x2 = (x + 1) % normalSize;
                int y1 = (y - 1 + normalSize) % normalSize;
                int y2 = (y + 1) % normalSize;

                float dX = heights[y * normalSize + x2] - heights[y * normalSize + x1];
                float dY = heights[y2 * normalSize + x] - heights[y1 * normalSize + x];

                Vector3 normal = new Vector3(-dX * normalStrength, -dY * normalStrength, 1f);
                normal.Normalize();

                // Convert to 0-1 range
                pixels[y * normalSize + x] = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f
                );
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        generatedNormalMap = tex;
        return tex;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate All Textures")]
    public void GenerateAllTextures()
    {
        GenerateCausticsTexture();
        GenerateFoamNoiseTexture();
        GenerateNormalMap();
        Debug.Log("All water textures generated!");
    }

    [ContextMenu("Save Textures to Assets")]
    public void SaveTexturesToAssets()
    {
        string path = "Assets/Shaders/GeneratedTextures/";

        if (!AssetDatabase.IsValidFolder("Assets/Shaders/GeneratedTextures"))
        {
            AssetDatabase.CreateFolder("Assets/Shaders", "GeneratedTextures");
        }

        if (generatedCaustics != null)
        {
            byte[] bytes = generatedCaustics.EncodeToPNG();
            System.IO.File.WriteAllBytes(path + "WaterCaustics.png", bytes);
        }

        if (generatedFoamNoise != null)
        {
            byte[] bytes = generatedFoamNoise.EncodeToPNG();
            System.IO.File.WriteAllBytes(path + "WaterFoamNoise.png", bytes);
        }

        if (generatedNormalMap != null)
        {
            byte[] bytes = generatedNormalMap.EncodeToPNG();
            System.IO.File.WriteAllBytes(path + "WaterNormal.png", bytes);
        }

        AssetDatabase.Refresh();
        Debug.Log($"Textures saved to {path}");
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(WaterTextureGenerator))]
public class WaterTextureGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WaterTextureGenerator generator = (WaterTextureGenerator)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Generate Textures", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Caustics", GUILayout.Height(30)))
        {
            generator.GenerateCausticsTexture();
        }

        if (GUILayout.Button("Generate Foam Noise", GUILayout.Height(30)))
        {
            generator.GenerateFoamNoiseTexture();
        }

        if (GUILayout.Button("Generate Normal Map", GUILayout.Height(30)))
        {
            generator.GenerateNormalMap();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Generate All Textures", GUILayout.Height(40)))
        {
            generator.GenerateAllTextures();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Save Textures to Assets", GUILayout.Height(30)))
        {
            generator.SaveTexturesToAssets();
        }
    }
}
#endif

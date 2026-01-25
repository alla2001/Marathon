using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class MaterialBaker : EditorWindow
{
    private GameObject targetObject;
    private int atlasSize = 4096;
    private int padding = 4; // Increased padding to reduce bleeding
    private int minTextureSize = 64; // Minimum size for each texture in atlas
    private string outputFolder = "Assets/BakedMaterials";
    private bool bakeAlbedo = true;
    private bool bakeNormal = true;
    private bool bakeMetallic = true;
    private bool bakeEmission = false;
    private bool makeTexturesReadable = true;

    [MenuItem("Tools/Material Baker")]
    public static void ShowWindow()
    {
        GetWindow<MaterialBaker>("Material Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Material Baker - Combine Multiple Materials", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("Atlas Settings", EditorStyles.boldLabel);
        atlasSize = EditorGUILayout.IntPopup("Atlas Size", atlasSize,
            new string[] { "1024", "2048", "4096", "8192" },
            new int[] { 1024, 2048, 4096, 8192 });
        padding = EditorGUILayout.IntSlider("Padding", padding, 0, 16);
        minTextureSize = EditorGUILayout.IntPopup("Min Texture Size", minTextureSize,
            new string[] { "32", "64", "128", "256" },
            new int[] { 32, 64, 128, 256 });
        makeTexturesReadable = EditorGUILayout.Toggle("Auto-fix Read/Write", makeTexturesReadable);

        EditorGUILayout.Space();
        GUILayout.Label("Textures to Bake", EditorStyles.boldLabel);
        bakeAlbedo = EditorGUILayout.Toggle("Albedo (Main Texture)", bakeAlbedo);
        bakeNormal = EditorGUILayout.Toggle("Normal Map", bakeNormal);
        bakeMetallic = EditorGUILayout.Toggle("Metallic/Smoothness", bakeMetallic);
        bakeEmission = EditorGUILayout.Toggle("Emission", bakeEmission);

        EditorGUILayout.Space();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();

        if (targetObject != null)
        {
            var renderers = targetObject.GetComponentsInChildren<MeshRenderer>();
            var skinnedRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            int totalMaterials = 0;
            foreach (var r in renderers) totalMaterials += r.sharedMaterials.Length;
            foreach (var r in skinnedRenderers) totalMaterials += r.sharedMaterials.Length;

            EditorGUILayout.HelpBox($"Found {renderers.Length + skinnedRenderers.Length} renderer(s) with {totalMaterials} total material(s)", MessageType.Info);

            // Calculate approximate texture size per material
            int approxTexturesPerRow = Mathf.FloorToInt(Mathf.Sqrt(totalMaterials));
            int approxTexSize = atlasSize / Mathf.Max(approxTexturesPerRow, 1);

            if (totalMaterials > 64)
            {
                EditorGUILayout.HelpBox(
                    $"WARNING: {totalMaterials} materials is a lot!\n" +
                    $"Each texture will be approximately {approxTexSize}x{approxTexSize} pixels.\n" +
                    $"Consider using 8192 atlas size or baking in batches.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.Space();

        GUI.enabled = targetObject != null;
        if (GUILayout.Button("Bake Materials", GUILayout.Height(40)))
        {
            BakeMaterials();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This tool will:\n" +
            "1. Collect all materials from the object\n" +
            "2. Create texture atlases\n" +
            "3. Remap UVs to the atlas\n" +
            "4. Create a new combined mesh and material\n\n" +
            "A new GameObject will be created with the baked result.",
            MessageType.Info);
    }

    private void BakeMaterials()
    {
        if (targetObject == null)
        {
            Debug.LogError("[MaterialBaker] No target object selected!");
            return;
        }

        // Create output folder
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string[] folders = outputFolder.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }

        string baseName = targetObject.name + "_Baked";

        // Collect all mesh filters and their materials
        var meshDataList = new List<MeshMaterialData>();

        // Handle MeshRenderer
        var meshFilters = targetObject.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in meshFilters)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null && mf.sharedMesh != null)
            {
                meshDataList.Add(new MeshMaterialData
                {
                    mesh = mf.sharedMesh,
                    materials = mr.sharedMaterials,
                    transform = mf.transform
                });
            }
        }

        // Handle SkinnedMeshRenderer
        var skinnedRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var smr in skinnedRenderers)
        {
            if (smr.sharedMesh != null)
            {
                meshDataList.Add(new MeshMaterialData
                {
                    mesh = smr.sharedMesh,
                    materials = smr.sharedMaterials,
                    transform = smr.transform,
                    isSkinned = true
                });
            }
        }

        if (meshDataList.Count == 0)
        {
            Debug.LogError("[MaterialBaker] No meshes found on target object!");
            return;
        }

        // Collect unique materials
        var uniqueMaterials = new List<Material>();
        foreach (var data in meshDataList)
        {
            foreach (var mat in data.materials)
            {
                if (mat != null && !uniqueMaterials.Contains(mat))
                {
                    uniqueMaterials.Add(mat);
                }
            }
        }

        Debug.Log($"[MaterialBaker] Found {uniqueMaterials.Count} unique materials");

        // Create texture atlases
        var atlasData = CreateTextureAtlas(uniqueMaterials, baseName);
        if (atlasData == null)
        {
            Debug.LogError("[MaterialBaker] Failed to create texture atlas!");
            return;
        }

        // Create combined material
        Material combinedMaterial = CreateCombinedMaterial(atlasData, baseName);

        // Create new mesh with remapped UVs
        GameObject bakedObject = new GameObject(baseName);
        bakedObject.transform.position = targetObject.transform.position;
        bakedObject.transform.rotation = targetObject.transform.rotation;
        bakedObject.transform.localScale = targetObject.transform.localScale;

        // Combine meshes
        CombineMeshes(meshDataList, uniqueMaterials, atlasData, bakedObject, combinedMaterial);

        // Select the new object
        Selection.activeGameObject = bakedObject;

        Debug.Log($"[MaterialBaker] Successfully baked {uniqueMaterials.Count} materials into 1 material!");
        Debug.Log($"[MaterialBaker] Output saved to: {outputFolder}");
    }

    private AtlasData CreateTextureAtlas(List<Material> materials, string baseName)
    {
        var atlasData = new AtlasData();
        atlasData.uvRects = new Dictionary<Material, Rect>();

        // Collect textures
        var albedoTextures = new List<Texture2D>();
        var normalTextures = new List<Texture2D>();
        var metallicTextures = new List<Texture2D>();
        var emissionTextures = new List<Texture2D>();

        foreach (var mat in materials)
        {
            Debug.Log($"[MaterialBaker] Processing material: {mat.name} (Shader: {mat.shader.name})");

            // Albedo - try multiple property names
            Texture2D albedoTex = null;
            Color albedoColor = Color.white;

            // Try different albedo property names (including 3ds Max Physical Material)
            if (mat.HasProperty("_BASE_COLOR_MAP"))
            {
                // 3ds Max Physical Material shader
                albedoTex = mat.GetTexture("_BASE_COLOR_MAP") as Texture2D;
                if (mat.HasProperty("_BASE_COLOR"))
                    albedoColor = mat.GetColor("_BASE_COLOR");
            }
            else if (mat.HasProperty("_BaseMap"))
            {
                albedoTex = mat.GetTexture("_BaseMap") as Texture2D;
                if (mat.HasProperty("_BaseColor"))
                    albedoColor = mat.GetColor("_BaseColor");
            }
            else if (mat.HasProperty("_MainTex"))
            {
                albedoTex = mat.GetTexture("_MainTex") as Texture2D;
                if (mat.HasProperty("_Color"))
                    albedoColor = mat.GetColor("_Color");
            }

            Debug.Log($"[MaterialBaker] - Albedo: {(albedoTex != null ? albedoTex.name : "NULL")} Color: {albedoColor}");

            Texture2D albedo = GetReadableTexture(albedoTex, albedoColor);
            albedoTextures.Add(albedo);

            // Normal - try multiple property names (including 3ds Max Physical Material)
            if (bakeNormal)
            {
                Texture2D normalTex = null;
                if (mat.HasProperty("_BUMP_MAP"))
                    normalTex = mat.GetTexture("_BUMP_MAP") as Texture2D;
                else if (mat.HasProperty("_BumpMap"))
                    normalTex = mat.GetTexture("_BumpMap") as Texture2D;
                else if (mat.HasProperty("_NormalMap"))
                    normalTex = mat.GetTexture("_NormalMap") as Texture2D;

                Texture2D normal = GetReadableTexture(normalTex, Color.white, true);
                normalTextures.Add(normal);
            }

            // Metallic - try multiple property names (including 3ds Max Physical Material)
            if (bakeMetallic)
            {
                Texture2D metallicTex = null;
                if (mat.HasProperty("_METALNESS_MAP"))
                    metallicTex = mat.GetTexture("_METALNESS_MAP") as Texture2D;
                else if (mat.HasProperty("_MetallicGlossMap"))
                    metallicTex = mat.GetTexture("_MetallicGlossMap") as Texture2D;
                else if (mat.HasProperty("_MetallicSmoothnessMap"))
                    metallicTex = mat.GetTexture("_MetallicSmoothnessMap") as Texture2D;

                Texture2D metallic = GetReadableTexture(metallicTex, Color.white);
                metallicTextures.Add(metallic);
            }

            // Emission (including 3ds Max Physical Material)
            if (bakeEmission)
            {
                Texture2D emissionTex = null;
                Color emissionColor = Color.black;

                if (mat.HasProperty("_EMISSION_COLOR_MAP"))
                {
                    emissionTex = mat.GetTexture("_EMISSION_COLOR_MAP") as Texture2D;
                    if (mat.HasProperty("_EMISSION_COLOR"))
                        emissionColor = mat.GetColor("_EMISSION_COLOR");
                }
                else if (mat.HasProperty("_EmissionMap"))
                    emissionTex = mat.GetTexture("_EmissionMap") as Texture2D;

                if (mat.HasProperty("_EmissionColor") && emissionColor == Color.black)
                    emissionColor = mat.GetColor("_EmissionColor");

                Texture2D emission = GetReadableTexture(emissionTex, emissionColor);
                emissionTextures.Add(emission);
            }
        }

        // Pack albedo atlas
        if (bakeAlbedo && albedoTextures.Count > 0)
        {
            Debug.Log($"[MaterialBaker] Packing {albedoTextures.Count} albedo textures into atlas...");

            atlasData.albedoAtlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
            Rect[] rects = atlasData.albedoAtlas.PackTextures(albedoTextures.ToArray(), padding, atlasSize, false);

            if (rects == null || rects.Length == 0)
            {
                Debug.LogError("[MaterialBaker] Failed to pack albedo textures!");
                return null;
            }

            for (int i = 0; i < materials.Count; i++)
            {
                atlasData.uvRects[materials[i]] = rects[i];
                Debug.Log($"[MaterialBaker] Material '{materials[i].name}' UV rect: {rects[i]}");
            }

            SaveTexture(atlasData.albedoAtlas, baseName + "_Albedo");
            Debug.Log($"[MaterialBaker] Albedo atlas saved: {atlasSize}x{atlasSize}");
        }

        // Pack normal atlas (use linear color space for normal maps)
        if (bakeNormal && normalTextures.Count > 0 && normalTextures.Count == materials.Count)
        {
            atlasData.normalAtlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true, true); // linear = true
            atlasData.normalAtlas.PackTextures(normalTextures.ToArray(), padding, atlasSize);
            SaveTexture(atlasData.normalAtlas, baseName + "_Normal", true);
        }

        // Pack metallic atlas
        if (bakeMetallic && metallicTextures.Count > 0 && metallicTextures.Count == materials.Count)
        {
            atlasData.metallicAtlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
            atlasData.metallicAtlas.PackTextures(metallicTextures.ToArray(), padding, atlasSize);
            SaveTexture(atlasData.metallicAtlas, baseName + "_Metallic");
        }

        // Pack emission atlas
        if (bakeEmission && emissionTextures.Count > 0 && emissionTextures.Count == materials.Count)
        {
            atlasData.emissionAtlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
            atlasData.emissionAtlas.PackTextures(emissionTextures.ToArray(), padding, atlasSize);
            SaveTexture(atlasData.emissionAtlas, baseName + "_Emission");
        }

        // Cleanup temporary textures
        foreach (var tex in albedoTextures) if (tex != null) DestroyImmediate(tex);
        foreach (var tex in normalTextures) if (tex != null) DestroyImmediate(tex);
        foreach (var tex in metallicTextures) if (tex != null) DestroyImmediate(tex);
        foreach (var tex in emissionTextures) if (tex != null) DestroyImmediate(tex);

        return atlasData;
    }

    private Texture2D GetReadableTexture(Texture2D source, Color tintColor, bool isNormal = false)
    {
        int size = Mathf.Max(minTextureSize, 128); // Use minimum texture size setting

        if (source != null)
        {
            size = Mathf.Max(source.width, source.height, minTextureSize);
        }

        // Use linear color space for normal maps
        Texture2D readable = new Texture2D(size, size, TextureFormat.RGBA32, false, isNormal);

        if (source != null)
        {
            // Try to make source readable temporarily
            string assetPath = AssetDatabase.GetAssetPath(source);
            TextureImporter importer = null;
            bool wasReadable = true;

            if (makeTexturesReadable && !string.IsNullOrEmpty(assetPath))
            {
                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    wasReadable = importer.isReadable;
                    if (!wasReadable)
                    {
                        Debug.Log($"[MaterialBaker] Making texture readable: {source.name}");
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                        // Reload the texture after reimport
                        source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    }
                }
            }

            try
            {
                // Method 1: Try direct copy if readable
                if (source.isReadable)
                {
                    Color[] pixels = source.GetPixels();

                    // Resize if needed
                    if (source.width != size || source.height != size)
                    {
                        readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, isNormal);
                    }

                    readable.SetPixels(pixels);

                    // Apply tint
                    if (!isNormal && tintColor != Color.white)
                    {
                        pixels = readable.GetPixels();
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            pixels[i] *= tintColor;
                        }
                        readable.SetPixels(pixels);
                    }

                    readable.Apply();
                }
                else
                {
                    // Method 2: Use RenderTexture as fallback
                    readable = CopyTextureViaRenderTexture(source, tintColor, isNormal);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MaterialBaker] Failed to read texture {source.name}: {e.Message}. Using RenderTexture fallback.");
                readable = CopyTextureViaRenderTexture(source, tintColor, isNormal);
            }

            // Restore original readable state
            if (importer != null && !wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
        else
        {
            // No source texture - fill with solid color
            Color fillColor = isNormal ? new Color(0.5f, 0.5f, 1f, 1f) : tintColor;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = fillColor;
            }
            readable.SetPixels(pixels);
            readable.Apply();
        }

        return readable;
    }

    private Texture2D CopyTextureViaRenderTexture(Texture2D source, Color tintColor, bool isNormal)
    {
        int width = source.width;
        int height = source.height;

        // Use Linear color space for normal maps, sRGB for albedo
        RenderTextureReadWrite colorSpace = isNormal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;

        // Create a temporary RenderTexture
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, colorSpace);
        rt.filterMode = FilterMode.Bilinear;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        // Clear with appropriate color
        Color clearColor = isNormal ? new Color(0.5f, 0.5f, 1f, 1f) : Color.black;
        GL.Clear(true, true, clearColor);

        // Blit the source texture
        Graphics.Blit(source, rt);

        // Read pixels - use linear for normal maps
        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, isNormal);
        readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);

        // Apply tint for non-normal textures
        if (!isNormal && tintColor != Color.white)
        {
            Color[] pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= tintColor;
            }
            readable.SetPixels(pixels);
        }

        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return readable;
    }

    private void SaveTexture(Texture2D texture, string name, bool isNormal = false)
    {
        string path = outputFolder + "/" + name + ".png";
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

        // Set import settings
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = atlasSize;
            importer.SaveAndReimport();
        }
    }

    private Material CreateCombinedMaterial(AtlasData atlasData, string baseName)
    {
        // Try to use URP Lit shader, fallback to Standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.name = baseName + "_Material";

        // Load saved textures
        if (atlasData.albedoAtlas != null)
        {
            Texture2D savedAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(outputFolder + "/" + baseName + "_Albedo.png");
            if (savedAlbedo != null)
            {
                mat.mainTexture = savedAlbedo;
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", savedAlbedo);
            }
        }

        if (atlasData.normalAtlas != null)
        {
            Texture2D savedNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(outputFolder + "/" + baseName + "_Normal.png");
            if (savedNormal != null)
            {
                if (mat.HasProperty("_BumpMap"))
                    mat.SetTexture("_BumpMap", savedNormal);
                if (mat.HasProperty("_NormalMap"))
                    mat.SetTexture("_NormalMap", savedNormal);
            }
        }

        if (atlasData.metallicAtlas != null)
        {
            Texture2D savedMetallic = AssetDatabase.LoadAssetAtPath<Texture2D>(outputFolder + "/" + baseName + "_Metallic.png");
            if (savedMetallic != null)
            {
                if (mat.HasProperty("_MetallicGlossMap"))
                    mat.SetTexture("_MetallicGlossMap", savedMetallic);
                if (mat.HasProperty("_MetallicSmoothnessMap"))
                    mat.SetTexture("_MetallicSmoothnessMap", savedMetallic);
            }
        }

        if (atlasData.emissionAtlas != null)
        {
            Texture2D savedEmission = AssetDatabase.LoadAssetAtPath<Texture2D>(outputFolder + "/" + baseName + "_Emission.png");
            if (savedEmission != null)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionMap"))
                    mat.SetTexture("_EmissionMap", savedEmission);
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", Color.white);
            }
        }

        // Save material
        string matPath = outputFolder + "/" + baseName + "_Material.mat";
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Material>(matPath);
    }

    private void CombineMeshes(List<MeshMaterialData> meshDataList, List<Material> uniqueMaterials,
        AtlasData atlasData, GameObject bakedObject, Material combinedMaterial)
    {
        List<CombineInstance> combineInstances = new List<CombineInstance>();

        foreach (var data in meshDataList)
        {
            Mesh originalMesh = data.mesh;

            // For each submesh (material slot)
            for (int subMeshIndex = 0; subMeshIndex < originalMesh.subMeshCount; subMeshIndex++)
            {
                Material mat = subMeshIndex < data.materials.Length ? data.materials[subMeshIndex] : null;
                if (mat == null) continue;

                // Get UV rect for this material
                if (!atlasData.uvRects.TryGetValue(mat, out Rect uvRect))
                {
                    Debug.LogWarning($"[MaterialBaker] No UV rect found for material: {mat.name}");
                    continue;
                }

                // Create a mesh for this submesh with remapped UVs
                Mesh subMesh = ExtractSubmesh(originalMesh, subMeshIndex);
                RemapUVs(subMesh, uvRect);

                // Calculate world transform relative to target
                Matrix4x4 matrix = targetObject.transform.worldToLocalMatrix * data.transform.localToWorldMatrix;

                combineInstances.Add(new CombineInstance
                {
                    mesh = subMesh,
                    transform = matrix
                });
            }
        }

        if (combineInstances.Count == 0)
        {
            Debug.LogError("[MaterialBaker] No meshes to combine!");
            return;
        }

        // Combine all meshes
        Mesh combinedMesh = new Mesh();
        combinedMesh.name = bakedObject.name + "_Mesh";
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support large meshes
        combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

        // Recalculate bounds and normals
        combinedMesh.RecalculateBounds();
        combinedMesh.RecalculateNormals();
        combinedMesh.RecalculateTangents();

        // Save mesh
        string meshPath = outputFolder + "/" + combinedMesh.name + ".asset";
        AssetDatabase.CreateAsset(combinedMesh, meshPath);
        AssetDatabase.SaveAssets();

        // Load saved mesh
        Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        // Add components to baked object
        MeshFilter mf = bakedObject.AddComponent<MeshFilter>();
        mf.sharedMesh = savedMesh;

        MeshRenderer mr = bakedObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = combinedMaterial;

        // Cleanup temporary meshes
        foreach (var ci in combineInstances)
        {
            DestroyImmediate(ci.mesh);
        }

        Debug.Log($"[MaterialBaker] Combined {combineInstances.Count} submeshes into 1 mesh with {savedMesh.vertexCount} vertices");
    }

    private Mesh ExtractSubmesh(Mesh originalMesh, int submeshIndex)
    {
        // Get submesh triangles
        int[] triangles = originalMesh.GetTriangles(submeshIndex);

        // Find unique vertices used by this submesh
        HashSet<int> usedVertices = new HashSet<int>();
        foreach (int tri in triangles)
        {
            usedVertices.Add(tri);
        }

        // Create new mesh with only the used vertices
        Mesh newMesh = new Mesh();
        newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        // Copy all vertex data
        newMesh.vertices = originalMesh.vertices;
        newMesh.normals = originalMesh.normals;
        newMesh.tangents = originalMesh.tangents;
        newMesh.uv = originalMesh.uv;
        newMesh.uv2 = originalMesh.uv2;
        newMesh.colors = originalMesh.colors;

        // Set triangles
        newMesh.triangles = triangles;

        return newMesh;
    }

    private void RemapUVs(Mesh mesh, Rect atlasRect)
    {
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0)
        {
            // Generate default UVs if none exist
            uvs = new Vector2[mesh.vertexCount];
            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i] = Vector2.zero;
            }
        }

        // Add margin to prevent texture bleeding (shrink the rect slightly)
        float margin = 0.001f; // Small margin to prevent bleeding
        Rect safeRect = new Rect(
            atlasRect.x + atlasRect.width * margin,
            atlasRect.y + atlasRect.height * margin,
            atlasRect.width * (1f - 2f * margin),
            atlasRect.height * (1f - 2f * margin)
        );

        // Find UV bounds to handle tiled textures properly
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;

        for (int i = 0; i < uvs.Length; i++)
        {
            minU = Mathf.Min(minU, uvs[i].x);
            maxU = Mathf.Max(maxU, uvs[i].x);
            minV = Mathf.Min(minV, uvs[i].y);
            maxV = Mathf.Max(maxV, uvs[i].y);
        }

        float rangeU = maxU - minU;
        float rangeV = maxV - minV;

        // Avoid division by zero
        if (rangeU < 0.0001f) rangeU = 1f;
        if (rangeV < 0.0001f) rangeV = 1f;

        // Remap UVs to atlas rect, normalizing tiled UVs to 0-1 range first
        for (int i = 0; i < uvs.Length; i++)
        {
            // Normalize UV to 0-1 based on actual UV bounds
            float u = (uvs[i].x - minU) / rangeU;
            float v = (uvs[i].y - minV) / rangeV;

            // Clamp to prevent any overflow
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            // Map to atlas rect with safe margin
            uvs[i] = new Vector2(
                safeRect.x + u * safeRect.width,
                safeRect.y + v * safeRect.height
            );
        }

        mesh.uv = uvs;
    }

    private class MeshMaterialData
    {
        public Mesh mesh;
        public Material[] materials;
        public Transform transform;
        public bool isSkinned;
    }

    private class AtlasData
    {
        public Texture2D albedoAtlas;
        public Texture2D normalAtlas;
        public Texture2D metallicAtlas;
        public Texture2D emissionAtlas;
        public Dictionary<Material, Rect> uvRects;
    }
}

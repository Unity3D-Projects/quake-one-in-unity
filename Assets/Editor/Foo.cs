﻿using UnityEngine;
using UnityEditor;

using System.IO;
using System.Collections.Generic;

public static class Foo
{
    [MenuItem("Test/Load BSP")]
    static void LoadBSP()
    {
        using (FileStream stream = File.OpenRead(Path.Combine(Application.dataPath, "e1m1.bsp")))
        {
            DataStream ds = new DataStream(stream);
            BSP bsp = new BSP(ds);

            var materials = GenerateMaterials(bsp);
            GenerateLevel(bsp, materials);
        }
    }

    static IList<Material> GenerateMaterials(BSP bsp)
    {
        string textureDir = Directory.GetParent(Application.dataPath).ToString();

        List<string> textures = new List<string>();
        List<Material> materials = new List<Material>();

        int tex_id = 0;
        foreach (var t in bsp.textures)
        {
            Texture2D tex = new Texture2D(t.width, t.height);
            Color32[] pixels = new Color32[t.width * t.height];
            for (int i = 0, j = 0; i < pixels.Length; ++i)
            {
                pixels[i] = new Color32(t.data[j++], t.data[j++], t.data[j++], t.data[j++]);
            }

            for (int x = 0; x < t.width; ++x)
            {
                for (int y = 0; y < t.height / 2; ++y)
                {
                    int from = y * t.width + x;
                    int to = (t.height - y - 1) * t.width + x;
                    var temp = pixels[to];
                    pixels[to] = pixels[from];
                    pixels[from] = temp;
                }
            }

            string texturePath = string.Format("Assets/Textures/[{0}] {1}.png", tex_id++, FileUtil.FixFilename(t.name));
            tex.SetPixels32(pixels);
            File.WriteAllBytes(Path.Combine(textureDir, texturePath), tex.EncodeToPNG());

            textures.Add(texturePath);
        }
        AssetDatabase.Refresh();

        foreach (var texture in textures)
        {
            TextureImporter importer = TextureImporter.GetAtPath(texture) as TextureImporter;
            importer.textureType = TextureImporterType.Image;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = 2048;
            importer.textureFormat = TextureImporterFormat.DXT1;
            importer.SaveAndReimport();

            var material = new Material(Shader.Find("Standard"));
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            material.SetFloat("_Glossiness", 0.0f);

            int index = texture.LastIndexOf('.');
            string materialPath = texture.Substring(0, index) + ".mat";
            AssetDatabase.CreateAsset(material, materialPath);

            materials.Add(AssetDatabase.LoadAssetAtPath<Material>(materialPath));
        }

        return materials;
    }

    static void GenerateLevel(BSP bsp, IList<Material> materials)
    {
        Level level = GameObject.FindObjectOfType<Level>();
        if (level != null)
        {
            level.Clear();
        }
        else
        {
            GameObject levelObject = new GameObject("Level");
            level = levelObject.AddComponent<Level>();
        }

        foreach (var model in bsp.models)
        {
            foreach (var geometry in model.geometries)
            {
                GenerateBrush(bsp, level, geometry, materials);
            }
        }

        foreach (var entity in bsp.entities)
        {
            GenerateEntity(bsp, level, entity);
        }
    }

    static void GenerateBrush(BSP bsp, Level level, BSPGeometry geometry, IList<Material> materials)
    {
        LevelBrush brush = level.CreateBrush();
        Mesh mesh = GenerateMesh(geometry);

        MeshFilter meshFilter = brush.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = brush.GetComponent<MeshRenderer>();
        meshRenderer.material = materials[(int) geometry.tex_id];
    }

    static Mesh GenerateMesh(BSPGeometry geometry)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        BufferGeometry g = geometry.geometry;
        for (int vi = 0, ti = 0; vi < g.verts.Length; vi +=3, ti += 3)
        {
            vertices.Add(TransformVertex(g.verts[vi + 0]));
            vertices.Add(TransformVertex(g.verts[vi + 1]));
            vertices.Add(TransformVertex(g.verts[vi + 2]));

            uvs.Add(g.uvs[vi + 0]);
            uvs.Add(g.uvs[vi + 1]);
            uvs.Add(g.uvs[vi + 2]);

            triangles.Add(ti + 2);
            triangles.Add(ti + 1);
            triangles.Add(ti + 0);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Brush Mesh";
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    static void GenerateEntity(BSP bsp, Level level, entity_t entity)
    {
        if (entity.classname.Contains("door"))
        {
            LevelEntity entityObj = level.CreateEntity(entity.classname);
            if (entity.model != -1)
            {
                var model = bsp.FindModel(entity.model);
                entityObj.transform.position = TransformVertex(model.origin);
            }
            else
            {
                entityObj.transform.position = TransformVertex(entity.origin);
            }
        }
    }

    static Vector3 TransformVertex(Vector3 v)
    {
        return new Vector3(v.x, v.z, v.y);
    }
}
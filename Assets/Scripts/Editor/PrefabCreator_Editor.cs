using UnityEngine;
using UnityEditor;

public class PrefabCreator_Editor : EditorWindow
{
    [MenuItem("Window/Prefab Creator")]

   public static void ShowWindow()
    {
        GetWindow<PrefabCreator_Editor>("Prefab Creator");
    }


   void OnGUI()
    {
        GUILayout.Label("Create prefabs from gameobjects", EditorStyles.boldLabel);
        GUILayout.Label("Drag the fbx models into the scene, select them, and click the \"Create Prefabs\" to automatically generatep prefabs of the models with materials and LODs", EditorStyles.wordWrappedLabel);

       if (GUILayout.Button("Create Prefabs"))
        {
            CreatePrefabs();
        }
    }

   void CreatePrefabs()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            string newname = obj.name.Remove(obj.name.Length - 7);
            string objAssName = obj.name.Remove(obj.name.Length - 4);
            string objAssPath = "Assets/Scans/Quixel/" + objAssName + "ms/";

            GameObject prefab = new GameObject(newname);
            prefab.transform.position = Vector3.zero;
            obj.transform.parent = prefab.transform;
            obj.transform.position = Vector3.zero;

           Material material = new Material(Shader.Find("Third Dimension Studios/MegascansSurface"));
           //Texture texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Scans/textures/" + obj.name + ".jpg");
           //if(!texture)
           //{
           //     texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Scans/textures/" + obj.name + ".png");
           //}


           if(AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "albedo.jpg"))
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "albedo.jpg"));
           if(AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "normal.jpg"))
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "normal.jpg"));
           if(AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "roughness.jpg"))
            material.SetTexture("_Roughness", AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "roughness.jpg"));
           if(AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "cavity.jpg"))
            material.SetTexture("_Cavity", AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "cavity.jpg"));
           if(AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "displacement.jpg"))
            material.SetTexture("_DisplacementMap", AssetDatabase.LoadAssetAtPath<Texture>(objAssPath + objAssName + "displacement.jpg"));

           AssetDatabase.CreateAsset(material, "Assets/Scans/QuixelMaterials/" + newname + ".mat");

           Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

           for (int i = 0; i < renderers.Length; i++)
            {

               renderers[i].sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scans/QuixelMaterials/" + newname + ".mat");

           }

           PrefabUtility.CreatePrefab("Assets/Prefabs/"+ prefab.name + ".prefab", prefab);
        }
    }
}
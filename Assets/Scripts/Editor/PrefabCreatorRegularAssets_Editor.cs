using UnityEngine;
using UnityEditor;

public class PrefabCreatorRegularAssets_Editor : EditorWindow
{
    [MenuItem("Window/Prefab Creator Regular Assets")]

   public static void ShowWindow()
    {
        GetWindow<PrefabCreatorRegularAssets_Editor>("Window/Prefab Creator Regular Assets");
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
            GameObject prefab = new GameObject(obj.name);
            prefab.transform.position = Vector3.zero;
            obj.transform.parent = prefab.transform;
            obj.transform.position = Vector3.zero;

           Material material = new Material(Shader.Find("Standard"));
           Texture texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Scans/textures/" + obj.name + ".jpg");
           if(!texture)
           {
                texture = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Scans/textures/" + obj.name + ".png");
           }



            material.SetTexture("_MainTex", texture);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Scans/normals/" + obj.name + "_normal.png"));

           AssetDatabase.CreateAsset(material, "Assets/Materials/" + obj.name + ".mat");

           Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

           for (int i = 0; i < renderers.Length; i++)
            {

               renderers[i].sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + obj.name + ".mat");

           }

           PrefabUtility.CreatePrefab("Assets/Prefabs/"+ prefab.name+".prefab", prefab);
        }
    }
}
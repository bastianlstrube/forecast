using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine;
using UnityEditor;
using Utility;

[ExecuteInEditMode]
public class TrailFlowPainter_Editor : EditorWindow {

    public static ParticleTrailSimulation particleTrailSimulation;

    enum BrushType
    {
        Paint,
        Erase
    };

    bool painting = false;
    float brushSize = 10f;
    float brushDistance = 150f;
    BrushType brushType = BrushType.Paint;
    Vector3 brushPositionPrev = new Vector3(99999, 99999, 99999);

    private Texture paintButtonTexture;
    private Texture eraseButtonTexture;
    private Vector3 moveVector = Vector3.zero;

    private bool checkAction = false;
    private bool unsaved = false;
    private bool erasing = false;

    private string filename;
    private string persistentFilePath;
    private bool loading = false;

    [MenuItem("Window/Trail Flow Painter")]
    public static void ShowWindow()
    {
        GetWindow<TrailFlowPainter_Editor>("Flow Painter");
    }

    void OnEnable()
    {
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;
        paintButtonTexture = (Texture)AssetDatabase.LoadAssetAtPath("Assets/Scripts/Editor/Textures/paintBrush.png", typeof(Texture));
        eraseButtonTexture = (Texture)AssetDatabase.LoadAssetAtPath("Assets/Scripts/Editor/Textures/eraseBrush.png", typeof(Texture));
        unsaved = false;

        filename = "New Flow";
        persistentFilePath = "";
    }

    void OnGUI()
    {
        if (EditorApplication.isPlaying)
        {
            particleTrailSimulation = GameObject.FindGameObjectWithTag("ParticleSimulation").GetComponent<ParticleTrailSimulation>();

            if (checkAction)
            {
                GUILayout.Label("There are unsaved changes. Proceed?");

                GUILayout.BeginHorizontal(GUIStyle.none);
                if (GUILayout.Button("No"))
                {
                    checkAction = false;
                }
                if (GUILayout.Button("Yes"))
                {
                    checkAction = false;
                    if (loading)
                    {
                        LoadFlow();
                        loading = false;
                    }
                    else
                    {
                        ClearFlow();
                    }
                }
            }
            else
            {
                if (unsaved)
                {
                    GUILayout.Label(filename+" *");
                }
                else
                {
                    GUILayout.Label(filename);
                }

                GUILayout.BeginHorizontal(GUIStyle.none);
                if (GUILayout.Button("Clear Flow"))
                {
                    if (unsaved)
                        checkAction = true;
                    else
                        ClearFlow();
                }


                if (GUILayout.Button("Save"))
                {
                    if (unsaved)
                    {
                        unsaved = false;
                        if (persistentFilePath != "")
                            SaveFlow(false);
                        else
                            SaveFlow(true);
                        
                    }
                    else
                    {
                        Debug.Log("There are no changes to save!");
                    }
                }
                if (GUILayout.Button("Save As..."))
                {
                    SaveFlow(true);
                    unsaved = false;
                }

                if (GUILayout.Button("Load..."))
                {
                    if (unsaved)
                    {
                        checkAction = true;
                        loading = true;
                    }
                    else
                    {
                        LoadFlow();
                    }
                }

            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            Event e = Event.current;
            if (e.type == EventType.keyDown)
            {
                if (Event.current.keyCode == (KeyCode.P))
                {
                    painting = !painting;
                }
            }

            if (painting == false)
            {
                if (GUILayout.Button("Start Painting (p)"))
                {
                    painting = true;
                }
            }
            else
            {
                if (GUILayout.Button("Stop Painting (p)"))
                {
                    painting = false;
                }
            }

            GUIStyle boldText = new GUIStyle();
            boldText.fontSize = 15;
            boldText.fontStyle = FontStyle.Bold;

            GUIStyle subText = new GUIStyle();
            subText.fontSize = 8;
            subText.fontStyle = FontStyle.Italic;

            GUIStyle centered = new GUIStyle();
            centered.alignment = TextAnchor.MiddleCenter;

            GUILayout.Space(15);

            GUILayout.BeginArea(new Rect(new Vector2(10f, 100f), new Vector2(170f, 130f)));
            GUILayout.Label("Tool", boldText);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(brushType == BrushType.Paint, "Paint (a)"))
            {
                brushType = BrushType.Paint;
            }
            if (GUILayout.Toggle(brushType == BrushType.Erase, "Erase (s)"))
            {
                brushType = BrushType.Erase;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Box(paintButtonTexture, GUILayout.Width(75), GUILayout.Height(75));
            GUILayout.Space(5);
            GUILayout.Box(eraseButtonTexture, GUILayout.Width(75), GUILayout.Height(75));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(new Vector2(200f, 100f), new Vector2(150f, 300f)));

            GUILayout.Label("Brush Size", boldText);

            GUILayout.BeginHorizontal();
            GUILayout.Label("(Numpad -)", subText);
            GUILayout.Space(50);
            GUILayout.Label("(Numpad +)", subText);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUIStyle.none);
            if (GUILayout.Button("-"))
            {
                brushSize -= 0.1f;
            }

            brushSize = float.Parse(GUILayout.TextField(brushSize.ToString()));

            if (GUILayout.Button("+"))
            {
                brushSize += 0.1f;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.Label("Brush Distance", boldText);

            GUILayout.BeginHorizontal();
            GUILayout.Label("(Numpad /)", subText);
            GUILayout.Space(50);
            GUILayout.Label("(Numpad *)", subText);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUIStyle.none);
            if (GUILayout.Button("-"))
            {
                brushDistance -= 0.1f;
            }

            brushDistance = float.Parse(GUILayout.TextField(brushDistance.ToString()));

            if (GUILayout.Button("+"))
            {
                brushDistance += 0.1f;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();


            GUILayout.Space(15);
        } else
        {
            GUILayout.Label("Enter play mode to edit the flow field");

        }
    }

    public void Update()
    {
        Repaint();
    }


    void OnSceneGUI(SceneView sceneView)
    {

        if (EditorApplication.isPlaying)
        {
            particleTrailSimulation = GameObject.FindGameObjectWithTag("ParticleSimulation").GetComponent<ParticleTrailSimulation>();

            Vector3 brushPosition = new Vector3(99999, 99999, 99999);

            Event e = Event.current;
            if (e.type == EventType.keyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.P:
                        painting = !painting;
                        break;
                    case KeyCode.A:
                        brushType = BrushType.Paint;
                        break;
                    case KeyCode.S:
                        brushType = BrushType.Erase;
                        break;
                    case KeyCode.V:
                        particleTrailSimulation.drawVelocityVectors = !particleTrailSimulation.drawVelocityVectors;
                        break;
                    case KeyCode.Plus:
                    case KeyCode.KeypadPlus:
                        brushSize += 0.1f;
                        break;
                    case KeyCode.Minus:
                    case KeyCode.KeypadMinus:
                        brushSize -= 0.1f;
                        break;
                    case KeyCode.KeypadMultiply:
                        brushDistance += 0.5f;
                        break;
                    case KeyCode.KeypadDivide:
                        brushDistance -= 0.5f;
                        break;
                    default:
                        break;
                }
            }
            brushSize = Mathf.Round(brushSize * 10f) / 10f;
            brushDistance = Mathf.Round(brushDistance * 10f) / 10f;
            if (brushSize < 0)
            {
                brushSize = 0;
            }
            if (brushDistance < 0)
            {
                brushDistance = 0;
            }

            Handles.BeginGUI();
            Handles.Label(Vector3.zero, "");

            if (painting)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

                Vector2 position = MouseHelper.Position;
                Vector3 offset = new Vector3((position.x - sceneView.position.x) / sceneView.position.width, ((sceneView.position.height + sceneView.position.y) - position.y) / sceneView.position.height, 0);
                Ray ray = sceneView.camera.ViewportPointToRay(offset);

                ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);


                brushPosition = ray.origin + ray.direction.normalized * brushDistance;

                if (e.type == EventType.MouseDrag && !e.alt && e.button == 0)
                {
                    unsaved = true;
                    erasing = false;
                    if (brushPositionPrev == new Vector3(99999, 99999, 99999))
                    {
                        brushPositionPrev = brushPosition;
                        moveVector = Vector3.zero;
                    }
                    else
                    {
                        if (brushType == BrushType.Erase)
                        {
                            moveVector = Vector3.zero;
                            erasing = true;
                        }
                        else
                        {
                            moveVector = (brushPosition - brushPositionPrev).normalized;
                        }
                        brushPositionPrev = brushPosition;
                    }
                }
                else if (e.type == EventType.MouseUp)
                {
                    brushPositionPrev = new Vector3(99999, 99999, 99999);
                    moveVector = Vector3.zero;
                    erasing = false;

                }
                else
                {
                    moveVector = Vector3.zero;
                }

                if (particleTrailSimulation)
                {
                    
                    particleTrailSimulation.flowpainterSourcePosition = brushPosition;
                    particleTrailSimulation.flowpainterBrushDistance = brushDistance;
                    particleTrailSimulation.flowpainterSourceVelocity = moveVector;
                    particleTrailSimulation.flowpainterBrushSize = brushSize;
                    particleTrailSimulation.erasing = erasing;
                }

                if (brushType == BrushType.Paint)
                {
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize);
                    float alphaScale = 0.5f - brushDistance / 20f;
                    Handles.color = new Color(1, 1, 1, alphaScale);

                    Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize * 1.1f);
                    Handles.color = Color.red;
                    Handles.DrawWireDisc(brushPosition, Vector3.up, brushSize);
                    Handles.color = Color.green;
                    Handles.DrawWireDisc(brushPosition, Vector3.right, brushSize);
                    Handles.color = Color.blue;
                    Handles.DrawWireDisc(brushPosition, Vector3.forward, brushSize);
                }
                else if (brushType == BrushType.Erase)
                {
                    Handles.color = Color.black;
                    Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize);
                    Handles.color = Color.yellow;
                    Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize + 0.5f);

                    float alphaScale = 0.5f - brushDistance / 20f;
                    Handles.color = new Color(1, 1, 1, alphaScale);

                    Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize * 1.1f);
                    Handles.color = Color.red;
                    Handles.DrawWireDisc(brushPosition, Vector3.up, brushSize);
                    Handles.color = Color.green;
                    Handles.DrawWireDisc(brushPosition, Vector3.right, brushSize);
                    Handles.color = Color.blue;
                    Handles.DrawWireDisc(brushPosition, Vector3.forward, brushSize);
                }
                Handles.color = Color.blue * 0.5f;
                Handles.DrawDottedLine(brushPosition, new Vector3(0, brushPosition.y, brushPosition.z), 1);
                Handles.DrawWireDisc(new Vector3(particleTrailSimulation.transform.position.x, brushPosition.y, brushPosition.z), Vector3.right, brushSize);

                Handles.color = Color.green * 0.5f;
                Handles.DrawDottedLine(brushPosition, new Vector3(brushPosition.x, 0, brushPosition.z), 1);
                Handles.DrawWireDisc(new Vector3(brushPosition.x, particleTrailSimulation.transform.position.y, brushPosition.z), Vector3.up, brushSize);

                Handles.color = Color.red * 0.5f;
                Handles.DrawDottedLine(brushPosition, new Vector3(brushPosition.x, brushPosition.y, 0), 1);
                Handles.DrawWireDisc(new Vector3(brushPosition.x, brushPosition.y, particleTrailSimulation.transform.position.z), Vector3.forward, brushSize);
            }
            else
            {
                //HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            Handles.EndGUI();
            SceneView.RepaintAll();
        }
    }

    void OnDestroy()
    {
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
    }

    void ClearFlow()
    {
        persistentFilePath = "";
        filename = "New Flow";
        if (particleTrailSimulation)
            particleTrailSimulation.InitialiseConstantFlowBuffer();

        unsaved = false;
    }

    void LoadFlow()
    {
        if (particleTrailSimulation)
        {
            string path = EditorUtility.OpenFilePanel("Load Flow...", "./Assets/Flows", "flo");

            if (File.Exists(path))
            {
                System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
                timer.Start();

                EditorUtility.DisplayProgressBar("Loading " + Path.GetFileName(path), "Loading Flow Map file from system...", 0f);

                BinaryFormatter bf = new BinaryFormatter();
                FileStream fileStream = new FileStream(path, FileMode.Open);

                EditorUtility.DisplayProgressBar("Loading " + Path.GetFileName(path), "Deserializing Flow Map...", 0.2f);

                //byte[] byteArray = System.IO.File.ReadAllBytes(path);
                //https://www.reddit.com/r/Unity3D/comments/5h7re0/serializing_deserializing_data_is_extremely_slow/

                FlowMap flowMap = bf.Deserialize(fileStream) as FlowMap;

                EditorUtility.DisplayProgressBar("Loading " + Path.GetFileName(path), "Closing filestream...", 0.8f);

                fileStream.Close();

                EditorUtility.DisplayProgressBar("Loading " + Path.GetFileName(path), "Passing Flow Map to GPU...", 0.9f);

                particleTrailSimulation.SetFlowMap(flowMap.DeserializeFlowMap(), flowMap.mapSizeX, flowMap.mapSizeY, flowMap.mapSizeZ);
                filename = Path.GetFileName(path);
                persistentFilePath = path;
                unsaved = false;

                EditorUtility.ClearProgressBar();
            }else{
                Debug.Log("File does not exist!");
            }
        } else
        {
            Debug.Log("Draw something first. Yeah my code is broken.");
        }
    }

    void SaveFlow(bool saveAs)
    {
        if (saveAs)
        {
            string path = EditorUtility.SaveFilePanel("Save Flow As...", "./Assets/Flows", "", "flo");
            persistentFilePath = path;
            if (path != "")
            {
                EditorUtility.DisplayProgressBar("Saving " + Path.GetFileName(path), "Grabbing Flow Map from GPU...", 0.1f);
                FlowMap flowMap = new FlowMap(particleTrailSimulation.GetFlowMap(), particleTrailSimulation.velocityBoxSize.x, particleTrailSimulation.velocityBoxSize.y, particleTrailSimulation.velocityBoxSize.z);

                BinaryFormatter bf = new BinaryFormatter();
                FileStream fileStream = new FileStream(path, FileMode.Create);

                EditorUtility.DisplayProgressBar("Saving " + Path.GetFileName(path), "Serializing flow map...", 0.3f);
                bf.Serialize(fileStream, flowMap);
                fileStream.Close();

                EditorUtility.DisplayProgressBar("Saving " + Path.GetFileName(path), "Saving to file...", 0.9f);

                Debug.Log("Saved flow as: " + path);
                filename = Path.GetFileName(path);
                unsaved = false;

                EditorUtility.ClearProgressBar();
            }
        } else
        {
            if (persistentFilePath != "")
            {
                EditorUtility.DisplayProgressBar("Saving " + Path.GetFileName(persistentFilePath), "Grabbing Flow Map from GPU...", 0.1f);
                
                FlowMap flowMap = new FlowMap(particleTrailSimulation.GetFlowMap(), particleTrailSimulation.velocityBoxSize.x, particleTrailSimulation.velocityBoxSize.y, particleTrailSimulation.velocityBoxSize.z);

                BinaryFormatter bf = new BinaryFormatter();
                FileStream fileStream = new FileStream(persistentFilePath, FileMode.Create);

                EditorUtility.DisplayProgressBar("Saving " + Path.GetFileName(persistentFilePath), "Serializing flow map...", 0.3f);

                bf.Serialize(fileStream, flowMap);
                fileStream.Close();

                EditorUtility.DisplayProgressBar("Saving " + Path.GetFileName(persistentFilePath), "Saving to file...", 0.9f);

                Debug.Log("Saved flow: " + persistentFilePath);
                filename = Path.GetFileName(persistentFilePath);
                unsaved = false;

                EditorUtility.ClearProgressBar();
            }
        }
        
    }
}

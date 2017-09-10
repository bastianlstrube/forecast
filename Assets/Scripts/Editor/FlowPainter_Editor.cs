using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Utility;

[ExecuteInEditMode]
public class FlowPainter_Editor : EditorWindow {

    public static ParticleSimulation particleSimulation;

    enum BrushType
    {
        Sphere,
        Cube
    };

    [System.Serializable]
    public struct IntVector3
    {
        public int x;
        public int y;
        public int z;

        // Constructor
        public IntVector3(int _x, int _y, int _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }
    }

    bool flowMapLoaded = false;
    bool painting = false;
    float brushSize = 10f;
    float brushDistance = 150f;
    BrushType brushType = BrushType.Sphere;
    Vector3 brushPositionPrev = new Vector3(99999, 99999, 99999);

    private Texture sphereButtonTexture;
    private Texture cubeButtonTexture;
    private IntVector3 velocityBoxSize = new IntVector3(12, 12, 12);
    int boxVolume = 12 * 12 * 12;
    private Vector3 moveVector = Vector3.zero;
    Vector3[] flowMap;

    [MenuItem("Window/Flow Painter")]
    public static void ShowWindow()
    {
        GetWindow<FlowPainter_Editor>("Flow Painter");
    }

    void OnEnable()
    {
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;
        cubeButtonTexture = (Texture)AssetDatabase.LoadAssetAtPath("Assets/Scripts/Editor/Textures/cubeBrush.png", typeof(Texture));
        sphereButtonTexture = (Texture)AssetDatabase.LoadAssetAtPath("Assets/Scripts/Editor/Textures/sphereBrush.png", typeof(Texture));
        flowMapLoaded = false;
    }

    void NewFlow()
    {
        boxVolume = velocityBoxSize.x * velocityBoxSize.y * velocityBoxSize.z;
        flowMap = new Vector3[boxVolume];
        for (int i = 0; i < boxVolume; i++)
        {
            flowMap[i] = Vector3.zero;
        }
        flowMapLoaded = true;
        //DrawVelocityBox();
    }

    void DrawVelocityBox(Vector3 brushPosition)
    {
        if (flowMapLoaded)
        {
            for (int i = 0; i < boxVolume; i++)
            {
                int xPosition = i % velocityBoxSize.z;
                int yPosition = (i / velocityBoxSize.z) % velocityBoxSize.y;
                int zPosition = i / (velocityBoxSize.y * velocityBoxSize.z);

                Vector3 position = new Vector3(xPosition, yPosition, zPosition);

                if((position-brushPosition).magnitude <= brushSize)
                {
                    Handles.color = Color.red;
                    if(moveVector != Vector3.zero)
                        flowMap[i] = moveVector;
                } else
                {
                    Handles.color = Color.white * 0.2f;
                }
                
                if (flowMap[i] == Vector3.zero)
                {
                    Handles.DrawLine(position - Vector3.up * 0.05f, position + Vector3.up * 0.05f);
                    Handles.DrawLine(position - Vector3.right * 0.05f, position + Vector3.right * 0.05f);
                    Handles.DrawLine(position - Vector3.forward * 0.05f, position + Vector3.forward * 0.05f);
                } else {
                    Handles.color = Color.white * 0.5f;
                    Handles.DrawLine(position, position + flowMap[i] * 0.5f);
                }
            }
        }
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal(GUIStyle.none);
        if(GUILayout.Button("New Flow"))
        {
            NewFlow();
        }
        GUILayout.Button("Load Flow");
        GUILayout.Button("Save Flow");
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

        GUILayout.BeginArea(new Rect(new Vector2(10f, 70f), new Vector2(170f, 130f)));
        GUILayout.Label("Brush Shape", boldText);
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(brushType==BrushType.Sphere, "Sphere (s)"))
        {
            brushType = BrushType.Sphere;
        }
        if(GUILayout.Toggle(brushType == BrushType.Cube, "Cube (c)"))
        {
            brushType = BrushType.Cube;
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Box(sphereButtonTexture, GUILayout.Width(75), GUILayout.Height(75));
        GUILayout.Space(5);
        GUILayout.Box(cubeButtonTexture, GUILayout.Width(75), GUILayout.Height(75));
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(new Vector2(200f, 70f), new Vector2(150f, 300f)));

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
    }

    public void Update()
    {
        Repaint();
    }


    void OnSceneGUI(SceneView sceneView)
    {

        if(EditorApplication.isPlaying)
        {
            particleSimulation = GameObject.FindGameObjectWithTag("ParticleSimulation").GetComponent<ParticleSimulation>();
        }

        Vector3 brushPosition = new Vector3(99999,99999,99999);

        Event e = Event.current;
        if (e.type == EventType.keyDown)
        {
            switch (Event.current.keyCode)
            {
                case KeyCode.P:
                    painting = !painting;
                    break;
                case KeyCode.S:
                    brushType = BrushType.Sphere;
                    break;
                case KeyCode.C:
                    brushType = BrushType.Cube;
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
                if (brushPositionPrev == new Vector3(99999, 99999, 99999))
                {
                    brushPositionPrev = brushPosition;
                    moveVector = Vector3.zero;
                }
                else
                {
                    
                    moveVector = (brushPosition - brushPositionPrev).normalized;
                    brushPositionPrev = brushPosition;
                }
            } 
            else if (e.type == EventType.MouseUp)
            {
                brushPositionPrev = new Vector3(99999, 99999, 99999);
                moveVector = Vector3.zero;
                
            } else
            {
                moveVector = Vector3.zero;
            }

            if (particleSimulation)
            {
                particleSimulation.flowpainterSourcePosition = brushPosition;
                particleSimulation.flowpainterBrushDistance = brushDistance;
                particleSimulation.flowpainterSourceVelocity = moveVector;
                particleSimulation.flowpainterBrushSize = brushSize;
            }

            if (brushType == BrushType.Sphere)
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize);
                float alphaScale = 0.5f-brushDistance/20f;
                Handles.color = new Color(1, 1, 1, alphaScale);
                
                Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize * 1.1f);
                Handles.color = Color.red;
                Handles.DrawWireDisc(brushPosition, Vector3.up, brushSize);
                Handles.color = Color.green;
                Handles.DrawWireDisc(brushPosition, Vector3.right, brushSize);
                Handles.color = Color.blue;
                Handles.DrawWireDisc(brushPosition, Vector3.forward, brushSize);
            } else if (brushType == BrushType.Cube)
            {
                Handles.DrawWireCube(brushPosition, Vector3.one * brushSize);
            }
            Handles.color = Color.blue * 0.5f;
            Handles.DrawDottedLine(brushPosition, new Vector3(0, brushPosition.y, brushPosition.z), 1);
            Handles.DrawWireDisc(new Vector3(0, brushPosition.y, brushPosition.z), Vector3.right, brushSize);

            Handles.color = Color.green * 0.5f;
            Handles.DrawDottedLine(brushPosition, new Vector3(brushPosition.x, 0, brushPosition.z), 1);
            Handles.DrawWireDisc(new Vector3(brushPosition.x, 0, brushPosition.z), Vector3.up, brushSize);

            Handles.color = Color.red * 0.5f;
            Handles.DrawDottedLine(brushPosition, new Vector3(brushPosition.x, brushPosition.y, 0), 1);
            Handles.DrawWireDisc(new Vector3(brushPosition.x, brushPosition.y, 0), Vector3.forward, brushSize);

        } else
        {
            //HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }



        DrawVelocityBox(brushPosition);

        Handles.EndGUI();
        SceneView.RepaintAll();
    }

    void OnDestroy()
    {
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
    }



    static void Start()
    {
        
        Debug.Log("HI");

    }
}


namespace Utility
{
    /// <summary>
    /// This is used to find the mouse position when it's over a SceneView.
    /// Used by tools that are menu invoked.
    /// </summary>
    [InitializeOnLoad]
    public class MouseHelper : Editor
    {
        private static Vector2 position;

        public static Vector2 Position
        {
            get { return position; }
        }

        static MouseHelper()
        {
            SceneView.onSceneGUIDelegate += UpdateView;
        }

        private static void UpdateView(SceneView sceneView)
        {
            if (Event.current != null)
                position = new Vector2(Event.current.mousePosition.x + sceneView.position.x, Event.current.mousePosition.y + sceneView.position.y);
        }
    }
}

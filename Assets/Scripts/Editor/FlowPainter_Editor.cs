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



    [MenuItem("Window/Flow Painter")]
    public static void ShowWindow()
    {
        GetWindow<FlowPainter_Editor>("Flow Painter");
    }

    void OnEnable()
    {
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;
        paintButtonTexture = (Texture)AssetDatabase.LoadAssetAtPath("Assets/Scripts/Editor/Textures/paintBrush.png", typeof(Texture));
        eraseButtonTexture = (Texture)AssetDatabase.LoadAssetAtPath("Assets/Scripts/Editor/Textures/eraseBrush.png", typeof(Texture));
    }

    void OnGUI()
    {
        

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
                ClearFlow();
            }
        }
        else
        {
            if (unsaved)
            {
                GUILayout.Label("Flow01.flo *");
            } else
            {
                GUILayout.Label("Flow01.flo  ");
            }

            GUILayout.BeginHorizontal(GUIStyle.none);
            if (GUILayout.Button("Clear Flow"))
            {
                if(unsaved)
                    checkAction = true;
                else
                    ClearFlow();
            }
            

            if (GUILayout.Button("Save  "))
            {
                if (unsaved)
                {
                    Debug.Log("Flow saved to C:/fjkohfa/flow01.flo");
                    unsaved = false;
                }
                else
                {
                    Debug.Log("No flow changes to save");
                }
            }
            if (GUILayout.Button("Save As..."))
            {
                if (unsaved)
                {
                    Debug.Log("Flow saved to C:/fjkohfa/flow01.flo");
                    unsaved = false;
                }
                else
                {
                    Debug.Log("No flow changes to save");
                }
            }

            if (GUILayout.Button("Load..."))
            {
                if (unsaved)
                    checkAction = true;
                else
                    LoadFlow();
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
        if (GUILayout.Toggle(brushType==BrushType.Paint, "Paint (a)"))
        {
            brushType = BrushType.Paint;
        }
        if(GUILayout.Toggle(brushType == BrushType.Erase, "Erase (s)"))
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
                case KeyCode.A:
                    brushType = BrushType.Paint;
                    break;
                case KeyCode.S:
                    brushType = BrushType.Erase;
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

                Handles.color = Color.white;
                Handles.DrawDottedLine(brushPosition, brushPosition + moveVector * 5f, 1);
            }

            if (brushType == BrushType.Paint)
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
            } else if (brushType == BrushType.Erase)
            {
                Handles.color = Color.black;
                Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize);
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(brushPosition, ray.direction.normalized, brushSize+0.5f);

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

        Handles.EndGUI();
        SceneView.RepaintAll();
    }

    void OnDestroy()
    {
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
    }

    void ClearFlow()
    {
        if(particleSimulation)
            particleSimulation.InitialiseVectorMap();

        unsaved = false;
    }

    void LoadFlow()
    {
        unsaved = false;
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

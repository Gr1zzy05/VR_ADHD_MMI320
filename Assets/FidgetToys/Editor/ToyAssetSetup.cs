using System;
using System.Linq;
using FocusShift.Toys;
using HomeTasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace FocusShift.Toys.Editor
{
    public static class ToyAssetSetup
    {
        const string Root = "Assets/FidgetToys/";
        const string Demo = Root + "Scenes/FidgetToyDemo.unity";
        static readonly string[] Colors = { "Violet", "Lagoon", "Leaf", "Lemon", "Tangerine", "Coral" };
        static readonly string[] Hex = { "#AC5CD4", "#15C8E9", "#5BD963", "#FFDC34", "#FFA037", "#F65B51" };

        [MenuItem("Home Focus/Fidget Toys/Create Missing Assets")]
        public static void Build()
        {
            foreach (var folder in new[] {"Prefabs", "Materials", "Scenes"})
                if (!AssetDatabase.IsValidFolder(Root + folder)) AssetDatabase.CreateFolder("Assets/FidgetToys", folder);
            var butter = Prefab("ButterSquishy", true);
            var board = Prefab("RainbowPopIt", false);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Demo) == null) BuildScene(butter, board);
        }

        static Material Surface(string name, string hex, Texture print = null)
        {
            string path = Root + "Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ColorUtility.TryParseHtmlString(hex, out Color color);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", .31f);
            if (print != null) mat.SetTexture("_BaseMap", print);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static GameObject Prefab(string name, bool butter)
        {
            string path = Root + "Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(name);
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/" + name + "/" + name + ".fbx");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(root.transform, false);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (butter)
                            materials[i] = Surface("ButterPrint", "#FFFFFF", AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Materials/ButterPrint.png"));
                        else
                        {
                            int color = Array.FindIndex(Colors, c => materials[i].name.Contains(c));
                            if (color < 0) throw new InvalidOperationException("Unknown silicone material " + materials[i].name);
                            materials[i] = Surface(Colors[color], Hex[color]);
                        }
                    }
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = butter ? .105f : .135f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Collider shape;
                if (butter)
                {
                    var skin = visual.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
                    var controller = root.AddComponent<ButterSquishyController>();
                    controller.foam = skin;
                    var box = root.AddComponent<BoxCollider>();
                    box.size = new Vector3(.158f,.039f,.059f);
                    shape = box;
                }
                else
                {
                    var controller = root.AddComponent<PopItController>();
                    controller.popSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/SoftPop.wav");
                    var audio = root.GetComponent<AudioSource>();
                    audio.playOnAwake = false; audio.spatialBlend = 1f; audio.minDistance = .15f;
                    controller.bubbles = visual.GetComponentsInChildren<SkinnedMeshRenderer>().Select(skin =>
                    {
                        var node = new GameObject(skin.name + " Input");
                        node.transform.SetParent(root.transform,false);
                        var b = node.AddComponent<PopItBubble>();
                        b.membrane = skin;
                        var center = root.transform.InverseTransformPoint(skin.bounds.center);
                        b.centerOnBoard = new Vector2(center.x, center.z);
                        b.enabled = false;
                        return b;
                    }).ToArray();
                    if (controller.bubbles.Length != 28) throw new InvalidOperationException("Expected 28 membranes");
                    var hull = root.AddComponent<MeshCollider>();
                    hull.sharedMesh = Hull(); hull.convex = true;
                    shape = hull;
                }
                var grab = root.GetComponent<XRGrabInteractable>();
                grab.colliders.Clear(); grab.colliders.Add(shape);
                grab.selectMode = InteractableSelectMode.Single;
                grab.interactionLayers = 1;
                grab.useDynamicAttach = true;
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                root.transform.rotation = Quaternion.Euler(0, 180, 0);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Mesh Hull()
        {
            const string path = Root+"Models/RainbowPopIt/GrabHull.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            Vector2[] outline = {new(-.05f,.105f),new(.05f,.105f),new(.105f,.05f),new(.105f,-.05f),
                new(.05f,-.105f),new(-.05f,-.105f),new(-.105f,-.05f),new(-.105f,.05f)};
            var points = new Vector3[16];
            var triangles = new System.Collections.Generic.List<int>();
            for (int i=0;i<8;i++)
            {
                int j=(i+1)%8;
                points[i]=new Vector3(outline[i].x,.009f,outline[i].y);
                points[i+8]=new Vector3(outline[i].x,-.009f,outline[i].y);
                triangles.AddRange(new[]{i,i+8,j+8,i,j+8,j});
                if (i>0 && i<7) triangles.AddRange(new[]{0,i,j,8,j+8,i+8});
            }
            mesh=new Mesh { name="Octagonal grab hull", vertices=points, triangles=triangles.ToArray() };
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }

        static void BuildScene(GameObject butter, GameObject board)
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                GameObject Place(GameObject prefab, Vector3 position)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    go.transform.position=position;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                    return go;
                }
                void Block(string name,Vector3 pos, Vector3 size,Material mat)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name=name;go.transform.position=pos;go.transform.localScale=size;
                    go.GetComponent<Renderer>().sharedMaterial=mat;
                }
                void LightAt(string name, Vector3 rotation,float power, Color tint)
                {
                    var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;
                    light.transform.rotation=Quaternion.Euler(rotation);light.intensity=power;light.color=tint;
                    if(name=="Key")
                    {
                        light.shadows=LightShadows.Soft;
                        light.shadowStrength=.48f;
                        light.shadowBias=.025f;
                        light.shadowNormalBias=.12f;
                    }
                }
                Block("Worktop",new Vector3(0,.80f,0),new Vector3(.84f,.045f,.48f),Surface("Worktop","#506665"));
                Block("Plinth",new Vector3(0,.39f,0),new Vector3(.64f,.78f,.34f),Surface("Plinth","#334B4A"));
                Block("Floor",new Vector3(0,-.05f,0),new Vector3(5,.1f,5),Surface("Floor","#9BA8A3"));
                Place(butter,new Vector3(-.128f,.849f,0));
                var pop=Place(board,new Vector3(.111f,.837f,.008f));
                var view=new GameObject("Desktop camera").AddComponent<Camera>();
                view.gameObject.AddComponent<AudioListener>();
                view.nearClipPlane=.02f;view.fieldOfView=38;
                view.transform.position=new Vector3(0,1.22f,-.32f);
                view.transform.LookAt(new Vector3(0,.827f,0));
                view.gameObject.AddComponent<ToyDemoMouse>().board=pop.GetComponent<PopItController>();
                LightAt("Key",new Vector3(52,-35,0),1.5f,new Color(1,.95f,.87f));
                LightAt("Fill",new Vector3(35,140,0),.55f,new Color(.83f,.91f,1));
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
                var rig=Place(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HomeTasks/Prefabs/Quest Player (OpenXR) Variant.prefab"),new Vector3(0,0,-.44f));
                rig.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
                var router=new GameObject("Player Mode").AddComponent<PlayerModeRouter>();
                router.questRig=rig;router.desktopRig=view.gameObject;
                if (!EditorSceneManager.SaveScene(scene,Demo))throw new InvalidOperationException("Could not save demo");
            }
            finally { EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous); }
        }
    }
}

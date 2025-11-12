using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

public class USDArticulationTool : EditorWindow
{
    [System.Serializable]
    public class LinkInfo
    {
        public Transform transform;
        public Vector2 driveLimit = new Vector2(-100, 100);
    }

    private GameObject targetPrefab;
    private List<LinkInfo> collectedChildren = new List<LinkInfo>();
    private ReorderableList reorderableList;

    private GameObject prefabCopy;
    private Transform activeChild;

    [MenuItem("Tools/USD Articulation Tool")]
    public static void ShowWindow()
    {
        GetWindow<USDArticulationTool>("USD Articulation Tool");
    }

    private void OnEnable()
    {
        reorderableList = new ReorderableList(collectedChildren, typeof(LinkInfo), true, true, false, false);
        reorderableList.elementHeight = EditorGUIUtility.singleLineHeight * 2 + 4;
        reorderableList.drawHeaderCallback = (Rect rect) => { EditorGUI.LabelField(rect, "Collected Links"); };

        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            if (index < 0 || index >= collectedChildren.Count) return;

            var item = collectedChildren[index];
            if (item.transform == null) return;

            float rightSidePos = rect.width / 3;
            Rect transformRect = new Rect(rect.x, rect.y + 2, rightSidePos - 5, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(transformRect, item.transform.name);

            if (index != 0)
            {
                Rect limitRect = new Rect(rect.x + rightSidePos, rect.y + 2, rect.width - rightSidePos - 5,
                    EditorGUIUtility.singleLineHeight);

                GUIContent[] labels = new GUIContent[] { new GUIContent("Lower"), new GUIContent("Upper") };
                float[] values = new float[] { item.driveLimit.x, item.driveLimit.y };
                EditorGUI.MultiFloatField(limitRect, new GUIContent("Drive Limit"), labels, values);

                item.driveLimit = new Vector2(values[0], values[1]);
            }
        };
    }

    private void OnGUI()
    {
        GUILayout.Label("USD Articulation Tool", EditorStyles.boldLabel);
        targetPrefab =
            (GameObject)EditorGUILayout.ObjectField("Target Prefab", targetPrefab, typeof(GameObject), false);

        if (targetPrefab == null)
            return;

        if (GUILayout.Button("Collect Active Child Nodes"))
        {
            CollectChildNodes();
        }

        if (collectedChildren.Count > 0)
        {
            reorderableList.DoLayoutList();

            if (GUILayout.Button("Apply Hierarchy & Add ArticulationBody"))
            {
                ApplyHierarchyAndAddArticulation();
            }
        }
    }

    private void CollectChildNodes()
    {
        collectedChildren.Clear();

        prefabCopy = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(targetPrefab));
        if (prefabCopy == null) return;

        activeChild = null;
        foreach (Transform child in prefabCopy.transform)
        {
            if (child.gameObject.activeSelf)
            {
                activeChild = child;
                break;
            }
        }

        if (activeChild == null)
        {
            Debug.LogWarning("No active child found!");
            return;
        }

        foreach (Transform child in activeChild.GetComponentsInChildren<Transform>())
        {
            if (child.Find("visuals") && child.Find("collisions"))
            {
                LinkInfo linkInfo = new LinkInfo{ transform = child };
                if (child.GetComponent<ArticulationBody>())
                {
                    linkInfo.driveLimit = new Vector2
                    {
                        x=child.GetComponent<ArticulationBody>().xDrive.lowerLimit,
                        y=child.GetComponent<ArticulationBody>().xDrive.upperLimit
                    };
                }
                collectedChildren.Add(linkInfo);
            }
        }
    }

    private void ApplyHierarchyAndAddArticulation()
    {
        if (activeChild == null)
        {
            Debug.LogWarning("No active child found!");
            return;
        }

        if (!prefabCopy.GetComponent<ArticulationBody>())
            prefabCopy.AddComponent<ArticulationBody>();

        Transform previous = activeChild;
        foreach (var link in collectedChildren)
        {
            Transform t = link.transform;
            if (t == null) continue;

            t.SetParent(previous);

            if (t.GetComponent<ArticulationBody>() == null)
                t.gameObject.AddComponent<ArticulationBody>();
            
            previous = t;
        }
        

        foreach (var link in collectedChildren)
        {
            Transform t = link.transform;
            ArticulationBody a = t.GetComponent<ArticulationBody>();
            a.anchorRotation = Quaternion.Euler(0, -90, 0);
            if (link.transform.name == "base")
            {
                a.jointType = ArticulationJointType.FixedJoint;
            }
            else
            {
                a.jointType = ArticulationJointType.RevoluteJoint;
                a.twistLock = ArticulationDofLock.LimitedMotion;
                
                ArticulationDrive drive = a.xDrive;
                drive.driveType = ArticulationDriveType.Target;
                drive.lowerLimit = link.driveLimit.x;
                drive.upperLimit = link.driveLimit.y;
                a.xDrive = drive;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(prefabCopy, AssetDatabase.GetAssetPath(targetPrefab));
        PrefabUtility.UnloadPrefabContents(prefabCopy);

        Debug.Log("Hierarchy updated and ArticulationBodies added to prefab!");
    }
}
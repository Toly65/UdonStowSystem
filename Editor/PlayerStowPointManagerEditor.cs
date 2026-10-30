using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerStowPointManager), true)]
public class PlayerStowPointManagerEditor : Editor
{
    private static readonly KeyCode[] NumberKeyOptions =
    {
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6,
        KeyCode.Alpha7,
        KeyCode.Alpha8,
        KeyCode.Alpha9,
        KeyCode.Alpha0
    };

    private static readonly string[] NumberKeyLabels =
    {
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "0"
    };

    private SerializedProperty stowPointsProp;
    private SerializedProperty stowKeysProp;
    private SerializedProperty desktopDisplayDistanceProp;
    private SerializedProperty selectionNoiseProp;
    private SerializedProperty vrPlayerTrackingConnectorProp;

    private static readonly string[] BoneLabels = { "Hips", "Chest" };
    private static readonly string[] SideLabels = { "Right", "Left", "Centre" };
    // sphere scale that reads sensibly per size class (extra small .. extra large); tweak per point afterwards
    private static readonly float[] SizeScales = { 0.7f, 1f, 1.8f, 2.2f };
    private int addBone;
    private int addSide;
    private StowSize addSize = StowSize.Small;

    private StowShape addShape;
    private StowShape layoutShape;
    private bool layoutAutoBox = true;
    private Vector3 layoutBox = new Vector3(0.04f, 0.04f, 0.02f);

    private bool showLayout;
    private int layoutBone;
    private StowSize layoutSize = StowSize.ExtraSmall;
    private int layoutCount = 3;
    private int layoutRows = 1;
    private float layoutCentre = 90f;
    private float layoutSpread = 90f;
    private float layoutRadius = 0.15f;
    private float layoutRowSpacing = 0.1f;
    private bool layoutFaceOutward = true;
    private StowLayoutKeys layoutKeys = StowLayoutKeys.OneKeyForAll;

    private void OnEnable()
    {
        stowPointsProp = serializedObject.FindProperty("stowPoints");
        stowKeysProp = serializedObject.FindProperty("stowKeys");
        desktopDisplayDistanceProp = serializedObject.FindProperty("desktopDisplayDistance");
        selectionNoiseProp = serializedObject.FindProperty("selectionNoise");
        vrPlayerTrackingConnectorProp = serializedObject.FindProperty("vrPlayerTrackingConnector");
    }

    // The renderer lives in the Combat System package, which this one must not depend on, so it is found by reflection.
    private static System.Reflection.MethodInfo FindPreviewBaker()
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType("UCS_StowLayoutPreviewBaker");
            if (type != null)
            {
                return type.GetMethod("Bake", new[] { typeof(PlayerStowPointManager), typeof(int) });
            }
        }

        return null;
    }

    private void DrawBakePreviewButton()
    {
        System.Reflection.MethodInfo bake = FindPreviewBaker();
        bool canBake = bake != null && !serializedObject.isEditingMultipleObjects && stowPointsProp.arraySize > 0;
        string tip = bake == null ? "Needs the Combat System package installed" : "Renders the pockets to a sprite in Assets/ItemPreviews";

        using (new EditorGUI.DisabledScope(!canBake))
        {
            Rect row = EditorGUILayout.GetControlRect();
            row.xMin += EditorGUIUtility.labelWidth;
            string label = serializedObject.FindProperty("previewImage").objectReferenceValue == null ? "Bake Preview" : "Re-bake Preview";
            if (GUI.Button(row, new GUIContent(label, tip)))
            {
                serializedObject.ApplyModifiedProperties();
                bake.Invoke(null, new object[] { target, 512 });
                serializedObject.Update();
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "stowPoints",
            "stowKeys",
            "desktopDisplayDistance",
            "selectionNoise",
            "vrPlayerTrackingConnector",
            "previewImage"
        );

        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewImage"));
        DrawBakePreviewButton();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stow Mapping", EditorStyles.boldLabel);

        SyncArraySizes();
        int slotCount = stowPointsProp.arraySize;
        int newSlotCount = Mathf.Max(0, EditorGUILayout.IntField("Slot Count", slotCount));
        if (newSlotCount != slotCount)
        {
            stowPointsProp.arraySize = newSlotCount;
            stowKeysProp.arraySize = newSlotCount;
            slotCount = newSlotCount;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Stow Point", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("Desktop Key", EditorStyles.miniBoldLabel, GUILayout.MaxWidth(150f));
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < slotCount; i++)
        {
            SerializedProperty stowPoint = stowPointsProp.GetArrayElementAtIndex(i);
            SerializedProperty stowKey = stowKeysProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginHorizontal();
            // an ObjectField, not a PropertyField: the array element's own right-click Delete would shorten
            // only one of the two parallel arrays
            stowPoint.objectReferenceValue = EditorGUILayout.ObjectField(new GUIContent("Slot " + i), stowPoint.objectReferenceValue, typeof(StowPoint), true);
            DrawNumberKeyPopup(stowKey);
            bool remove = GUILayout.Button(new GUIContent("-", "Remove this slot (the stow point object stays in the scene)"), GUILayout.Width(22f));
            EditorGUILayout.EndHorizontal();

            if (remove)
            {
                RemoveSlot(i);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        DrawSetupTools();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Desktop Display", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(desktopDisplayDistanceProp, new GUIContent("Display Distance"));
        EditorGUILayout.PropertyField(selectionNoiseProp, new GUIContent("Selection Audio"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(vrPlayerTrackingConnectorProp, new GUIContent("VR Tracking Connector", "This object is enabled in VR and disabled for desktop users."));

        serializedObject.ApplyModifiedProperties();
    }

    // The two arrays are one list of slots. If they ever differ (an old scene, an edit from the default array UI, undo)
    // pad the shorter one rather than throwing, so the inspector keeps working.
    private void SyncArraySizes()
    {
        int points = stowPointsProp.arraySize;
        int keys = stowKeysProp.arraySize;
        if (points == keys) return;

        if (keys < points)
        {
            stowKeysProp.arraySize = points;
            for (int i = keys; i < points; i++) stowKeysProp.GetArrayElementAtIndex(i).intValue = (int)NextFreeKey(stowKeysProp, i);
        }
        else
        {
            stowPointsProp.arraySize = keys;
        }
    }

    private void RemoveSlot(int index)
    {
        // deleting a non-null object reference only clears it the first time, so clear it first
        stowPointsProp.GetArrayElementAtIndex(index).objectReferenceValue = null;
        stowPointsProp.DeleteArrayElementAtIndex(index);
        stowKeysProp.DeleteArrayElementAtIndex(index);
    }

    private void DrawSetupTools()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Setup Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        addBone = EditorGUILayout.Popup(addBone, BoneLabels);
        addSide = EditorGUILayout.Popup(addSide, SideLabels);
        addSize = (StowSize)EditorGUILayout.EnumPopup(addSize);
        addShape = (StowShape)EditorGUILayout.EnumPopup(addShape, GUILayout.Width(90f));
        if (GUILayout.Button("Add Stow Point"))
        {
            serializedObject.ApplyModifiedProperties();
            AddStowPoint((PlayerStowPointManager)target, addBone == 1, addSide, addSize, addShape);
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();

        showLayout = EditorGUILayout.Foldout(showLayout, new GUIContent("Pocket Layout (ring or grid)", "Adds a whole arc or grid of pockets at once, one layout at a time."), true);
        if (showLayout)
        {
            EditorGUI.indentLevel++;
            layoutBone = EditorGUILayout.Popup("Bone", layoutBone, BoneLabels);
            layoutSize = (StowSize)EditorGUILayout.EnumPopup("Size", layoutSize);
            layoutShape = (StowShape)EditorGUILayout.EnumPopup(new GUIContent("Shape", "Sphere is the standard round pocket. Upright Box is a tall box that stays upright, for things that stand in a pouch such as magazines and grenades."), layoutShape);
            if (layoutShape == StowShape.UprightBox)
            {
                layoutAutoBox = EditorGUILayout.Toggle(new GUIContent("Box size from Size", "On: width, height and depth come from the size class (extra small is 4 x 8 x 4 cm). Off: set them yourself."), layoutAutoBox);
                if (layoutAutoBox) layoutBox = DefaultBoxSize(layoutSize);
                using (new EditorGUI.DisabledScope(layoutAutoBox))
                {
                    layoutBox.x = EditorGUILayout.Slider("Width (m)", layoutBox.x, 0.02f, 0.4f);
                    layoutBox.y = EditorGUILayout.Slider("Height (m)", layoutBox.y, 0.04f, 0.8f);
                    layoutBox.z = EditorGUILayout.Slider("Depth (m)", layoutBox.z, 0.02f, 0.4f);
                }
            }
            layoutCount = EditorGUILayout.IntSlider(new GUIContent("Pockets per row", "How many pockets along the arc."), layoutCount, 1, 8);
            layoutRows = EditorGUILayout.IntSlider(new GUIContent("Rows", "Stacked heights. 1 = a single ring, 2 or more = a grid."), layoutRows, 1, 4);
            layoutCentre = EditorGUILayout.Slider(new GUIContent("Centre angle", "Where the arc is centred, in degrees around the player: 0 = straight ahead, 90 = their right, -90 = their left, 180 = behind."), layoutCentre, -180f, 180f);
            layoutSpread = EditorGUILayout.Slider(new GUIContent("Arc width", "Total arc covered. 90 = a quarter circle, 180 = half. Ignored for a single pocket."), layoutSpread, 0f, 360f);
            layoutRadius = EditorGUILayout.Slider(new GUIContent("Radius (m)", "Distance from the bone, for a 1.0 m eye height avatar."), layoutRadius, 0.05f, 0.5f);
            layoutRowSpacing = EditorGUILayout.Slider(new GUIContent("Row spacing (m)", "Vertical gap between rows."), layoutRowSpacing, 0.02f, 0.3f);
            layoutFaceOutward = EditorGUILayout.Toggle(new GUIContent("Face outward", "Turns each pocket to face away from the player, like a pouch on a belt. Off keeps them all unrotated."), layoutFaceOutward);
            layoutKeys = (StowLayoutKeys)EditorGUILayout.EnumPopup(new GUIContent("Keys", "One key for all: the pockets cycle on a single key. One key per row: each row cycles on its own key. One key per pocket: every pocket has its own key."), layoutKeys);
            EditorGUI.indentLevel--;

            if (GUILayout.Button("Add Pocket Layout"))
            {
                serializedObject.ApplyModifiedProperties();
                AddPocketLayout((PlayerStowPointManager)target, layoutBone == 1, layoutCount, layoutRows, layoutCentre, layoutSpread,
                    layoutRadius, layoutRowSpacing, layoutSize, layoutFaceOutward, layoutKeys, layoutShape, layoutBox);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }

        bool anyEmpty = false;
        for (int i = 0; i < stowPointsProp.arraySize; i++) anyEmpty |= stowPointsProp.GetArrayElementAtIndex(i).objectReferenceValue == null;
        using (new EditorGUI.DisabledScope(!anyEmpty))
        {
            if (GUILayout.Button(new GUIContent("Remove Empty Slots", "Deletes slots whose stow point object is gone, for example after deleting a Pocket Layout by hand.")))
            {
                for (int i = stowPointsProp.arraySize - 1; i >= 0; i--)
                {
                    if (stowPointsProp.GetArrayElementAtIndex(i).objectReferenceValue == null) RemoveSlot(i);
                }
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        if (GUILayout.Button(new GUIContent("Mirror Left / Right", "Flips every stow point across the player's centre line, turning a right-handed layout into a left-handed one and back. Undoable.")))
        {
            serializedObject.ApplyModifiedProperties();
            MirrorStowPoints((PlayerStowPointManager)target);
            serializedObject.Update();
        }
    }

    // Adds a PlayerStowSphere under the hip or chest tracker, sized, with the next unused number key. Keys 1-0 only.
    private static void AddStowPoint(PlayerStowPointManager manager, bool chest, int side, StowSize size, StowShape shape = StowShape.Sphere)
    {
        Transform bone = FindBone(manager, chest);
        if (bone == null) return;

        float x = side == 0 ? 0.15f : side == 1 ? -0.15f : 0f;
        Vector3 position = chest ? new Vector3(x * 0.67f, 0.2f, -0.14f) : new Vector3(x, 0f, 0f);
        StowPoint point = CreatePoint(manager, bone, position, Quaternion.identity, size, 0, shape);
        if (point != null) Selection.activeGameObject = point.gameObject;
    }

    internal static Transform FindBone(PlayerStowPointManager manager, bool chest)
    {
        playerTrackingParenting tracker = manager.GetComponentInChildren<playerTrackingParenting>(true);
        if (tracker == null) tracker = Object.FindObjectOfType<playerTrackingParenting>();
        Transform bone = tracker == null ? null : (chest ? tracker.TorsoTransform : tracker.hipTransform);
        if (bone == null)
        {
            EditorUtility.DisplayDialog("Stow Tools", "No bone tracker found. Put this manager under the StowSystem prefab (it has the Hips and Chest trackers).", "OK");
        }

        return bone;
    }

    // One pocket under parent, shaped and sized, appended to the manager's lists. key 0 = the next unused number key.
    internal static StowPoint CreatePoint(PlayerStowPointManager manager, Transform parent, Vector3 localPosition, Quaternion localRotation, StowSize size, int key,
        StowShape shape = StowShape.Sphere, Vector3 boxSize = default(Vector3))
    {
        string[] guids = AssetDatabase.FindAssets("PlayerStowSphere t:Prefab");
        GameObject prefab = guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Stow Tools", "PlayerStowSphere.prefab wasn't found in the project.", "OK");
            return null;
        }

        var sphere = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(sphere, "Add Stow Point");
        sphere.transform.localPosition = localPosition;
        sphere.transform.localRotation = localRotation;
        ApplyShape(sphere, shape, size, boxSize == Vector3.zero ? DefaultBoxSize(size) : boxSize);

        StowPoint point = sphere.GetComponent<StowPoint>();
        var pointObject = new SerializedObject(point);
        pointObject.FindProperty("sizeClass").enumValueIndex = (int)size;
        pointObject.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon(point);

        var managerObject = new SerializedObject(manager);
        SerializedProperty points = managerObject.FindProperty("stowPoints");
        SerializedProperty keys = managerObject.FindProperty("stowKeys");
        int index = points.arraySize;
        points.arraySize = index + 1;
        points.GetArrayElementAtIndex(index).objectReferenceValue = point;
        keys.arraySize = index + 1;
        keys.GetArrayElementAtIndex(index).intValue = key != 0 ? key : (int)NextFreeKey(keys, index);
        managerObject.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon(manager);
        return point;
    }

    // Gives a pocket the round or upright-box trigger and visual. A sphere keeps its size in the root scale; a box keeps
    // root scale 1 and puts the size in the collider, with the cube visual matching it. Safe to call repeatedly.
    internal static void ApplyShape(GameObject point, StowShape shape, StowSize size, Vector3 boxSize)
    {
        MeshFilter visual = point.GetComponentInChildren<MeshFilter>(true);
        if (shape == StowShape.UprightBox)
        {
            point.transform.localScale = Vector3.one;
            SphereCollider round = point.GetComponent<SphereCollider>();
            if (round != null) Undo.DestroyObjectImmediate(round);
            BoxCollider box = point.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(point);
            box.isTrigger = true;
            box.size = boxSize;
            box.center = Vector3.zero;
            if (visual != null)
            {
                visual.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                visual.transform.localScale = boxSize;
            }
        }
        else
        {
            point.transform.localScale = Vector3.one * SizeScales[(int)size];
            BoxCollider box = point.GetComponent<BoxCollider>();
            if (box != null) Undo.DestroyObjectImmediate(box);
            SphereCollider round = point.GetComponent<SphereCollider>();
            if (round == null) round = Undo.AddComponent<SphereCollider>(point);
            round.isTrigger = true;
            round.radius = 0.06f;
            round.center = Vector3.zero;
            if (visual != null)
            {
                visual.sharedMesh = SphereMesh();
                visual.transform.localScale = Vector3.one * 0.1f;
            }
        }
    }

    // the PlayerStowSphere prefab's own round mesh, so switching a pocket back from a box restores exactly what it had
    private static Mesh SphereMesh()
    {
        string[] guids = AssetDatabase.FindAssets("PlayerStowSphere t:Prefab");
        if (guids.Length > 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            MeshFilter filter = prefab == null ? null : prefab.GetComponentInChildren<MeshFilter>(true);
            if (filter != null && filter.sharedMesh != null) return filter.sharedMesh;
        }

        return Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
    }

    // width, height, depth in metres for an upright box per size class (extra small .. extra large)
    private static readonly Vector3[] BoxSizes =
    {
        new Vector3(0.04f, 0.04f, 0.02f),
        new Vector3(0.12f, 0.22f, 0.12f),
        new Vector3(0.18f, 0.40f, 0.18f),
        new Vector3(0.24f, 0.55f, 0.24f)
    };

    public static Vector3 DefaultBoxSize(StowSize size)
    {
        return BoxSizes[(int)size];
    }

    /// <summary>
    /// Adds a ring or grid of pockets around the player, as a Pocket Layout object whose inspector can adjust it afterwards:
    /// <paramref name="count"/> pockets spread over <paramref name="spreadDegrees"/> of arc centred on <paramref name="centreDegrees"/>
    /// (0 = straight ahead, 90 = the player's right, -90 = their left, 180 = behind), <paramref name="radius"/> metres from the bone,
    /// repeated for <paramref name="rows"/> heights <paramref name="rowSpacing"/> apart. <paramref name="shape"/> UprightBox makes
    /// tall boxes (size from <paramref name="boxSize"/>, or the size class default when zero). Returns the new pockets, or null
    /// if there is no tracker.
    /// </summary>
    public static StowPoint[] AddPocketLayout(PlayerStowPointManager manager, bool chest, int count, int rows, float centreDegrees,
        float spreadDegrees, float radius, float rowSpacing, StowSize size, bool faceOutward, StowLayoutKeys keyMode,
        StowShape shape = StowShape.Sphere, Vector3 boxSize = default(Vector3))
    {
        StowPocketLayout layout = StowPocketLayoutTools.Create(manager, chest, l =>
        {
            l.size = size;
            l.shape = shape;
            l.autoBoxSize = boxSize == Vector3.zero;
            if (boxSize != Vector3.zero) l.boxSize = boxSize;
            l.pocketsPerRow = Mathf.Clamp(count, 1, 8);
            l.rows = Mathf.Clamp(rows, 1, 4);
            l.centreAngle = centreDegrees;
            l.arcWidth = spreadDegrees;
            l.radius = radius;
            l.rowSpacing = rowSpacing;
            l.faceOutward = faceOutward;
            l.keys = keyMode;
        });
        if (layout == null) return null;

        Selection.activeGameObject = layout.gameObject;
        return StowPocketLayoutTools.Pockets(layout).ToArray();
    }

    internal static int FirstKey()
    {
        return (int)NumberKeyOptions[0];
    }

    // lowest number key not in used; with all ten taken, the last key (so the pocket joins that group)
    internal static int NextUnusedKey(HashSet<int> used)
    {
        for (int k = 0; k < NumberKeyOptions.Length; k++)
        {
            if (!used.Contains((int)NumberKeyOptions[k])) return (int)NumberKeyOptions[k];
        }

        return (int)NumberKeyOptions[NumberKeyOptions.Length - 1];
    }

    // the lowest number key no slot uses yet; with all ten taken the new slot joins the last key's group
    private static KeyCode NextFreeKey(SerializedProperty keys, int count)
    {
        var used = new HashSet<int>();
        for (int i = 0; i < count; i++) used.Add(keys.GetArrayElementAtIndex(i).intValue);
        return (KeyCode)NextUnusedKey(used);
    }

    // x -> -x on each stow point's position and orientation (the rotation of a mirrored pose is (x, -y, -z, w))
    private static void MirrorStowPoints(PlayerStowPointManager manager)
    {
        playerTrackingParenting tracker = manager.GetComponentInChildren<playerTrackingParenting>(true);
        Undo.IncrementCurrentGroup();
        foreach (StowPoint point in manager.stowPoints)
        {
            if (point == null) continue;
            Transform t = point.transform;
            Undo.RecordObject(t, "Mirror Stow Points");

            // mirror in the frame of the bone the point hangs from, so a point inside a moved or rotated group still mirrors correctly
            Transform bone = t.parent;
            for (Transform up = t.parent; up != null; up = up.parent)
            {
                if (tracker != null && (up == tracker.hipTransform || up == tracker.TorsoTransform)) { bone = up; break; }
            }

            Vector3 p = bone.InverseTransformPoint(t.position);
            Quaternion q = Quaternion.Inverse(bone.rotation) * t.rotation;
            t.position = bone.TransformPoint(new Vector3(-p.x, p.y, p.z));
            t.rotation = bone.rotation * new Quaternion(q.x, -q.y, -q.z, q.w);
        }
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
    }

    private static void DrawNumberKeyPopup(SerializedProperty stowKey)
    {
        int currentValue = stowKey.intValue;
        KeyCode currentKey = (KeyCode)currentValue;

        int selectedIndex = 0;
        for (int i = 0; i < NumberKeyOptions.Length; i++)
        {
            if (NumberKeyOptions[i] == currentKey)
            {
                selectedIndex = i;
                break;
            }
        }

        int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, NumberKeyLabels, GUILayout.MaxWidth(150f));
        stowKey.intValue = (int)NumberKeyOptions[newSelectedIndex];
    }
}

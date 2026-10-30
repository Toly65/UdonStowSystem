using UnityEditor;
using UnityEngine;

// The Pocket Layout object's inspector: the same controls as the Pocket Layout foldout on the stow manager, but live.
// Changing a value rebuilds the pockets right away (adding or removing pockets and their manager slots as needed).
[CustomEditor(typeof(StowPocketLayout))]
public class StowPocketLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var layout = (StowPocketLayout)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox("Changing any value below rebuilds this layout's pockets. Moving a single pocket by hand is overwritten the next time you change a value here, so use Detach once the layout is how you want it.", MessageType.None);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("size"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shape"));
        if (layout.shape == StowShape.UprightBox)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoBoxSize"), new GUIContent("Box size from Size", "On: width, height and depth come from the size class. Off: set them yourself."));
            if (layout.autoBoxSize)
            {
                Vector3 auto = PlayerStowPointManagerEditor.DefaultBoxSize(layout.size);
                EditorGUILayout.LabelField("Box (W x H x D)", (auto.x * 100f) + " x " + (auto.y * 100f) + " x " + (auto.z * 100f) + " cm");
            }
            else
            {
                SerializedProperty box = serializedObject.FindProperty("boxSize");
                Vector3 v = box.vector3Value;
                v.x = EditorGUILayout.Slider("Width (m)", v.x, 0.02f, 0.4f);
                v.y = EditorGUILayout.Slider("Height (m)", v.y, 0.04f, 0.8f);
                v.z = EditorGUILayout.Slider("Depth (m)", v.z, 0.02f, 0.4f);
                box.vector3Value = v;
            }
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("pocketsPerRow"), new GUIContent("Pockets per row"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rows"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("centreAngle"), new GUIContent("Centre angle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("arcWidth"), new GUIContent("Arc width"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("radius"), new GUIContent("Radius (m)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rowSpacing"), new GUIContent("Row spacing (m)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("faceOutward"), new GUIContent("Face outward"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keys"), new GUIContent("Keys", "Changing this reassigns the pockets' keys. Keys you set by hand are kept until you change this, the pocket count or the rows."));
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();
        if (changed) StowPocketLayoutTools.Rebuild(layout);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Mirror", "Flips the layout to the other side: the centre angle changes sign. Right hip becomes left hip.")))
        {
            Undo.RecordObject(layout, "Mirror Pocket Layout");
            layout.centreAngle = -layout.centreAngle;
            StowPocketLayoutTools.Rebuild(layout);
        }

        if (GUILayout.Button(new GUIContent("Detach", "Keeps the pockets exactly as they are and removes this settings component, so they can be moved by hand without being overwritten.")))
        {
            Undo.DestroyObjectImmediate(layout);
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button(new GUIContent("Delete Layout", "Removes every pocket in this layout and their slots from the stow manager.")))
        {
            if (EditorUtility.DisplayDialog("Delete Pocket Layout", "Remove this layout's " + StowPocketLayoutTools.Pockets(layout).Count + " pockets and their stow manager slots?", "Delete", "Cancel"))
            {
                StowPocketLayoutTools.Delete(layout);
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}

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

    private void OnEnable()
    {
        stowPointsProp = serializedObject.FindProperty("stowPoints");
        stowKeysProp = serializedObject.FindProperty("stowKeys");
        desktopDisplayDistanceProp = serializedObject.FindProperty("desktopDisplayDistance");
        selectionNoiseProp = serializedObject.FindProperty("selectionNoise");
        vrPlayerTrackingConnectorProp = serializedObject.FindProperty("vrPlayerTrackingConnector");
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
            "vrPlayerTrackingConnector"
        );

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stow Mapping", EditorStyles.boldLabel);

        int slotCount = Mathf.Max(stowPointsProp.arraySize, stowKeysProp.arraySize);
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
            EditorGUILayout.PropertyField(stowPoint, new GUIContent("Slot " + i));
            DrawNumberKeyPopup(stowKey);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Desktop Display", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(desktopDisplayDistanceProp, new GUIContent("Display Distance"));
        EditorGUILayout.PropertyField(selectionNoiseProp, new GUIContent("Selection Audio"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(vrPlayerTrackingConnectorProp, new GUIContent("VR Tracking Connector", "This object is enabled in VR and disabled for desktop users."));

        serializedObject.ApplyModifiedProperties();
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

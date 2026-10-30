using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// Builds and rebuilds the pockets of a StowPocketLayout. Rebuild is idempotent: it reuses the pockets already under the
// layout object, adds or removes only the difference, and re-places every one from the settings.
public static class StowPocketLayoutTools
{
    /// <summary>Creates a layout object on the hip or chest bone, fills its settings through <paramref name="configure"/>, and builds the pockets.</summary>
    public static StowPocketLayout Create(PlayerStowPointManager manager, bool chest, System.Action<StowPocketLayout> configure)
    {
        Transform bone = PlayerStowPointManagerEditor.FindBone(manager, chest);
        if (bone == null) return null;

        Undo.IncrementCurrentGroup();
        var group = new GameObject("Pocket Layout (" + (chest ? "Chest" : "Hips") + ")");
        Undo.RegisterCreatedObjectUndo(group, "Add Pocket Layout");
        group.transform.SetParent(bone, false);

        StowPocketLayout layout = Undo.AddComponent<StowPocketLayout>(group);
        layout.chest = chest;
        if (configure != null) configure(layout);
        Rebuild(layout);
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        return layout;
    }

    /// <summary>The pockets under the layout, in build order (row by row).</summary>
    public static List<StowPoint> Pockets(StowPocketLayout layout)
    {
        var list = new List<StowPoint>();
        foreach (Transform child in layout.transform)
        {
            StowPoint point = child.GetComponent<StowPoint>();
            if (point != null) list.Add(point);
        }

        return list;
    }

    public static void Rebuild(StowPocketLayout layout)
    {
        PlayerStowPointManager manager = layout.GetComponentInParent<PlayerStowPointManager>(true);
        if (manager == null) return;

        int wanted = Mathf.Max(1, layout.pocketsPerRow) * Mathf.Max(1, layout.rows);
        List<StowPoint> pockets = Pockets(layout);

        // too many: remove the surplus from the end, and their slots from the manager
        while (pockets.Count > wanted)
        {
            StowPoint extra = pockets[pockets.Count - 1];
            pockets.RemoveAt(pockets.Count - 1);
            RemoveSlot(manager, extra);
            Undo.DestroyObjectImmediate(extra.gameObject);
        }

        // too few: add more at the end
        while (pockets.Count < wanted)
        {
            StowPoint added = PlayerStowPointManagerEditor.CreatePoint(manager, layout.transform, Vector3.zero, Quaternion.identity, layout.size, 0);
            if (added == null) return;
            pockets.Add(added);
        }

        bool rekey = layout.keys != layout.appliedKeys || layout.pocketsPerRow != layout.appliedCount || layout.rows != layout.appliedRows;
        int count = Mathf.Max(1, layout.pocketsPerRow);
        int rows = Mathf.Max(1, layout.rows);
        Vector3 box = layout.autoBoxSize ? PlayerStowPointManagerEditor.DefaultBoxSize(layout.size) : layout.boxSize;

        for (int row = 0; row < rows; row++)
        {
            float y = (row - (rows - 1) * 0.5f) * layout.rowSpacing;
            for (int i = 0; i < count; i++)
            {
                StowPoint point = pockets[row * count + i];
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float angle = (layout.centreAngle + layout.arcWidth * (t - 0.5f)) * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)); // 0 = +Z (front), 90 = +X (right)

                Transform t2 = point.transform;
                Undo.RecordObject(t2, "Edit Pocket Layout");
                t2.localPosition = outward * layout.radius + Vector3.up * y;
                t2.localRotation = layout.faceOutward ? Quaternion.LookRotation(outward, Vector3.up) : Quaternion.identity;
                point.name = "Pocket " + (row * count + i + 1);

                var pointObject = new SerializedObject(point);
                pointObject.FindProperty("sizeClass").enumValueIndex = (int)layout.size;
                pointObject.ApplyModifiedProperties();
                UdonSharpEditorUtility.CopyProxyToUdon(point);
                PlayerStowPointManagerEditor.ApplyShape(point.gameObject, layout.shape, layout.size, box);
            }
        }

        if (rekey) AssignKeys(manager, pockets, count, layout.keys);
        layout.appliedKeys = layout.keys;
        layout.appliedCount = layout.pocketsPerRow;
        layout.appliedRows = layout.rows;
        EditorUtility.SetDirty(layout);
    }

    // One key for all / per row / per pocket. The first pocket keeps its current key; the rest take the lowest number key
    // that no pocket outside this layout (or earlier in it) already uses.
    private static void AssignKeys(PlayerStowPointManager manager, List<StowPoint> pockets, int perRow, StowLayoutKeys mode)
    {
        var managerObject = new SerializedObject(manager);
        SerializedProperty points = managerObject.FindProperty("stowPoints");
        SerializedProperty keys = managerObject.FindProperty("stowKeys");

        var used = new HashSet<int>();
        var inLayout = new HashSet<StowPoint>(pockets);
        for (int i = 0; i < points.arraySize && i < keys.arraySize; i++)
        {
            if (!inLayout.Contains(points.GetArrayElementAtIndex(i).objectReferenceValue as StowPoint)) used.Add(keys.GetArrayElementAtIndex(i).intValue);
        }

        int firstSlot = SlotOf(points, pockets[0]);
        int current = firstSlot >= 0 && firstSlot < keys.arraySize ? keys.GetArrayElementAtIndex(firstSlot).intValue : PlayerStowPointManagerEditor.FirstKey();
        used.Add(current);

        int rowKey = current;
        for (int i = 0; i < pockets.Count; i++)
        {
            int key;
            if (mode == StowLayoutKeys.OneKeyForAll)
            {
                key = current;
            }
            else if (mode == StowLayoutKeys.OneKeyPerRow)
            {
                if (i > 0 && i % perRow == 0)
                {
                    rowKey = PlayerStowPointManagerEditor.NextUnusedKey(used);
                    used.Add(rowKey);
                }
                key = rowKey;
            }
            else
            {
                key = current;
                if (i > 0)
                {
                    key = PlayerStowPointManagerEditor.NextUnusedKey(used);
                    used.Add(key);
                }
            }

            int slot = SlotOf(points, pockets[i]);
            if (slot >= 0 && slot < keys.arraySize) keys.GetArrayElementAtIndex(slot).intValue = key;
        }

        managerObject.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon(manager);
    }

    private static int SlotOf(SerializedProperty points, StowPoint point)
    {
        for (int i = 0; i < points.arraySize; i++)
        {
            if (points.GetArrayElementAtIndex(i).objectReferenceValue == point) return i;
        }

        return -1;
    }

    private static void RemoveSlot(PlayerStowPointManager manager, StowPoint point)
    {
        var managerObject = new SerializedObject(manager);
        SerializedProperty points = managerObject.FindProperty("stowPoints");
        SerializedProperty keys = managerObject.FindProperty("stowKeys");
        int slot = SlotOf(points, point);
        if (slot < 0) return;

        points.GetArrayElementAtIndex(slot).objectReferenceValue = null;
        points.DeleteArrayElementAtIndex(slot);
        if (slot < keys.arraySize) keys.DeleteArrayElementAtIndex(slot);
        managerObject.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon(manager);
    }

    /// <summary>Removes the whole layout: its pockets, their manager slots and the layout object.</summary>
    public static void Delete(StowPocketLayout layout)
    {
        PlayerStowPointManager manager = layout.GetComponentInParent<PlayerStowPointManager>(true);
        Undo.IncrementCurrentGroup();
        if (manager != null)
        {
            foreach (StowPoint point in Pockets(layout)) RemoveSlot(manager, point);
        }

        Undo.DestroyObjectImmediate(layout.gameObject);
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
    }
}

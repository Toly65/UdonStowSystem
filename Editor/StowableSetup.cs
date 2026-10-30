using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

// Right-click a VRC Pickup > Make Stowable. Builds what StowableManager needs: a manager on an object that belongs to
// this pickup alone, a StowablePickup on the pickup, and the references between them.
public static class StowableSetup
{
    [MenuItem("CONTEXT/VRCPickup/Make Stowable")]
    private static void MakeStowableMenu(MenuCommand command)
    {
        MakeStowable(((Component)command.context).GetComponent<VRC_Pickup>());
    }

    [MenuItem("CONTEXT/VRCPickup/Make Stowable", true)]
    private static bool MakeStowableValid(MenuCommand command)
    {
        return ((Component)command.context).GetComponentInParent<StowableManager>() == null;
    }

    public static void MakeStowable(VRC_Pickup pickup)
    {
        if (pickup == null) return;

        if (pickup.GetComponentInParent<StowableManager>() != null)
        {
            EditorUtility.DisplayDialog("Make Stowable", pickup.name + " is already under a StowableManager.", "OK");
            return;
        }

        Transform parent = pickup.transform.parent;
        // the manager shows and hides its whole object, so a parent only works if this is the one pickup in it
        bool parentUsable = parent != null && parent.GetComponentsInChildren<VRC_Pickup>(true).Length == 1;
        bool inPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(pickup.gameObject) && !PrefabUtility.IsAnyPrefabInstanceRoot(pickup.gameObject);

        const string warning = "The stow system hides and shows the item through a StowableManager on a PARENT of the pickup, "
            + "so that parent has to belong to this pickup alone. Never point it at a container that also holds other items, "
            + "or stowing one would hide them all.\n\n";

        bool createParent;
        if (parentUsable && !inPrefabInstance)
        {
            int choice = EditorUtility.DisplayDialogComplex("Make Stowable",
                warning + "'" + parent.name + "' holds only this pickup, so it can be used as the stowable root.",
                "Use '" + parent.name + "'", "Cancel", "Create New Parent");
            if (choice == 1) return;
            createParent = choice == 2;
        }
        else
        {
            string why = inPrefabInstance ? "This pickup is inside a prefab instance, so its parent can't be changed here. Unpack the prefab or make the pickup itself the prefab root first."
                : parent == null ? "This pickup has no parent." : "'" + parent.name + "' also holds other pickups.";
            if (inPrefabInstance)
            {
                EditorUtility.DisplayDialog("Make Stowable", warning + why, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Make Stowable", warning + why + " A new parent will be created for this pickup.", "Create New Parent", "Cancel")) return;
            createParent = true;
        }

        Build(pickup, createParent);
    }

    /// <summary>Makes <paramref name="pickup"/> stowable without asking. <paramref name="createParent"/> wraps it in a new parent that the manager goes on; otherwise its current parent is used, which must belong to this pickup alone.</summary>
    public static StowableManager Build(VRC_Pickup pickup, bool createParent)
    {
        Transform parent = pickup.transform.parent;
        Undo.IncrementCurrentGroup();
        GameObject root;
        if (createParent)
        {
            root = new GameObject(pickup.name + " (Stowable)");
            Undo.RegisterCreatedObjectUndo(root, "Make Stowable");
            root.transform.SetParent(pickup.transform.parent, false);
            root.transform.SetSiblingIndex(pickup.transform.GetSiblingIndex());
            root.transform.SetPositionAndRotation(pickup.transform.position, pickup.transform.rotation);
            Undo.SetTransformParent(pickup.transform, root.transform, "Make Stowable");
        }
        else
        {
            root = parent.gameObject;
        }

        StowableManager manager = root.GetComponent<StowableManager>() ?? UdonSharpUndo.AddComponent<StowableManager>(root);
        StowablePickup stowablePickup = pickup.GetComponent<StowablePickup>() ?? UdonSharpUndo.AddComponent<StowablePickup>(pickup.gameObject);

        var managerObject = new SerializedObject(manager);
        managerObject.FindProperty("pickupVisualRoot").objectReferenceValue = pickup.gameObject;
        managerObject.FindProperty("stowablePickup").objectReferenceValue = stowablePickup;
        managerObject.ApplyModifiedProperties();

        var pickupObject = new SerializedObject(stowablePickup);
        pickupObject.FindProperty("manager").objectReferenceValue = manager;
        pickupObject.ApplyModifiedProperties();

        UdonSharpEditorUtility.CopyProxyToUdon(manager);
        UdonSharpEditorUtility.CopyProxyToUdon(stowablePickup);
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

        Selection.activeGameObject = root;
        Debug.Log("[Stow] " + pickup.name + " is stowable. Set its Stow Size (and optionally Stow Icon / Stow Name) on the StowableManager of '" + root.name + "'.", root);
        return manager;
    }
}

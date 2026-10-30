
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerStowPointManager : UdonSharpBehaviour
{
    [Header("Stow Mapping")]
    public StowPoint[] stowPoints;
    [Tooltip("Slots mapped to the same key form a group. Pressing the key cycles through the group's occupied slots.")]
    public int[] stowKeys;
    [Tooltip("Presses of a group's key closer together than this advance to the next slot instead of restarting at the first.")]
    [SerializeField] private float cycleWindowSeconds = 1.2f;
    [Tooltip("Desktop: how long a slot stays shown in front of the player after its key is released, so you can tap the key and then click the item. 0 = only while the key is held.")]
    [SerializeField] private float displayLingerSeconds = 0.8f;

    [Header("Layout Menu")]
    [Tooltip("The name a StowLayoutSwitcher menu shows for this layout.")]
    [SerializeField] private string layoutName;
    [Tooltip("Preview picture a StowLayoutSwitcher menu shows for this layout. The Bake Preview button below renders one (needs the Combat System package).")]
    [SerializeField] private Sprite previewImage;

    [Header("Left / Right Handed (VR)")]
    [Tooltip("Remember the player's mirror choice with VRChat Persistence and restore it when they rejoin.")]
    [SerializeField] private bool persistMirror = true;
    [Tooltip("The PlayerData key the choice is saved under (a bool, true = mirrored). Other systems can read it directly with PlayerData.TryGetBool.")]
    [SerializeField] private string mirrorPlayerDataKey = "UdonStow_MirrorLayout";
    [Tooltip("Behaviours told with Mirror Changed Event Name whenever the mirror state changes, for example to update a UI toggle.")]
    [SerializeField] private UdonSharpBehaviour[] mirrorEventTargets;
    [SerializeField] private string mirrorChangedEventName = "OnStowMirrorChanged";

    [Header("Desktop Display")]
    [SerializeField] private float desktopDisplayDistance = 0.3f;
    [SerializeField] private AudioSource selectionNoise;

    [Header("Performance")]
    [SerializeField] private GameObject vrPlayerTrackingConnector;
    [SerializeField] private GameObject desktopKeyPollerObject;

    private Vector3[] initialPositions;
    private Quaternion[] initialRotations;
    private VRCPlayerApi localPlayer;
    private float avatarSize = 1f;
    private int currentActiveStow = -1;
    private bool itemStowedInCurrentStow;
    private bool displayActive;
    private float[] lastKeyTime; // per group, indexed by the group's first slot
    private int[] cursor;
    private float displayReleaseTime = -1f; // when a released key's display ends, or -1 while the key is held

    [HideInInspector] public int pendingKeyIndex = -1;

    private bool mirrored;
    private Transform[] mirrorBones; // per slot: the hip or chest tracker the point hangs from, VR only

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (localPlayer.IsUserInVR())
        {
            FindMirrorBones();
        }

        if(!localPlayer.IsUserInVR())
        {
            int count = stowPoints != null ? stowPoints.Length : 0;
            initialPositions = new Vector3[count];
            initialRotations = new Quaternion[count];
            lastKeyTime = new float[count];
            cursor = new int[count];
            for (int i = 0; i < count; i++) lastKeyTime[i] = -999f;

            for (int i = 0; i < count; i++)
            {
                StowPoint stow = stowPoints[i];
                if (stow == null)
                {
                    continue;
                }

                Transform stowTransform = stow.transform;
                if (stowTransform.parent != transform)
                {
                    stowTransform.SetParent(transform, true);
                }

                initialPositions[i] = stowTransform.localPosition;
                initialRotations[i] = stowTransform.localRotation;
            }
        }
        UpdateConnectorActiveState();
        RefreshLocalAvatarHeight();
    }

    public void PostLateUpdate()
    {
        if (!displayActive || currentActiveStow < 0 || currentActiveStow >= stowPoints.Length)
        {
            return;
        }

        if (displayReleaseTime >= 0f && Time.time >= displayReleaseTime)
        {
            StopCurrentDisplay();
            return;
        }

        StowPoint stow = stowPoints[currentActiveStow];
        if (stow == null)
        {
            StopCurrentDisplay();
            return;
        }

        if (itemStowedInCurrentStow)
        {
            if (!stow.GetItemLockState())
            {
                StopCurrentDisplay();
                return;
            }

            DisplayStowInFront(currentActiveStow);
            return;
        }

        DisplayStowInHand(currentActiveStow);

        if (localPlayer == null)
        {
            return;
        }

        VRC_Pickup pickup = localPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        if (pickup != null)
        {
            stow.ForceItemLock(pickup);
            itemStowedInCurrentStow = stow.GetItemLockState();
        }
    }

    public void OnDesktopKeyDown()
    {
        int pressed = pendingKeyIndex;
        pendingKeyIndex = -1;

        // the poller fires once per slot, so a shared key arrives several times: only the group's first slot acts
        if (pressed < 0 || stowPoints == null || pressed >= stowPoints.Length || GetGroupFirst(pressed) != pressed)
        {
            return;
        }

        int key = stowKeys[pressed];
        VRC_Pickup heldPickup = GetHeldPickup();
        if (heldPickup != null)
        {
            StopCurrentDisplay();
            for (int j = pressed; j < stowPoints.Length && j < stowKeys.Length; j++)
            {
                StowPoint stow = stowPoints[j];
                if (stowKeys[j] != key || stow == null || stow.GetItemLockState())
                {
                    continue;
                }

                stow.ForceItemLockDesktop(heldPickup);
                if (stow.GetItemLockState())
                {
                    cursor[pressed] = j; // the group's key now opens on what was just stowed
                    break; // a slot that is too small leaves the item held, so the next slot gets a try
                }
            }
            return;
        }

        ShowStow(NextInGroup(pressed, key));
    }

    public void OnDesktopKeyUp()
    {
        int pressed = pendingKeyIndex;
        pendingKeyIndex = -1;

        if (!displayActive || pressed < 0 || stowKeys == null || pressed >= stowKeys.Length
            || currentActiveStow >= stowKeys.Length)
        {
            return;
        }

        if (stowKeys[currentActiveStow] == stowKeys[pressed])
        {
            if (displayLingerSeconds > 0f)
            {
                displayReleaseTime = Time.time + displayLingerSeconds;
            }
            else
            {
                StopCurrentDisplay();
            }
        }
    }

    private int GetGroupFirst(int index)
    {
        if (stowKeys == null || index >= stowKeys.Length)
        {
            return -1;
        }

        for (int j = 0; j < index; j++)
        {
            if (stowKeys[j] == stowKeys[index])
            {
                return j;
            }
        }

        return index;
    }

    // Quick re-presses step on to the next occupied slot of the group; a fresh press opens on the slot last stowed into or cycled to.
    // A group with nothing stowed shows its first slot, which is the old single-slot behaviour.
    private int NextInGroup(int first, int key)
    {
        int count = stowPoints.Length;
        bool cycling = Time.time - lastKeyTime[first] <= cycleWindowSeconds;
        lastKeyTime[first] = Time.time;
        int start = cycling ? cursor[first] + 1 : cursor[first]; // a fresh press opens on the most recent slot

        for (int step = 0; step < count; step++)
        {
            int j = (start + step) % count;
            if (j < stowKeys.Length && stowKeys[j] == key && stowPoints[j] != null && stowPoints[j].GetItemLockState())
            {
                cursor[first] = j;
                return j;
            }
        }

        cursor[first] = first;
        return first;
    }

    public string GetLayoutName()
    {
        return string.IsNullOrEmpty(layoutName) ? gameObject.name : layoutName;
    }

    public Sprite GetPreviewImage()
    {
        return previewImage;
    }

    public int GetStowPointCount()
    {
        return stowPoints == null ? 0 : stowPoints.Length;
    }

    public StowPoint GetStowPoint(int index)
    {
        if (stowPoints == null || index < 0 || index >= stowPoints.Length)
        {
            return null;
        }

        return stowPoints[index];
    }

    // The slot currently shown in front of the desktop player, or -1.
    public int GetActiveStow()
    {
        return displayActive ? currentActiveStow : -1;
    }

    // Left / right handed layouts (VR only: desktop stow points are shown in front of the camera, not on the body).
    // Mirrors every stow point across the player's centre line, in the frame of the bone it hangs from. Mirroring twice
    // restores the authored layout, and an item stowed in a point follows it. The choice is local to the player.

    public bool IsMirrored()
    {
        return mirrored;
    }

    public void ToggleMirrored()
    {
        SetMirrored(!mirrored);
    }

    // No-argument versions for UI buttons and interact events, which can't pass a bool.
    public void MirrorOn()
    {
        SetMirrored(true);
    }

    public void MirrorOff()
    {
        SetMirrored(false);
    }

    public void SetMirrored(bool value)
    {
        ApplyMirror(value, true);
    }

    private void ApplyMirror(bool value, bool save)
    {
        if (!Utilities.IsValid(localPlayer) || !localPlayer.IsUserInVR() || value == mirrored)
        {
            return;
        }

        FlipStowPoints();
        mirrored = value;

        if (save && persistMirror && !string.IsNullOrEmpty(mirrorPlayerDataKey))
        {
            PlayerData.SetBool(mirrorPlayerDataKey, mirrored);
        }

        if (mirrorEventTargets != null)
        {
            for (int i = 0; i < mirrorEventTargets.Length; i++)
            {
                if (mirrorEventTargets[i] != null)
                {
                    mirrorEventTargets[i].SendCustomEvent(mirrorChangedEventName);
                }
            }
        }
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!persistMirror || !Utilities.IsValid(player) || !player.isLocal || string.IsNullOrEmpty(mirrorPlayerDataKey))
        {
            return;
        }

        bool saved;
        if (PlayerData.TryGetBool(player, mirrorPlayerDataKey, out saved))
        {
            ApplyMirror(saved, false);
        }
    }

    private void FindMirrorBones()
    {
        int count = stowPoints != null ? stowPoints.Length : 0;
        mirrorBones = new Transform[count];
        playerTrackingParenting tracker = GetComponentInChildren<playerTrackingParenting>(true);
        if (tracker == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (stowPoints[i] == null)
            {
                continue;
            }

            // walk up to the tracker bone this point hangs from, so a point inside a moved or rotated layout group still mirrors correctly
            Transform up = stowPoints[i].transform.parent;
            while (up != null && up != tracker.hipTransform && up != tracker.TorsoTransform)
            {
                up = up.parent;
            }

            mirrorBones[i] = up;
        }
    }

    private void FlipStowPoints()
    {
        if (mirrorBones == null)
        {
            return;
        }

        for (int i = 0; i < mirrorBones.Length; i++)
        {
            Transform bone = mirrorBones[i];
            if (bone == null || stowPoints[i] == null)
            {
                continue;
            }

            Transform t = stowPoints[i].transform;
            Vector3 p = bone.InverseTransformPoint(t.position);
            Quaternion q = Quaternion.Inverse(bone.rotation) * t.rotation;
            t.position = bone.TransformPoint(new Vector3(-p.x, p.y, p.z));
            t.rotation = bone.rotation * new Quaternion(q.x, -q.y, -q.z, q.w);
        }
    }

    public int GetMappedKeyCount()
    {
        if (stowKeys == null)
        {
            return 0;
        }

        return stowKeys.Length;
    }

    public int GetMappedKey(int index)
    {
        if (stowKeys == null || index < 0 || index >= stowKeys.Length)
        {
            return (int)KeyCode.None;
        }

        return stowKeys[index];
    }

    private void ShowStow(int stowID)
    {
        RefreshLocalAvatarHeight();
        StopCurrentDisplay();

        currentActiveStow = stowID;
        StowPoint stow = stowPoints[stowID];
        if (stow == null)
        {
            currentActiveStow = -1;
            return;
        }

        itemStowedInCurrentStow = stow.GetItemLockState();
        displayActive = true;

        if (itemStowedInCurrentStow)
        {
            stow.SetDesktopHiddenPickupVisible(true);
        }

        if (selectionNoise != null)
        {
            selectionNoise.Play();
        }
    }

    private void StopCurrentDisplay()
    {
        if (currentActiveStow >= 0)
        {
            ReturnStow(currentActiveStow);
        }

        displayActive = false;
        itemStowedInCurrentStow = false;
        currentActiveStow = -1;
        displayReleaseTime = -1f;
    }

    private void ReturnStow(int stowID)
    {
        if (stowPoints == null || stowID < 0 || stowID >= stowPoints.Length)
        {
            return;
        }

        StowPoint stow = stowPoints[stowID];
        if (stow == null)
        {
            return;
        }

        Transform oldStowPointTransform = stow.transform;
        if (initialPositions != null && stowID < initialPositions.Length)
        {
            oldStowPointTransform.localPosition = initialPositions[stowID];
        }
        if (initialRotations != null && stowID < initialRotations.Length)
        {
            oldStowPointTransform.localRotation = initialRotations[stowID];
        }

        if (stow.GetItemLockState())
        {
            stow.SetDesktopHiddenPickupVisible(false);
            return;
        }

        stow.ReturnToDefaultState();
    }

    private VRC_Pickup GetHeldPickup()
    {
        if (localPlayer == null)
        {
            return null;
        }

        VRC_Pickup pickup = localPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        if (pickup == null)
        {
            pickup = localPlayer.GetPickupInHand(VRC_Pickup.PickupHand.Left);
        }

        return pickup;
    }

    private void DisplayStowInFront(int stowID)
    {
        if (localPlayer == null)
        {
            return;
        }

        StowPoint stow = stowPoints[stowID];
        if (stow == null)
        {
            return;
        }

        Transform stowPointTransform = stow.transform;
        VRCPlayerApi.TrackingData head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 relativePosition = (head.rotation * Vector3.forward).normalized * desktopDisplayDistance * avatarSize;

        stowPointTransform.position = head.position + relativePosition;
        stowPointTransform.rotation = head.rotation;
        stowPointTransform.Rotate(0f, 270f, 0f);
    }

    private void DisplayStowInHand(int stowID)
    {
        if (localPlayer == null)
        {
            return;
        }

        StowPoint stow = stowPoints[stowID];
        if (stow == null)
        {
            return;
        }

        Transform stowPointTransform = stow.transform;
        VRCPlayerApi.TrackingData rightHand = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        stowPointTransform.position = rightHand.position;
        stowPointTransform.rotation = rightHand.rotation;
    }

    private float GetAvatarHeight(VRCPlayerApi player)
    {
        if (player == null)
        {
            return 1f;
        }

        float height = 0f;
        Vector3 position1 = player.GetBonePosition(HumanBodyBones.Head);
        Vector3 position2 = player.GetBonePosition(HumanBodyBones.Neck);
        height += (position1 - position2).magnitude;
        position1 = position2;
        position2 = player.GetBonePosition(HumanBodyBones.Hips);
        height += (position1 - position2).magnitude;
        position1 = position2;
        position2 = player.GetBonePosition(HumanBodyBones.RightLowerLeg);
        height += (position1 - position2).magnitude;
        position1 = position2;
        position2 = player.GetBonePosition(HumanBodyBones.RightFoot);
        height += (position1 - position2).magnitude;

        avatarSize = Mathf.Max(height, 0.1f);

        if (stowPoints != null)
        {
            for (int i = 0; i < stowPoints.Length; i++)
            {
                if (stowPoints[i] != null)
                {
                    stowPoints[i].avatarSize = avatarSize;
                }
            }
        }

        return avatarSize;
    }

    private void RefreshLocalAvatarHeight()
    {
        if (localPlayer == null)
        {
            avatarSize = 1f;
            return;
        }

        GetAvatarHeight(localPlayer);
    }

    private void UpdateConnectorActiveState()
    {
        if (localPlayer == null)
        {
            return;
        }

        bool inVr = localPlayer.IsUserInVR();

        if (vrPlayerTrackingConnector != null)
        {
            vrPlayerTrackingConnector.SetActive(inVr);
        }

        if (desktopKeyPollerObject != null)
        {
            desktopKeyPollerObject.SetActive(!inVr);
        }
    }

    public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
    {
        if (player == null || !player.isLocal)
        {
            return;
        }

        RefreshLocalAvatarHeight();
    }
}


using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerStowPointManager : UdonSharpBehaviour
{
    [Header("Stow Mapping")]
    public StowPoint[] stowPoints;
    public int[] stowKeys;

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

    [HideInInspector] public int pendingKeyIndex = -1;

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if(!localPlayer.IsUserInVR())
        {
            int count = stowPoints != null ? stowPoints.Length : 0;
            initialPositions = new Vector3[count];
            initialRotations = new Quaternion[count];


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
        int stowIndex = pendingKeyIndex;
        pendingKeyIndex = -1;

        if (stowIndex < 0 || stowPoints == null || stowIndex >= stowPoints.Length)
        {
            return;
        }

        StowPoint stow = stowPoints[stowIndex];
        if (stow == null)
        {
            return;
        }

        VRC_Pickup heldPickup = GetHeldPickup();
        if (heldPickup != null)
        {
            StopCurrentDisplay();
            if (!stow.GetItemLockState())
            {
                stow.ForceItemLockDesktop(heldPickup);
            }
            return;
        }

        ShowStow(stowIndex);
    }

    public void OnDesktopKeyUp()
    {
        int stowIndex = pendingKeyIndex;
        pendingKeyIndex = -1;

        if (!displayActive)
        {
            return;
        }

        if (stowIndex == currentActiveStow)
        {
            StopCurrentDisplay();
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

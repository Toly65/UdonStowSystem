
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public enum StowSize
{
    ExtraSmall = 0,
    Small = 1,
    Large = 2,
    ExtraLarge = 3
}

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class StowableManager : UdonSharpBehaviour
{
    [Header("Config")]
    [SerializeField] private StowSize stowSize = StowSize.Small;
    [SerializeField] private GameObject pickupVisualRoot;
    [SerializeField] private StowablePickup stowablePickup;

    [Header("Relayed Events")]
    [SerializeField] private UdonSharpBehaviour[] eventTargets;
    [SerializeField] private string onPickupEventName = "OnManagedPickup";
    [SerializeField] private string onDropEventName = "OnManagedDrop";
    [SerializeField] private string onStowedEventName = "OnManagedStowed";
    [SerializeField] private string onUnstowedEventName = "OnManagedUnstowed";

    // Does this item exist in the world right now? An owning system (pool, spawner,
    // inventory) sets it; stow state can never override it. Distinct from the pickup's
    // pickupable flag, which is about who is allowed to grab an item that does exist.
    // Owning systems should call SetItemSpawned(false) from Awake so the pickup never
    // flashes visible before Start decides its state.
    private bool itemSpawned = true;

    [UdonSynced, FieldChangeCallback(nameof(IsStowedSynced))]
    private bool _isStowedSynced;

    public bool IsStowedSynced
    {
        get { return _isStowedSynced; }
        set
        {
            _isStowedSynced = value;
            ApplyPickupState();
        }
    }

    private void Start()
    {
        if (stowablePickup == null)
        {
            stowablePickup = GetComponentInChildren<StowablePickup>();
        }

        if (stowablePickup != null)
        {
            stowablePickup.SetManager(this);
        }

        ApplyPickupState();
    }

    public override void OnDeserialization()
    {
        ApplyPickupState();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ApplyPickupState();
    }

    public int GetSizeClass()
    {
        return (int)stowSize;
    }

    public void OnPickupRelayed()
    {
        SetStowedState(false);
        RelayEvent(onPickupEventName);
    }

    public void OnDropRelayed()
    {
        RelayEvent(onDropEventName);
    }

    public void SetItemSpawned(bool spawned)
    {
        itemSpawned = spawned;
        ApplyPickupState();
    }

    // False means an owning system has despawned/pooled this item. Hiding the visuals
    // for stow or ownership reasons does NOT clear this - stow points rely on the
    // difference to tell "despawned" from "merely hidden".
    public bool IsItemSpawned()
    {
        return itemSpawned;
    }

    // The stow point that has this item locked on THIS client. Stow points are
    // local-only, so every client tracks its own.
    private StowPoint localStowPoint;

    public void RegisterStowPoint(StowPoint point)
    {
        localStowPoint = point;
    }

    // Call this from any despawn/return/pool system before reclaiming the item, so
    // every client's stow point lets go instead of dragging it back next frame.
    public void ForceUnstowEverywhere()
    {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(ForceUnstowLocal));
    }

    // Local release only - no ownership grab, so a mass despawn doesn't make every
    // client fight over the object.
    public void ForceUnstowLocal()
    {
        if (localStowPoint != null)
        {
            StowPoint point = localStowPoint;
            localStowPoint = null;
            point.ForceReleaseItemLock();
        }

        if (Networking.IsOwner(gameObject))
        {
            SetStowedState(false);
        }
    }

    // Clears stale stowed state on respawn. No RequestSerialization - the caller
    // bundles this into its own sync.
    public void ResetStowedState()
    {
        IsStowedSynced = false;
    }

    public void MarkStowed()
    {
        SetStowedState(true);
    }

    public void MarkUnstowed()
    {
        SetStowedState(false);
    }

    private void SetStowedState(bool isStowed)
    {
        if (IsStowedSynced == isStowed)
        {
            return;
        }

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && !Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(localPlayer, gameObject);
        }

        IsStowedSynced = isStowed;
        RequestSerialization();

        RelayEvent(isStowed ? onStowedEventName : onUnstowedEventName);
    }

    private void ApplyPickupState()
    {
        // Despawned - force everything off, ignore stow/owner state.
        if (!itemSpawned)
        {
            SetPickupable(false);
            if (pickupVisualRoot != null && pickupVisualRoot.activeSelf)
            {
                pickupVisualRoot.SetActive(false);
            }
            return;
        }

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        // Only the owner may grab a stowed item; remote players can't steal it.
        bool ownedLocally = localPlayer == null || Networking.IsOwner(gameObject);
        bool shouldBeVisible = !IsStowedSynced || ownedLocally;
        SetPickupable(shouldBeVisible);

        if (pickupVisualRoot != null && pickupVisualRoot.activeSelf != shouldBeVisible)
        {
            pickupVisualRoot.SetActive(shouldBeVisible);
        }
    }

    private void SetPickupable(bool value)
    {
        if (stowablePickup != null)
        {
            stowablePickup.SetPickupable(value);
        }
    }

    private void RelayEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName) || eventTargets == null)
        {
            return;
        }

        for (int i = 0; i < eventTargets.Length; i++)
        {
            UdonSharpBehaviour target = eventTargets[i];
            if (target != null)
            {
                target.SendCustomEvent(eventName);
            }
        }
    }
}

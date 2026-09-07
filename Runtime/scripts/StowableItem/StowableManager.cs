
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

    // External owner (e.g. ActiveItem) decides whether the pickup exists at all.
    // When false, the pickup is forced off regardless of stow/owner state.
    // When true, stow state controls its active state.
    private bool pickupEnabled = true;

    [UdonSynced, FieldChangeCallback(nameof(IsStowedSynced))]
    private bool _isStowedSynced;

    public bool IsStowedSynced
    {
        get { return _isStowedSynced; }
        set
        {
            _isStowedSynced = value;
            ApplyPickupVisibility();
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

        ApplyPickupVisibility();
    }

    public override void OnDeserialization()
    {
        ApplyPickupVisibility();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ApplyPickupVisibility();
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

    // Called by the item's owning script (e.g. ActiveItem) to declare whether the
    // pickup should exist at all. When disabled, stow/owner logic can't re-enable it.
    public void SetPickupEnabled(bool enabled)
    {
        pickupEnabled = enabled;
        ApplyPickupVisibility();
    }

    // Called from an owner script's Awake, before this Start runs, so the pickup
    // never flashes visible before the owner decides its state.
    public void SetExternallyControlled()
    {
        pickupEnabled = false;
        ApplyPickupVisibility();
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

    private void ApplyPickupVisibility()
    {
        // Owner script says this pickup shouldn't exist right now — force off, ignore stow/owner state.
        if (!pickupEnabled)
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

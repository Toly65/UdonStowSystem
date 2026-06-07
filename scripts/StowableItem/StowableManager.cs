
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
        if (pickupVisualRoot == null)
        {
            return;
        }

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null)
        {
            pickupVisualRoot.SetActive(true);
            return;
        }

        // Keep visuals for owner, hide for non-owners when stowed.
        bool shouldBeVisible = !IsStowedSynced || Networking.IsOwner(gameObject);
        if (pickupVisualRoot.activeSelf != shouldBeVisible)
        {
            pickupVisualRoot.SetActive(shouldBeVisible);
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

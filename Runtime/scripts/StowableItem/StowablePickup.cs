
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]

public class StowablePickup : UdonSharpBehaviour
{
    [SerializeField] private StowableManager manager;
    private VRC_Pickup pickup;

    private void Start()
    {
        if (manager == null)
        {
            manager = GetComponentInParent<StowableManager>();
        }

        pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
    }

    // Called by the manager so remote players can't grab a stowed item.
    public void SetPickupable(bool value)
    {
        if (pickup == null)
        {
            pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
        }

        if (pickup != null && pickup.pickupable != value)
        {
            pickup.pickupable = value;
        }
    }

    public override void OnPickup()
    {
        if (manager != null)
        {
            manager.OnPickupRelayed();
        }
    }

    public override void OnDrop()
    {
        if (manager != null)
        {
            manager.OnDropRelayed();
        }
    }

    public void SetManager(StowableManager newManager)
    {
        manager = newManager;
    }
}

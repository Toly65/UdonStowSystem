
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StowPointAttachmentPoint : UdonSharpBehaviour
{
    [SerializeField] private StowPoint stowPoint;

    private VRC_Pickup trackedPickup;
    private bool receptive;
    private bool itemLocked;

    private void Start()
    {
        if (stowPoint == null)
        {
            stowPoint = GetComponentInParent<StowPoint>();
        }
    }

    private void PostLateUpdate()
    {
        if (trackedPickup == null)
        {
            return;
        }

        if (!itemLocked)
        {
            if (receptive && !trackedPickup.IsHeld)
            {
                itemLocked = true;
                receptive = false;
                if (stowPoint != null)
                {
                    stowPoint.OnAttachmentLocked(trackedPickup);
                }
            }
            return;
        }

        if (trackedPickup.IsHeld)
        {
            itemLocked = false;
            receptive = true;
            if (stowPoint != null)
            {
                stowPoint.OnAttachmentUnlocked(trackedPickup);
            }
            return;
        }

        trackedPickup.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

    public void BeginReceptive(VRC_Pickup pickup)
    {
        trackedPickup = pickup;
        receptive = pickup != null;
        itemLocked = false;
    }

    public void BeginLocked(VRC_Pickup pickup)
    {
        trackedPickup = pickup;
        receptive = false;
        itemLocked = pickup != null;
    }

    public void ClearTrackedPickup()
    {
        trackedPickup = null;
        receptive = false;
        itemLocked = false;
    }

    public bool IsLocked()
    {
        return itemLocked;
    }

    public bool IsReceptive()
    {
        return receptive;
    }
}

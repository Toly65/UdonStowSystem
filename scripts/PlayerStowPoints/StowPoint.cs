
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StowPoint : UdonSharpBehaviour
{
    [Header("config")]
    [SerializeField] private bool onlyAcceptStowablePickups;
    [SerializeField] private StowSize sizeClass = StowSize.Small;
    [SerializeField] private StowPointAttachmentPoint attachmentPoint;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material DefaultMaterial;
    [SerializeField] private Material ActiveMaterial;

    [Header("this may be managed by a stow Manager instead")]
    [SerializeField] private bool onlyRecieveItemsWithStowSettings;
    //[SerializeField] private Material DuplicationMaterial;
    private bool itemlocked;
    private bool receptive;
    private VRC_Pickup recievabePickup;
    private VRC_Pickup hiddenDesktopPickup;
    private VRCPlayerApi localplayer;
    private Rigidbody trackedPickupRigidbody;
    private bool trackedPhysicsStateValid;
    private bool trackedWasKinematic;
    private bool trackedUsedGravity;
    [HideInInspector] public float avatarSize = 1;
    private void Start()
    {
        localplayer = Networking.LocalPlayer;

        if (attachmentPoint == null)
        {
            attachmentPoint = GetComponentInChildren<StowPointAttachmentPoint>();
        }

        if (attachmentPoint != null)
        {
            attachmentPoint.gameObject.SetActive(false);
        }
    }

    VRC_Pickup interactingPickup; //re-usable variable because shitty VRChat garbage collection
    private void OnTriggerEnter(Collider other)
    {
        if (itemlocked || receptive || localplayer == null) return;
        
        interactingPickup = other.GetComponent<VRC_Pickup>(); //pickup typically has collider
        if (interactingPickup != null && interactingPickup.IsHeld)
        {
            VRC_Pickup.PickupHand hand = VRC_Pickup.PickupHand.None;
            if (interactingPickup == localplayer.GetPickupInHand(VRC_Pickup.PickupHand.Right))
                hand = VRC_Pickup.PickupHand.Right;
            else if (interactingPickup == localplayer.GetPickupInHand(VRC_Pickup.PickupHand.Left))
                hand = VRC_Pickup.PickupHand.Left;

            if (hand != VRC_Pickup.PickupHand.None)
            {
                BecomeReceptive(interactingPickup, hand);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (receptive && recievabePickup != null)
        {
            VRC_Pickup pickup = other.GetComponentInParent<VRC_Pickup>();
            if (pickup == recievabePickup)
            {
                ReturnToDefaultState();
            }
        }
    }

    private void BecomeReceptive(VRC_Pickup pickup, VRC_Pickup.PickupHand hand)
    {
        recievabePickup = pickup;
        if(recievabePickup)
        {
            StowableManager stowableManager = null;
            bool isStowablePickup = TryGetStowableManager(recievabePickup, ref stowableManager);
            if (onlyAcceptStowablePickups && !isStowablePickup)
            {
                recievabePickup = null;
                return;
            }

            int pickupSizeClass = 0;
            bool hasStowMetadata = TryGetPickupSizeClass(recievabePickup, ref pickupSizeClass);
            if (onlyRecieveItemsWithStowSettings && !hasStowMetadata)
            {
                recievabePickup = null;
                return;
            }
            if (hasStowMetadata)
            {
                //check the size
                if (pickupSizeClass > (int)sizeClass)
                {
                    //size too big, ignore
                    recievabePickup = null;
                    return;
                }
            }
            //material swap
            targetRenderer.material = ActiveMaterial;
            //haptics    
            localplayer.PlayHapticEventInHand(hand, 0.1f, 1, 1);
            receptive = true;
            if (attachmentPoint != null)
            {
                attachmentPoint.BeginReceptive(recievabePickup);
            }
            UpdateAttachmentPointActiveState();
        }

    }
    public void ReturnToDefaultState()
    {
        RestoreHiddenDesktopPickup();
        RestoreTrackedPickupPhysicsState();

        targetRenderer.material = DefaultMaterial;
        receptive = false;
        recievabePickup = null;
        itemlocked = false;

        if (attachmentPoint != null)
        {
            attachmentPoint.ClearTrackedPickup();
        }

        UpdateAttachmentPointActiveState();
    }

    public void ForceItemLock(VRC_Pickup pickup)
    {
        ForceItemLockInternal(pickup, false);
    }

    public void ForceItemLockDesktop(VRC_Pickup pickup)
    {
        ForceItemLockInternal(pickup, true);
    }

    private void ForceItemLockInternal(VRC_Pickup pickup, bool disablePickupObject)
    {
        StowableManager stowableManager = null;
        bool isStowablePickup = TryGetStowableManager(pickup, ref stowableManager);
        if (onlyAcceptStowablePickups && !isStowablePickup)
        {
            return;
        }

        int pickupSizeClass = 0;
        bool hasStowMetadata = TryGetPickupSizeClass(pickup, ref pickupSizeClass);
        if (onlyRecieveItemsWithStowSettings && !hasStowMetadata)
        {
            return;
        }
        if (hasStowMetadata)
        {
            
            //check the size
            if (pickupSizeClass > (int)sizeClass)
            {
                //size too big, ignore
                return;
            }
            
        }
        pickup.Drop();
        recievabePickup = pickup;
        if (disablePickupObject && pickup.gameObject.activeSelf)
        {
            hiddenDesktopPickup = pickup;
            pickup.gameObject.SetActive(false);
        }
        lockItem(pickup);
    }
    public void ForceReleaseItemLock()
    {
        RestoreHiddenDesktopPickup();
        RestoreTrackedPickupPhysicsState();
        recievabePickup = null;
        itemlocked = false;
        receptive = false;
        targetRenderer.material = DefaultMaterial;

        if (attachmentPoint != null)
        {
            attachmentPoint.ClearTrackedPickup();
        }

        UpdateAttachmentPointActiveState();
    }
    private void lockItem(VRC_Pickup pickup)
    {
        CacheAndApplyLockedPhysicsState(pickup);
        
        targetRenderer.material = DefaultMaterial;
        itemlocked = true;
        receptive = false;

        if (attachmentPoint != null)
        {
            attachmentPoint.BeginLocked(pickup);
        }

        UpdateAttachmentPointActiveState();

        StowableManager stowableManager = null;
        TryGetStowableManager(pickup, ref stowableManager);
        if (stowableManager != null)
        {
            stowableManager.MarkStowed();
        }
    }
    public int GetSizeClass()
    {
        return (int)sizeClass;
    }
    public bool GetItemLockState()
    {
        return itemlocked;
    }

    public void SetDesktopHiddenPickupVisible(bool isVisible)
    {
        if (hiddenDesktopPickup == null)
        {
            return;
        }

        if (!itemlocked)
        {
            RestoreHiddenDesktopPickup();
            return;
        }

        GameObject pickupObject = hiddenDesktopPickup.gameObject;
        if (pickupObject.activeSelf == isVisible)
        {
            return;
        }

        pickupObject.SetActive(isVisible);
    }

    public void OnAttachmentLocked(VRC_Pickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        recievabePickup = pickup;
        lockItem(pickup);
    }

    public void OnAttachmentUnlocked(VRC_Pickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        RestoreHiddenDesktopPickup();
        RestoreTrackedPickupPhysicsState();

        recievabePickup = pickup;
        itemlocked = false;
        receptive = true;
        targetRenderer.material = ActiveMaterial;

        StowableManager stowableManager = null;
        TryGetStowableManager(pickup, ref stowableManager);
        if (stowableManager != null)
        {
            stowableManager.MarkUnstowed();
        }

        if (attachmentPoint != null)
        {
            attachmentPoint.BeginReceptive(pickup);
        }

        UpdateAttachmentPointActiveState();
    }

    private void UpdateAttachmentPointActiveState()
    {
        if (attachmentPoint == null)
        {
            return;
        }

        bool shouldRunLoop = recievabePickup != null && (itemlocked || receptive);
        if (attachmentPoint.gameObject.activeSelf != shouldRunLoop)
        {
            attachmentPoint.gameObject.SetActive(shouldRunLoop);
        }
    }

    private bool TryGetPickupSizeClass(VRC_Pickup pickup, ref int outSizeClass)
    {
        if (pickup == null)
        {
            return false;
        }

        StowableManager stowableManager = null;
        TryGetStowableManager(pickup, ref stowableManager);

        if (stowableManager != null)
        {
            outSizeClass = stowableManager.GetSizeClass();
            return true;
        }

        if (pickup.transform.childCount <= 0)
        {
            return false;
        }

        StowSettings settings = pickup.transform.GetChild(0).GetComponent<StowSettings>();
        if (settings == null)
        {
            return false;
        }

        outSizeClass = settings.GetSizeClass();
        return true;
    }

    private bool TryGetStowableManager(VRC_Pickup pickup, ref StowableManager outManager)
    {
        outManager = null;
        if (pickup == null)
        {
            return false;
        }

        outManager = pickup.GetComponent<StowableManager>();
        if (outManager == null)
        {
            outManager = pickup.GetComponentInParent<StowableManager>();
        }

        return outManager != null;
    }

    private void CacheAndApplyLockedPhysicsState(VRC_Pickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        Rigidbody rigidbody = pickup.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = pickup.GetComponentInChildren<Rigidbody>();
        }

        if (rigidbody == null)
        {
            trackedPickupRigidbody = null;
            trackedPhysicsStateValid = false;
            return;
        }

        if (trackedPickupRigidbody != rigidbody)
        {
            RestoreTrackedPickupPhysicsState();
        }

        trackedPickupRigidbody = rigidbody;
        trackedWasKinematic = rigidbody.isKinematic;
        trackedUsedGravity = rigidbody.useGravity;
        trackedPhysicsStateValid = true;

        rigidbody.isKinematic = true;
    }

    private void RestoreTrackedPickupPhysicsState()
    {
        if (!trackedPhysicsStateValid || trackedPickupRigidbody == null)
        {
            trackedPickupRigidbody = null;
            trackedPhysicsStateValid = false;
            return;
        }

        trackedPickupRigidbody.isKinematic = trackedWasKinematic;
        trackedPickupRigidbody.useGravity = trackedUsedGravity;

        trackedPickupRigidbody = null;
        trackedPhysicsStateValid = false;
    }

    private void RestoreHiddenDesktopPickup()
    {
        if (hiddenDesktopPickup == null)
        {
            return;
        }

        if (!hiddenDesktopPickup.gameObject.activeSelf)
        {
            hiddenDesktopPickup.gameObject.SetActive(true);
        }

        hiddenDesktopPickup = null;
    }
}

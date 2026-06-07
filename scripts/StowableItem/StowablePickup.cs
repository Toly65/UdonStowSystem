
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]

public class StowablePickup : UdonSharpBehaviour
{
    [SerializeField] private StowableManager manager;

    private void Start()
    {
        if (manager == null)
        {
            manager = GetComponentInParent<StowableManager>();
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

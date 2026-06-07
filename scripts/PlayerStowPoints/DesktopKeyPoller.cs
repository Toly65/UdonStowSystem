
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DesktopKeyPoller : UdonSharpBehaviour
{
    [SerializeField] private PlayerStowPointManager stowPointManager;
    [SerializeField] private bool disableInVR = true;

    private VRCPlayerApi localPlayer;

    private void Start()
    {
        localPlayer = Networking.LocalPlayer;

        if (disableInVR && localPlayer != null && localPlayer.IsUserInVR())
        {
            gameObject.SetActive(false);
            return;
        }

        if (stowPointManager == null)
        {
            stowPointManager = GetComponent<PlayerStowPointManager>();
        }
    }

    private void Update()
    {
        if (stowPointManager == null)
        {
            return;
        }

        int keyCount = stowPointManager.GetMappedKeyCount();
        for (int i = 0; i < keyCount; i++)
        {
            int keyValue = stowPointManager.GetMappedKey(i);
            if (keyValue == (int)KeyCode.None)
            {
                continue;
            }

            KeyCode key = (KeyCode)keyValue;

            if (Input.GetKeyDown(key))
            {
                stowPointManager.pendingKeyIndex = i;
                stowPointManager.OnDesktopKeyDown();
            }

            if (Input.GetKeyUp(key))
            {
                stowPointManager.pendingKeyIndex = i;
                stowPointManager.OnDesktopKeyUp();
            }
        }
    }
}

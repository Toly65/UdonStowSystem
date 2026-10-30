
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Lets players pick between several stow layouts. Each layout is a complete PlayerStowPointManager hierarchy (its own
/// stow points, keys and tracking connector); list them in Layouts and only the chosen one is kept active. Items already
/// stowed move to free pockets of the new layout when it fits them, otherwise they are let go. The mirror (left / right
/// handed) choice carries over. The choice is local to the player and saved with VRChat Persistence.
/// Wire UI Buttons to NextLayout / PreviousLayout, or call SelectLayout(index) from your own menu. Layout names and
/// preview sprites come from each manager's Layout Name / Preview Image.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StowLayoutSwitcher : UdonSharpBehaviour
{
    [Tooltip("One PlayerStowPointManager per layout, each on its own GameObject. Put each layout's whole stow hierarchy under it.")]
    [SerializeField] private PlayerStowPointManager[] layouts;
    [SerializeField] private int defaultLayout;

    [Header("Menu (all optional)")]
    [SerializeField] private Image previewImage;
    [SerializeField] private Text nameLabel;
    [Tooltip("Pocket count per size, e.g. \"XS x3   S x2   L x1   XL x0\". What desktop players care about instead of a picture.")]
    [SerializeField] private Text countsLabel;
    [Tooltip("Behaviours told with Changed Event Name after the layout changes, for example a custom menu.")]
    [SerializeField] private UdonSharpBehaviour[] changedEventTargets;
    [SerializeField] private string changedEventName = "OnStowLayoutChanged";

    [Header("Persistence")]
    [SerializeField] private bool persistChoice = true;
    [Tooltip("The PlayerData key the layout index is saved under (an int).")]
    [SerializeField] private string playerDataKey = "UdonStow_Layout";

    private int current;
    private PlayerStowPointManager previous;
    private bool switching;

    private void Start()
    {
        int count = layouts == null ? 0 : layouts.Length;
        current = Mathf.Clamp(defaultLayout, 0, Mathf.Max(count - 1, 0));
        for (int i = 0; i < count; i++)
        {
            if (layouts[i] != null)
            {
                layouts[i].gameObject.SetActive(i == current);
            }
        }

        Refresh();
        NotifyChanged(); // a listener that started first (the HUD) reads the default layout, which this fixes up if its Start ran earlier
    }

    public int GetLayoutCount()
    {
        return layouts == null ? 0 : layouts.Length;
    }

    public int GetCurrentLayout()
    {
        return current;
    }

    public PlayerStowPointManager GetActiveManager()
    {
        return layouts == null || current >= layouts.Length ? null : layouts[current];
    }

    public void NextLayout()
    {
        if (GetLayoutCount() > 0)
        {
            SelectLayout((current + 1) % layouts.Length);
        }
    }

    public void PreviousLayout()
    {
        if (GetLayoutCount() > 0)
        {
            SelectLayout((current + layouts.Length - 1) % layouts.Length);
        }
    }

    public void SelectLayout(int index)
    {
        if (!BeginSwitch(index))
        {
            return;
        }

        if (persistChoice && !string.IsNullOrEmpty(playerDataKey))
        {
            PlayerData.SetInt(playerDataKey, index);
        }
    }

    private bool BeginSwitch(int index)
    {
        if (switching || layouts == null || index < 0 || index >= layouts.Length || index == current || layouts[index] == null)
        {
            return false;
        }

        previous = layouts[current];
        current = index;
        switching = true;

        // the new layout must run its Start (and its stow points theirs) before items are moved into it
        layouts[current].gameObject.SetActive(true);
        SendCustomEventDelayedFrames(nameof(_FinishSwitch), 2);
        return true;
    }

    public void _FinishSwitch()
    {
        PlayerStowPointManager next = layouts[current];
        if (previous != null)
        {
            next.SetMirrored(previous.IsMirrored());
            MoveStowedItems(previous, next);
            previous.gameObject.SetActive(false);
        }

        previous = null;
        switching = false;
        Refresh();
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (changedEventTargets == null)
        {
            return;
        }

        for (int i = 0; i < changedEventTargets.Length; i++)
        {
            if (changedEventTargets[i] != null)
            {
                changedEventTargets[i].SendCustomEvent(changedEventName);
            }
        }
    }

    private void MoveStowedItems(PlayerStowPointManager from, PlayerStowPointManager to)
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        bool desktop = Utilities.IsValid(localPlayer) && !localPlayer.IsUserInVR();
        int fromCount = from.GetStowPointCount();
        int toCount = to.GetStowPointCount();

        for (int i = 0; i < fromCount; i++)
        {
            StowPoint source = from.GetStowPoint(i);
            VRC_Pickup pickup = source == null ? null : source.GetStowedPickup();
            if (pickup == null)
            {
                continue;
            }

            source.ForceReleaseItemLock();
            for (int j = 0; j < toCount; j++)
            {
                StowPoint target = to.GetStowPoint(j);
                if (target == null || target.GetItemLockState())
                {
                    continue;
                }

                // a pocket that is too small leaves the item unlocked, so the next one gets a try
                if (desktop)
                {
                    target.ForceItemLockDesktop(pickup);
                }
                else
                {
                    target.ForceItemLock(pickup);
                }

                if (target.GetItemLockState())
                {
                    break;
                }
            }
        }
    }

    private void Refresh()
    {
        PlayerStowPointManager active = GetActiveManager();
        if (active == null)
        {
            return;
        }

        if (nameLabel != null)
        {
            nameLabel.text = active.GetLayoutName();
        }

        if (countsLabel != null)
        {
            int[] sizes = new int[4];
            for (int i = 0; i < active.GetStowPointCount(); i++)
            {
                StowPoint point = active.GetStowPoint(i);
                if (point != null)
                {
                    sizes[Mathf.Clamp(point.GetSizeClass(), 0, 3)]++;
                }
            }

            countsLabel.text = "XS x" + sizes[0] + "   S x" + sizes[1] + "   L x" + sizes[2] + "   XL x" + sizes[3];
        }

        if (previewImage != null)
        {
            Sprite sprite = active.GetPreviewImage();
            previewImage.sprite = sprite;
            previewImage.enabled = sprite != null;
        }
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!persistChoice || !Utilities.IsValid(player) || !player.isLocal || string.IsNullOrEmpty(playerDataKey))
        {
            return;
        }

        // let the managers' own restore (mirror) finish first, so the choice carries over
        SendCustomEventDelayedFrames(nameof(_RestoreSaved), 5);
    }

    public void _RestoreSaved()
    {
        int saved;
        if (PlayerData.TryGetInt(Networking.LocalPlayer, playerDataKey, out saved))
        {
            BeginSwitch(saved);
        }
    }
}

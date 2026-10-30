
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// A ready-made switch for the left / right handed stow layout. Put it on any object with a collider (Interact toggles it),
/// or point a UI Toggle's On Value Changed at <see cref="OnToggleValueChanged"/>, or a UI Button at the manager's
/// ToggleMirrored. It listens for the manager's OnStowMirrorChanged event (add this component to the manager's Mirror
/// Event Targets) so it always shows the current state, including the saved choice restored when the player joins.
/// Only VR players have a mirrored layout, so the switch hides itself for desktop players when Hide On Desktop is on.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StowMirrorToggle : UdonSharpBehaviour
{
    [SerializeField] private PlayerStowPointManager manager;
    [Tooltip("Optional: a UI Toggle kept in step with the layout. Point its On Value Changed at OnToggleValueChanged.")]
    [SerializeField] private Toggle uiToggle;
    [Tooltip("Optional: shown while the layout is mirrored (a left-handed icon or label).")]
    [SerializeField] private GameObject mirroredIndicator;
    [Tooltip("Optional: shown while the layout is not mirrored.")]
    [SerializeField] private GameObject normalIndicator;
    [SerializeField] private bool hideOnDesktop = true;

    private bool refreshing;

    private void Start()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (hideOnDesktop && Utilities.IsValid(localPlayer) && !localPlayer.IsUserInVR())
        {
            gameObject.SetActive(false);
            return;
        }

        OnStowMirrorChanged();
    }

    public override void Interact()
    {
        if (manager != null)
        {
            manager.ToggleMirrored();
        }
    }

    // Wire a UI Toggle's On Value Changed to this.
    public void OnToggleValueChanged()
    {
        if (refreshing || manager == null || uiToggle == null)
        {
            return;
        }

        manager.SetMirrored(uiToggle.isOn);
    }

    // Called by the manager when the state changes, and once at start.
    public void OnStowMirrorChanged()
    {
        if (manager == null)
        {
            return;
        }

        bool mirrored = manager.IsMirrored();
        if (mirroredIndicator != null) mirroredIndicator.SetActive(mirrored);
        if (normalIndicator != null) normalIndicator.SetActive(!mirrored);

        if (uiToggle != null && uiToggle.isOn != mirrored)
        {
            refreshing = true;
            uiToggle.isOn = mirrored;
            refreshing = false;
        }
    }
}

using UnityEngine;
using VRC.SDKBase;

public enum StowShape
{
    Sphere,
    UprightBox
}

public enum StowLayoutKeys
{
    OneKeyForAll,
    OneKeyPerRow,
    OneKeyPerPocket
}

/// <summary>
/// The settings of a ring or grid of stow pockets made by the Pocket Layout tool. It sits on the "Pocket Layout" object
/// that holds the pockets, and its inspector rebuilds them live as you change a value. It does nothing at runtime and is
/// stripped from builds, so it can be left in or removed with "Detach" once the layout is how you want it.
/// </summary>
[AddComponentMenu("")]
public class StowPocketLayout : MonoBehaviour, IEditorOnly
{
    public bool chest;
    public StowSize size = StowSize.ExtraSmall;
    public StowShape shape = StowShape.Sphere;
    public bool autoBoxSize = true;
    public Vector3 boxSize = new Vector3(0.04f, 0.04f, 0.02f);
    [Range(1, 8)] public int pocketsPerRow = 3;
    [Range(1, 4)] public int rows = 1;
    [Tooltip("0 = straight ahead, 90 = the player's right, -90 = their left, 180 = behind")]
    [Range(-180f, 180f)] public float centreAngle = 90f;
    [Range(0f, 360f)] public float arcWidth = 90f;
    [Range(0.05f, 0.5f)] public float radius = 0.15f;
    [Range(0.02f, 0.3f)] public float rowSpacing = 0.1f;
    public bool faceOutward = true;
    public StowLayoutKeys keys = StowLayoutKeys.OneKeyForAll;

    // what the keys were last assigned for; keys are only reassigned when these change, so hand-edited keys survive
    [HideInInspector] public StowLayoutKeys appliedKeys = StowLayoutKeys.OneKeyForAll;
    [HideInInspector] public int appliedCount;
    [HideInInspector] public int appliedRows;
}

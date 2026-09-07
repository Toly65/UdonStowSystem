
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class playerTrackingParenting : UdonSharpBehaviour
{
    public Transform mainTransform;
    public Transform hipTransform;
    public Transform TorsoTransform;
    private float avatarSize;
    private VRCPlayerApi localplayer;

    // Some avatars rig the chest backwards or the hips upside down. Player rotation is always
    // upright and facing forward, so we measure each bone against it once per avatar and keep
    // the axis flip as a fix quaternion.
    private Quaternion hipFix = Quaternion.identity;
    private Quaternion torsoFix = Quaternion.identity;

    private void Start()
    {
        localplayer = Networking.LocalPlayer;
        CalibrateBones();
    }
    public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
    {
        if(player != localplayer)
        {
            return;
        }
        mainTransform.localScale = new Vector3(player.GetAvatarEyeHeightAsMeters(), player.GetAvatarEyeHeightAsMeters(), player.GetAvatarEyeHeightAsMeters());
        // Also fires on avatar change, so re-measure. One frame late: bones aren't posed yet here.
        SendCustomEventDelayedFrames(nameof(CalibrateBones), 1);
    }

    public void CalibrateBones()
    {
        if(!Utilities.IsValid(localplayer))
        {
            return;
        }
        hipFix = MeasureFix(localplayer.GetBoneRotation(HumanBodyBones.Hips));
        torsoFix = MeasureFix(localplayer.GetBoneRotation(HumanBodyBones.Chest));
    }

    private Quaternion MeasureFix(Quaternion boneRotation)
    {
        // Bone orientation in player space, snapped to the nearest axis-aligned rotation so only
        // the rig's constant flip is cancelled and the live pose (any lean under 45 degrees) survives.
        Quaternion local = Quaternion.Inverse(localplayer.GetRotation()) * boneRotation;
        Vector3 forward = SnapAxis(local * Vector3.forward);
        Vector3 up = SnapAxis(local * Vector3.up);
        if (forward == Vector3.zero || up == Vector3.zero || Mathf.Abs(Vector3.Dot(forward, up)) > 0.5f)
        {
            return Quaternion.identity;
        }
        return Quaternion.Inverse(Quaternion.LookRotation(forward, up));
    }

    private Vector3 SnapAxis(Vector3 v)
    {
        float x = Mathf.Abs(v.x);
        float y = Mathf.Abs(v.y);
        float z = Mathf.Abs(v.z);
        if (x >= y && x >= z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
        if (y >= z) return new Vector3(0f, Mathf.Sign(v.y), 0f);
        return new Vector3(0f, 0f, Mathf.Sign(v.z));
    }

    public override void PostLateUpdate()
    {
        if(!Utilities.IsValid(localplayer))
        {
            return;
        }
        if(hipTransform)
        {
            hipTransform.SetPositionAndRotation(localplayer.GetBonePosition(HumanBodyBones.Hips), localplayer.GetBoneRotation(HumanBodyBones.Hips) * hipFix);
            
        }
        if (TorsoTransform)
        {
            TorsoTransform.SetPositionAndRotation(localplayer.GetBonePosition(HumanBodyBones.Chest), localplayer.GetBoneRotation(HumanBodyBones.Chest) * torsoFix);
        }
    }
}

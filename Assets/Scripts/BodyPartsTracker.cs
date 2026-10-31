using System;
using UnityEngine;

/// <summary>
/// Hidden tracking system for collected Frank body parts.
/// Keeps count of the Head, Torso, Arms, and Legs.
/// </summary>
public static class BodyPartsTracker
{
    public static bool HasHead { get; private set; } = false;
    public static bool HasTorso { get; private set; } = false;
    public static int ArmsCollected { get; private set; } = 0;
    public static int LegsCollected { get; private set; } = 0;

    public const int TotalRequiredParts = 6; // 1 Head, 1 Torso, 2 Arms, 2 Legs

    public static int TotalPartsCollected =>
        (HasHead ? 1 : 0) + (HasTorso ? 1 : 0) + ArmsCollected + LegsCollected;

    public static bool AllPartsCollected => TotalPartsCollected >= TotalRequiredParts;

    public static event Action<CollectibleType, int> OnPartCollected;

    public static void RegisterPart(CollectibleType type)
    {
        switch (type)
        {
            case CollectibleType.FrankHead:
                HasHead = true;
                break;
            case CollectibleType.FrankTorso:
                HasTorso = true;
                break;
            case CollectibleType.FrankArm:
                ArmsCollected = Mathf.Min(ArmsCollected + 1, 2);
                break;
            case CollectibleType.FrankLeg:
                LegsCollected = Mathf.Min(LegsCollected + 1, 2);
                break;
            default:
                return;
        }

        Debug.Log($"<color=#5CE6B0><b>[BodyPartsTracker]</b></color> Collected: <b>{type}</b>! Total Parts: <b>{TotalPartsCollected} / {TotalRequiredParts}</b> (Head: {HasHead}, Torso: {HasTorso}, Arms: {ArmsCollected}/2, Legs: {LegsCollected}/2)");
        OnPartCollected?.Invoke(type, TotalPartsCollected);
    }

    public static void ResetTracker()
    {
        HasHead = false;
        HasTorso = false;
        ArmsCollected = 0;
        LegsCollected = 0;
    }
}

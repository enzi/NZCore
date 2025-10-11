using System;
using NZCore.Hybrid;
using NZSpellCasting;
using Unity.Mathematics;
using UnityEngine;

public class Locators : MonoBehaviour
{
    public Transform Head;
    public Vector3 HeadOffset;
    
    public Transform HandLeft;
    public Vector3 HandLeftOffset;
    
    public Transform HandRight;
    public Vector3 HandRightOffset;
    
    public Transform Spine;
    public Vector3 SpineOffset;
    
    public Transform FeetLeft;
    public Vector3 FeetLeftOffset;
    public Transform FeetRight;
    public Vector3 FeetRightOffset;
    
    public Transform WeaponLeft;
    public Vector3 WeaponLeftOffset;
    
    public Transform WeaponRight;
    public Vector3 WeaponRightOffset;

    public float3 GetLocatorPosition(LocatorPosition locatorPosition)
    {
        switch (locatorPosition)
        {
            case LocatorPosition.None:
                return float3.zero;
            case LocatorPosition.Head:
                return Head.position + HeadOffset;
            case LocatorPosition.HandLeft:
                return HandLeft.position + HandLeftOffset;
            case LocatorPosition.HandRight:
                return HandRight.position + HandRightOffset;
            case LocatorPosition.Spine:
                return Spine.position + SpineOffset;
            case LocatorPosition.FeetLeft:
                return FeetLeft.position + FeetLeftOffset;
            case LocatorPosition.FeetRight:
                return FeetRight.position + FeetRightOffset;
            case LocatorPosition.FeetBetween:
                //return fee
                throw new NotImplementedException("FeetBetween not implemented");
            case LocatorPosition.WeaponLeft:
                return WeaponLeft.position + WeaponLeftOffset;
            case LocatorPosition.WeaponRight:
                return WeaponRight.position + WeaponRightOffset;
            default:
                throw new ArgumentOutOfRangeException(nameof(locatorPosition), locatorPosition, null);
        }
    }
    
    public Quaternion GetLocatorRotation(LocatorPosition locatorPosition)
    {
        switch (locatorPosition)
        {
            case LocatorPosition.None:
                return quaternion.identity;
            case LocatorPosition.Head:
                return Head.rotation;
            case LocatorPosition.HandLeft:
                return HandLeft.rotation;
            case LocatorPosition.HandRight:
                return HandRight.rotation;
            case LocatorPosition.Spine:
                return Spine.rotation;
            case LocatorPosition.FeetLeft:
                return FeetLeft.rotation;
            case LocatorPosition.FeetRight:
                return FeetRight.rotation;
            case LocatorPosition.FeetBetween:
                //return fee
                throw new NotImplementedException("FeetBetween not implemented");
            case LocatorPosition.WeaponLeft:
                return WeaponLeft.rotation;
            case LocatorPosition.WeaponRight:
                return WeaponRight.rotation;
            default:
                throw new ArgumentOutOfRangeException(nameof(locatorPosition), locatorPosition, null);
        }
    }
}

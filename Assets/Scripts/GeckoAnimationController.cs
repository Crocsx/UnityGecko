using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeckoAnimationController : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Transform headBone;

    [SerializeField] float headMaxTurnAngle;
    [SerializeField] float headTrackingSpeed;

    [SerializeField] Transform leftEyeBone;
    [SerializeField] Transform rightEyeBone;

    [SerializeField] float eyeTrackingSpeed;
    [SerializeField] float leftEyeMaxYRotation;
    [SerializeField] float leftEyeMinYRotation;
    [SerializeField] float rightEyeMaxYRotation;
    [SerializeField] float rightEyeMinYRotation;

    void LateUpdate()
    {
        HeadTrackingUpdate();
        EyesTrackingUpdate();
    }

    void HeadTrackingUpdate()
    {        
        // Store the current head rotation since we will be resetting it
        Quaternion currentLocalRotation = headBone.localRotation;
        // Reset the head rotation so our world to local space transformation will use the head's zero rotation. 
        // Note: Quaternion.Identity is the quaternion equivalent of "zero"
        headBone.localRotation = Quaternion.identity;

        Vector3 targetWorldLookDir = target.position - headBone.position;
        Vector3 targetLocalLookDir = headBone.InverseTransformDirection(targetWorldLookDir);

        // Apply angle limit
        targetLocalLookDir = Vector3.RotateTowards(
            Vector3.forward,
            targetLocalLookDir,
            Mathf.Deg2Rad * headMaxTurnAngle, // Note we multiply by Mathf.Deg2Rad here to convert degrees to radians
            0 // We don't care about the length here, so we leave it at zero
        );

        // Get the local rotation by using LookRotation on a local directional vector
        Quaternion targetLocalRotation = Quaternion.LookRotation(targetLocalLookDir, Vector3.up);

        // Apply smoothing
        headBone.localRotation = Quaternion.Slerp(
            currentLocalRotation,
            targetLocalRotation, 
            1 - Mathf.Exp(-headTrackingSpeed * Time.deltaTime)
        );
    }

    void EyeTrackingUpdate(Quaternion targetEyeRotation, Transform eyeBone, float minRotation, float maxRotation){
        eyeBone.rotation = Quaternion.Slerp(
            eyeBone.rotation,
            targetEyeRotation,
            1 - Mathf.Exp(-eyeTrackingSpeed * Time.deltaTime)
        );

        // This code is called after we set the rotations in the previous block
        float eyeCurrentYRotation = eyeBone.localEulerAngles.y;

        // Move the rotation to a -180 ~ 180 range
        if (eyeCurrentYRotation > 180)
        {
            eyeCurrentYRotation -= 360;
        }

        // Clamp the Y axis rotation
        float eyeClampedYRotation = Mathf.Clamp(
            eyeCurrentYRotation,
            minRotation,
            maxRotation
        );

        // Apply the clamped Y rotation without changing the X and Z rotations
        eyeBone.localEulerAngles = new Vector3(
            eyeBone.localEulerAngles.x,
            eyeClampedYRotation,
            eyeBone.localEulerAngles.z
        );
    }
    
    void EyesTrackingUpdate() {

        // Note: We use head position here just because the gecko doesn't look so great when cross eyed.
        // To make it relative to the eye itself, subtract the eye's position instead of the head's.
        Quaternion targetEyeRotation = Quaternion.LookRotation(
            target.position - headBone.position, // toward target
            transform.up
        );

        EyeTrackingUpdate(targetEyeRotation, leftEyeBone, rightEyeMinYRotation, rightEyeMaxYRotation);
        EyeTrackingUpdate(targetEyeRotation, rightEyeBone, leftEyeMinYRotation, leftEyeMaxYRotation);
    }
}
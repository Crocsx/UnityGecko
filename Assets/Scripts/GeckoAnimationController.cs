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

    [SerializeField] GeckoLegStepper frontLeftLegStepper;
    [SerializeField] GeckoLegStepper frontRightLegStepper;
    [SerializeField] GeckoLegStepper backLeftLegStepper;
    [SerializeField] GeckoLegStepper backRightLegStepper;

    // How fast we can turn and move full throttle
    [SerializeField] float turnSpeed;
    [SerializeField] float moveSpeed;
    // How fast we will reach the above speeds
    [SerializeField] float turnAcceleration;
    [SerializeField] float moveAcceleration;
    // Try to stay in this range from the target
    [SerializeField] float minDistToTarget;
    [SerializeField] float maxDistToTarget;
    // If we are above this angle from the target, start turning
    [SerializeField] float maxAngToTarget;

    // World space velocity
    Vector3 currentVelocity;
    // We are only doing a rotation around the up axis, so we only use a float here
    float currentAngularVelocity;

    void Awake()
    {
        StartCoroutine(LegUpdateCoroutine());
    }

    void LateUpdate()
    {
        RootMotionUpdate();
        HeadTrackingUpdate();
        EyesTrackingUpdate();
    }

    // Only allow diagonal leg pairs to step together
    IEnumerator LegUpdateCoroutine()
    {
        // Run forever
        while (true)
        {
            // Try moving one diagonal pair of legs
            do
            {
                frontLeftLegStepper.RequestMove();
                backRightLegStepper.RequestMove();
                // Wait a frame
                yield return null;
            
            // Stay in this loop while either leg is moving.
            // If only one leg in the pair is moving, the calls to TryMove() will let
            // the other leg move if it wants to.
            } while (backRightLegStepper.isMoving || frontLeftLegStepper.isMoving);

            // Do the same thing for the other pair
            do
            {
                frontRightLegStepper.RequestMove();
                backLeftLegStepper.RequestMove();
                yield return null;
            } while (backLeftLegStepper.isMoving || frontRightLegStepper.isMoving);
        }
    }

    void RootMotionUpdate()
    {
        // Get the direction toward our target
        Vector3 towardTarget = target.position - transform.position;
        // Vector toward target on the local XZ plane
        Vector3 towardTargetProjected = Vector3.ProjectOnPlane(towardTarget, transform.up);
        // Get the angle from the gecko's forward direction to the direction toward toward our target
        // Here we get the signed angle around the up vector so we know which direction to turn in
        float angToTarget = Vector3.SignedAngle(transform.forward, towardTargetProjected, transform.up);

        float targetAngularVelocity = 0;

        // If we are within the max angle (i.e. approximately facing the target)
        // leave the target angular velocity at zero
        if (Mathf.Abs(angToTarget) > maxAngToTarget)
        {
            // Angles in Unity are clockwise, so a positive angle here means to our right
            if (angToTarget > 0)
            {
                targetAngularVelocity = turnSpeed;
            }
            // Invert angular speed if target is to our left
            else
            {
                targetAngularVelocity = -turnSpeed;
            }
        }

        // Use our smoothing function to gradually change the velocity
        currentAngularVelocity = Mathf.Lerp(
            currentAngularVelocity,
            targetAngularVelocity,
            1 - Mathf.Exp(-turnAcceleration * Time.deltaTime)
        );
        
        // Rotate the transform around the Y axis in world space, 
        // making sure to multiply by delta time to get a consistent angular velocity
        transform.Rotate(0, Time.deltaTime * currentAngularVelocity, 0, Space.World);

        Vector3 targetVelocity = Vector3.zero;

        // Don't move if we're facing away from the target, just rotate in place
        if (Mathf.Abs(angToTarget) < 90)
        {
            float distToTarget = Vector3.Distance(transform.position, target.position);

            // If we're too far away, approach the target
            if (distToTarget > maxDistToTarget)
            {
                targetVelocity = moveSpeed * towardTargetProjected.normalized;
            }
            // If we're too close, reverse the direction and move away
            else if (distToTarget < minDistToTarget)
            {
                targetVelocity = moveSpeed * -towardTargetProjected.normalized;
            }
        }

        currentVelocity = Vector3.Lerp(
            currentVelocity,
            targetVelocity,
            1 - Mathf.Exp(-moveAcceleration * Time.deltaTime)
        );

        // Apply the velocity
        transform.position += currentVelocity * Time.deltaTime;
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
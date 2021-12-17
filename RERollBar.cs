using UnityEngine;

public class RERollBar : MonoBehaviour
{
    public REWheel leftWheel;
    public REWheel rightWheel;

    public float antiRoll = 5000.0f;

    public void OnUpdate(
        Rigidbody carBody,
        float powerTorque,
        float brakeTorque,
        float steerAmount,
        bool applyHandBrake)
    {
        leftWheel.OnUpdate(
            powerTorque / 2.0f,
            brakeTorque,
            steerAmount,
            applyHandBrake);
        rightWheel.OnUpdate(
            powerTorque / 2.0f,
            brakeTorque,
            steerAmount,
            applyHandBrake);

        var groundedL = leftWheel.IsGrounded();
        var groundedR = rightWheel.IsGrounded();

        if (!groundedL && !groundedR)
            return;

        var travelL = groundedL ? leftWheel.GetSuspensionTravel() : 0.5f;
        var travelR = groundedR ? rightWheel.GetSuspensionTravel() : 0.5f;
        var antiRollForce = (travelL - travelR) * antiRoll;

        if (groundedL)
        {
            var lwTransform = leftWheel.transform;
            carBody.AddForceAtPosition(
                lwTransform.up * -antiRollForce,
                lwTransform.position);
        }

        if (!groundedR)
            return;

        var rwTransform = rightWheel.transform;
        carBody.AddForceAtPosition(
            rwTransform.up * antiRollForce,
            rwTransform.position);

    }
}
using ExtensionMethods;
using UnityEngine;

public class FloatPID {
    public float pFactor, iFactor, dFactor;

    float integral;
    float lastError;

    public FloatPID(float pFactor, float iFactor, float dFactor) {
        this.pFactor = pFactor;
        this.iFactor = iFactor;
        this.dFactor = dFactor;
    }

    public float Update(float present, float timeFrame) {
        integral += present * timeFrame;
        float derivation = (present - lastError) / timeFrame;
        lastError = present;
        return present * pFactor + integral * iFactor + derivation * dFactor;
    }
    public void Reset() {
        lastError = 0;
        integral = 0;
    }
}

public class PID {
	public float pFactor, iFactor, dFactor;
		
	Vector3 integral;
	Vector3 lastError;
	
	public PID(float pFactor, float iFactor, float dFactor) {
		this.pFactor = pFactor;
		this.iFactor = iFactor;
		this.dFactor = dFactor;
	}

	public Vector3 Update(Vector3 present, float timeFrame) {
		integral += present * timeFrame;
		Vector3 deriv = (present - lastError) / timeFrame;
		lastError = present;
		return present * pFactor + integral * iFactor + deriv * dFactor;
	}

    public void Reset() {
        lastError = Vector3.zero;
        integral = Vector3.zero;
    }
}

public class RBPID {
    public PID velocityPID;
    public PID headingPID;
    public Rigidbody rb;

    private ForceMode forceMode;

    public bool isActive;
    private float maxForce;

    public RBPID(Rigidbody rigidbody, float p = 1, float i = 0, float d = 0.3f, ForceMode forceMode = ForceMode.Force, float maxForce = 100) {
        rb = rigidbody;
        isActive = true;
        this.maxForce = maxForce;
        this.forceMode = forceMode;
        velocityPID = new PID(p, i, d);
        headingPID = new PID(p, i, d);
    }

    public RBPID Position(float p, float i, float d) {
        velocityPID = new PID(p, i, d);
        return this;
    }

    public RBPID Rotation(float p, float i, float d) {
        headingPID = new PID(p, i, d);
        return this;
    }

    public void Update(Vector3 targetPos, Quaternion targetRot, float forceMult = 1, float slowMult = 1) {
        if (!isActive)
            return;
        UpdateVelocity(targetPos, forceMult, slowMult);
        UpdateTorque(targetRot, forceMult, slowMult);
    }

    public void UpdateVelocity(Vector3 targetPos, float forceMult = 1f, float slowMult = 1f) {
        if (!isActive)
            return;
        if (Time.deltaTime == 0 || Time.deltaTime == float.NaN) return;
        var force = velocityPID.Update(targetPos - rb.transform.position, Time.deltaTime) * forceMult;
        rb.AddForce(force.SafetyClamp(maxForce), forceMode);
    }

    public void UpdateTorque(Quaternion targetRot, float forceMult = 1f, float slowMult = 1f) {
        if (!isActive)
            return;
        if (Time.deltaTime == 0 || Time.deltaTime == float.NaN) return;
        var rotation = (Vector3.Cross(rb.transform.rotation * Vector3.forward, targetRot * Vector3.forward)
                        + Vector3.Cross(
                            rb.transform.rotation
                            * Vector3.up,
                            targetRot * Vector3.up)).normalized
                       * Quaternion.Angle(rb.transform.rotation, targetRot)
                       / 360;

        var torque = headingPID.Update(rotation, Time.deltaTime) * forceMult;
        rb.AddTorque(torque.SafetyClamp(maxForce), forceMode);
    }

    public void Reset() {
        velocityPID.Reset();
        headingPID.Reset();
    }
}
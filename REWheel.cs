using UnityEngine;
using Random = UnityEngine.Random;

public class REWheel : MonoBehaviour
{
    private Transform _driveEffectMount;
    private Transform _slipEffectMount;

    private Transform TireModel { get; set; }

    public WheelCollider Wheel { get; private set; }

    private WheelHit _groundHit;

    private bool _isGrounded;

    private int _previousTerrain;

    private GameObject _activeDriveEffect;
    private GameObject _activeSlipEffect;

    private int _lastSkid = -1; // Array index for the skidmarks controller. Index of last skidmark piece this wheel used

    private bool _applyHandbrake;
    
    private void Awake()
    {
        _previousTerrain = -1;
        Wheel = GetComponent<WheelCollider>();
        TireModel = transform.GetChild(
            0);
        _driveEffectMount = transform.Find("DriveEffectMount").transform;
        _slipEffectMount = transform.Find("SkidEffectMount").transform;
        Wheel.ConfigureVehicleSubsteps(5.0f, 5, 5);
    }

    public void OnUpdate(
        float powerTorque,
        float brakeTorque,
        float steerAmount,
        bool applyHandBrake)
    {
        _isGrounded = Wheel.GetGroundHit(
            out _groundHit);

        Wheel.motorTorque = _isGrounded ? powerTorque : 0.0f;
        Wheel.brakeTorque = brakeTorque;
        Wheel.steerAngle = steerAmount;

        _applyHandbrake = applyHandBrake;

        if (_applyHandbrake)
        {
            Wheel.brakeTorque *= 4.0f;
        }
    }

    public bool IsGrounded()
    {
        return _isGrounded;
    }

    public float GetSuspensionTravel()
    {
        return Wheel.transform.InverseTransformPoint(
            _groundHit.point).y + Wheel.radius;
        /*
        return ( -wheel.transform.InverseTransformPoint( 
            groundHit.point ).y-wheel.radius )/wheel.suspensionDistance;*/
    }

    private void Animate()
    {
        Vector3 colliderPosition;
        Quaternion colliderRotation;
        Wheel.GetWorldPose(out colliderPosition, out colliderRotation);
        TireModel.position = colliderPosition;
        TireModel.rotation = colliderRotation;
    }

    private void HandleSurface()
    {
        var audioEffect = GetComponent<AudioSource>();

        var surfaceIndex = -1;

        if (_isGrounded)
        {
            if (Wheel.rpm > 10.0f)
            {
                if (!audioEffect.isPlaying)
                {
                    audioEffect.Play();
                    audioEffect.pitch = Random.Range(
                        0.85f,
                        1.15f);
                }
            }
            else if (audioEffect.isPlaying)
            {
                audioEffect.Stop();
            }

            if(_groundHit.collider.CompareTag("Terrain"))
                surfaceIndex = TerrainInfoHelper.GetMainTexture(_groundHit.point);
            else
            {
                var terrainTypeComponent = _groundHit.collider.GetComponent<TerrainType>();
                if (terrainTypeComponent == null)
                    surfaceIndex = -1;
                else
                    surfaceIndex = terrainTypeComponent.terrainTypeIndex;
            }
        }

        if (surfaceIndex == _previousTerrain) return;

        var raceManager = GameObject.Find("Race").GetComponent<RERace>();

        if (_previousTerrain != -1)
        {
            raceManager.ReturnDriveEffect(_previousTerrain, _activeDriveEffect);
            raceManager.ReturnSlipEffect(_previousTerrain, _activeSlipEffect);
        }

        _previousTerrain = surfaceIndex;

        audioEffect.Stop();

        if (surfaceIndex == -1)
        {
            return;
        }

        var gameManager = GameObject.Find("GM").GetComponent<GM>();

        audioEffect.clip = gameManager.GetTireSound(surfaceIndex);
        audioEffect.Play();

        _activeDriveEffect = raceManager.GetDriveEffect(surfaceIndex);
        _activeDriveEffect.transform.SetParent(_driveEffectMount, false);
        _activeDriveEffect.transform.position = _driveEffectMount.position;
        _activeDriveEffect.transform.rotation = _driveEffectMount.rotation;

        _activeSlipEffect = raceManager.GetSlipEffect(surfaceIndex);
        _activeSlipEffect.transform.SetParent(_slipEffectMount, false);
        _activeSlipEffect.transform.position = _slipEffectMount.position;
        _activeSlipEffect.transform.rotation = _slipEffectMount.rotation;

        float newForwardAsSlip;
        float newForwardAsValue;
        float newForwardExSlip;
        float newForwardExValue;
        float newSideAsSlip;
        float newSideAsValue;
        float newSideExSlip;
        float newSideExValue;

        gameManager.GetForwardFrictionCurve(
            surfaceIndex,
            out newForwardAsSlip,
            out newForwardAsValue,
            out newForwardExSlip,
            out newForwardExValue);

        gameManager.GetSideFrictionCurve(
            surfaceIndex,
            out newSideAsSlip,
            out newSideAsValue,
            out newSideExSlip,
            out newSideExValue);

        var forwardFriction = Wheel.forwardFriction;
        forwardFriction.asymptoteSlip = newForwardAsSlip;
        forwardFriction.asymptoteValue = newForwardAsValue;
        forwardFriction.extremumSlip = newForwardExSlip;
        forwardFriction.extremumValue = newForwardExValue;
        //forwardFriction.stiffness = 2.0f;
        Wheel.forwardFriction = forwardFriction;

        var sideFriction = Wheel.sidewaysFriction;
        sideFriction.asymptoteSlip = newSideAsSlip;
        sideFriction.asymptoteValue = newSideAsValue;
        sideFriction.extremumSlip = newSideExSlip;
        sideFriction.extremumValue = newSideExValue;
        //sideFriction.stiffness = 1.0f;
        Wheel.sidewaysFriction = sideFriction;
    }
    protected void LateUpdate()
    {
        HandleSurface();
        Animate();

        if (!_activeSlipEffect)
            return;

        var particles = _activeSlipEffect.GetComponent<ParticleSystem>();
        var audioEffect = _activeSlipEffect.GetComponent<AudioSource>();
        if (_isGrounded)
        {
            if (Mathf.Max(_groundHit.forwardSlip, _groundHit.sidewaysSlip) >= 0.2f)
            {
                var skidTotal = _groundHit.forwardSlip + _groundHit.sidewaysSlip;
                var intensity = Mathf.Clamp01(skidTotal);
                var gameManager = GameObject.Find("GM").GetComponent<GM>();
                _lastSkid = gameManager.AddSkidMark(_groundHit.point, _groundHit.normal, intensity, _lastSkid);

                if(particles)
                    particles.Play();
                
                if (!audioEffect)
                    return;

                if (_groundHit.sidewaysSlip >= 0.2f)
                {
                    audioEffect.volume = _groundHit.sidewaysSlip;
                    audioEffect.pitch = Random.Range(
                        0.85f,
                        1.15f);
                    if(audioEffect.isPlaying)
                        return;

                    audioEffect.Play();
                }
                else
                {
                    audioEffect.Stop();
                }
            }
            else
            {
                _lastSkid = -1;

                if(particles)
                    particles.Stop();

                if(audioEffect)
                    audioEffect.Stop();
            }
        }
        else
        {
            _lastSkid = -1;

            if(particles)
                particles.Stop();
            if(audioEffect)
                audioEffect.Stop();
        }
    }
}
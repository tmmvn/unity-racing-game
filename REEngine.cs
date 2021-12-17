using UnityEngine;

public class REEngine : MonoBehaviour
{
    public int maxGear = 5;
    public float maxSteer = 25.0f;
    public float enginePower = 150.0f;
    public int idleRPM = 1100;
    public int dropRPM = 2200;
    public int maxRPM = 6500;
    public float finalDrive = 4.0f;
    public AnimationCurve powerCurve;
    public AnimationCurve differential;

    private float _power;
    private float _brake;
    private float _steer;
    private bool _handbrake;
    private float _wheelRPM;

    private int RPM { get; set; }

    private int _currentGear;

    private AudioSource _engineSound;

    private void Awake()
    {
        _engineSound = GetComponent<AudioSource>();
    }
    
    public float GetPowerTorque()
    {
        return _power;
    }

    public float GetBrakeTorque()
    {
        return _brake;
    }

    public float GetSteerAngle()
    {
        return _steer;
    }

    public bool GetHandBrake()
    {
        return _handbrake;
    }

    public void SetGearToIdle()
    {
        _currentGear = 0;
    }

    public void SetGearOn()
    {
        _currentGear = 1;
    }

    public int GetRPM()
    {
        return RPM;
    }

    public int GetGear()
    {
        return _currentGear;
    }
    
    public void OnUpdate(
        float carMass,
        float wheelRatio,
        float FLRPM,
        float FRRPM,
        float RLRPM,
        float RRRPM)
    {
        _handbrake = Input.GetButton("R1");
        _steer = Input.GetAxis("LX") * maxSteer;
        var accelerationInput = Input.GetButton("R2") ? 1.0f : 0.0f;
        var deAccelerationInput = Input.GetButton("L2") ? -1.0f : 0.0f;
        var acceleration = accelerationInput + deAccelerationInput;

        if (!RERace.IsRacing())
        {
            accelerationInput = 0.0f;
            deAccelerationInput = 0.0f;
            acceleration = 0.0f;
        }

        var speed = transform.parent.GetComponent<Rigidbody>().velocity.magnitude;

        if (acceleration > 0.0f)
        {
            if (_currentGear <= 0)
            {
                _currentGear++;
                //audioeffect.Play();
            }
            else if (_currentGear < maxGear)
            {
                if (RPM > 0.85f * maxRPM)
                {
                    _currentGear++;
                    //audioeffect.Play();
                }
            }
        }
        else if (acceleration < 0.0f)
        {
            if (_currentGear == 0 && speed < 1.5f)
            {
                _currentGear--;
                //audioeffect.Play();
            }
            else if (RPM < dropRPM && _currentGear > 0)
            {
                _currentGear--;
                //audioeffect.Play();
            }
        }
        else
        {
            if (_currentGear > 0)
            {
                if (RPM < dropRPM)
                {
                    _currentGear--;
                    //audioeffect.Play();
                }
            }
        }
        /*OLD manual shifting, likely not needed
        else 
        {
            if( currentGear<maxGear )
                if( Input.GetKeyUp( 
                    KeyCode.E ) )
                currentGear++;

            if( currentGear>1 )
                if( Input.GetKeyUp( 
                    KeyCode.Q ) )
                currentGear--;
        }*/

        var wheelRadius = 0.3f;
        var wheelCircumference = wheelRadius * 2.0f * Mathf.PI;
        var effectiveGearRatio = finalDrive * differential.Evaluate(_currentGear);

        _wheelRPM = ((FLRPM + FRRPM)/2 * (1.0f - wheelRatio) + (RLRPM + RRRPM)/2 * wheelRatio) * effectiveGearRatio;
        RPM = Mathf.RoundToInt(speed / (wheelCircumference / 60) * effectiveGearRatio);//Mathf.RoundToInt(Mathf.Abs(_wheelRPM));//Mathf.RoundToInt(speed / (wheelCircumference / 60) * effectiveGearRatio); //Mathf.RoundToInt(_wheelRPM);
        RPM = Mathf.Max(idleRPM, RPM);

        var scaledRPM = (RPM - idleRPM) / (float) (maxRPM - idleRPM);
        _engineSound.pitch = 1.0f + scaledRPM;

        var engineOutput = powerCurve.Evaluate(RPM / 10000.0f) * enginePower * Mathf.Abs(acceleration);
        _power = engineOutput * effectiveGearRatio;

        if (deAccelerationInput < 0.0f && _currentGear >= 0)
        {
            _brake = carMass * 1.3f * 9.81f;
            //_power = 0.0f;
        }
        else if (accelerationInput > 0.0f && _currentGear == -1)
        {
            _brake = carMass * 1.3f * 9.81f;
            //_power = 0.0f;
        }
        else
        {
            _brake = _handbrake ? carMass * 9.81f : 0.0f;
        }
    }
}
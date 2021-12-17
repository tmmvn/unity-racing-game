using UnityEngine;
using UnityEngine.UI;

public class RECar : MonoBehaviour
{
    public RERollBar frontRollBar;
    public RERollBar rearRollBar;
    public REEngine engine;

    public Transform centerOfMass;

    public float wheelDriveRatio;

    public Transform collisionSound;

    public Light FLHeadLight;
    public Light FRHeadLight;
    public Light LBreakLight;
    public Light RBreakLight;
    public Light LReverseLight;
    public Light RReverseLight;

    private Rigidbody _rigidbody;
    private float _maxDrag;
    private float _mass;
    private float _speed;

    private Image _rpmImage;
    private Text _gearText;
    private Text _speedText;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.centerOfMass = centerOfMass.localPosition;
        _maxDrag = _rigidbody.drag;
        _mass = _rigidbody.mass;
        _rigidbody.maxAngularVelocity = 100.0f;
        _gearText = GameObject.Find("Gear").GetComponent<Text>();
        _rpmImage = GameObject.Find("RPM").GetComponent<Image>();
        _speedText = GameObject.Find("Speed").GetComponent<Text>();
        if (!RERace.Instance.dark)
            return;
        FLHeadLight.enabled = true;
        FRHeadLight.enabled = true;
    }

    private void FixedUpdate()
    {
        if (!RERace.IsRacing())
        {
            engine.SetGearToIdle();
            //return;
        }

        engine.OnUpdate(
            _mass,
            wheelDriveRatio,
            frontRollBar.leftWheel.Wheel.rpm,
            frontRollBar.rightWheel.Wheel.rpm,
            rearRollBar.leftWheel.Wheel.rpm,
            rearRollBar.rightWheel.Wheel.rpm);
        OnUpdate();
    }

    private void OnUpdate()
    {
        frontRollBar.OnUpdate(
            _rigidbody,
            engine.GetPowerTorque() * (1.0f - wheelDriveRatio),
            engine.GetBrakeTorque() / 2.0f,
            engine.GetSteerAngle(),
            false);
        rearRollBar.OnUpdate(
            _rigidbody,
            engine.GetPowerTorque() * wheelDriveRatio,
            engine.GetBrakeTorque() / 2.0f,
            0.0f,
            engine.GetHandBrake());
    }

    private void LateUpdate()
    {
        _speed = _rigidbody.velocity.magnitude * 2.237f; // 2.237 is 1ms as mph
        _rigidbody.drag = _speed * _maxDrag / 4000.0f; // 40mph/60kmh is where we get the max drag
        _speedText.text = Mathf.RoundToInt(_speed) + " MPH";
        var gear = engine.GetGear();
        var isReversing = false;
        if (gear == 0)
            _gearText.text = "N";
        else if (gear < 0) {
            _gearText.text = "R";
            isReversing = true;
        }
        else
            _gearText.text = gear.ToString();

        var rpmGauge = (float) engine.GetRPM() / engine.maxRPM * 0.5f;
        _rpmImage.fillAmount = rpmGauge;

        RReverseLight.enabled = isReversing;
        LReverseLight.enabled = isReversing;
        var isBreaking = engine.GetBrakeTorque() > 0.0f;
        LBreakLight.enabled = isBreaking;
        RBreakLight.enabled = isBreaking;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Terrain"))
            return;

        var contact = other.contacts[0];

        collisionSound.position = contact.point;
        //TODO: Add more detailed collision sounds
        collisionSound.GetComponent<AudioSource>().Play();
    }
}
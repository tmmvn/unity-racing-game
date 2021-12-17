using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class GM : MonoBehaviour
{
    // TODO: Move skid mark code to race? Or it's own class. Or figure out how to reset.
    private class MarkSection
    {
        public Vector3 Position = Vector3.zero;
        public Vector3 Normal = Vector3.zero;
        public Vector4 Tangent = Vector4.zero;
        public Vector3 PositionLeft = Vector3.zero;
        public Vector3 PositionRight = Vector3.zero;
        public Color32 Color;
        public int LastIndex;
    }

    public Vector4[] forwardSlipCurves;
    public Vector4[] sideSlipCurves;
    public AudioClip[] wheelRollSounds;
    public AudioClip[] wheelSkidSounds;
    
    public Material skidMarkMaterial; // Material for the skidmarks to use

    public int maxMarks = 2048; // Max number of marks total for everyone together
    public float markWidth = 0.35f; // Width of the skidmarks. Should match the width of the wheels
    public float groundOffset = 0.02f; // Distance above surface in metres
    public float minDistance = 0.25f; // Distance between skid texture sections in metres. Bigger = better performance, less smooth
    public float maxOpacity = 1.0f; // Max skidmark opacity
    
    private bool _connectedController;

    private int _markIndex;
    private MarkSection[] _skidMarks;
    private Mesh _marksMesh;
    private MeshRenderer _mr;
    private MeshFilter _mf;

    private Vector3[] _vertices;
    private Vector3[] _normals;
    private Vector4[] _tangents;
    private Color32[] _colors;
    private Vector2[] _uvs;
    private int[] _triangles;

    private bool _meshUpdated;
    private bool _haveSetBounds;

    private Color32 _black = Color.black;

    private void Awake()
    {
        UnityEngine.tvOS.Remote.allowExitToHome = false;
        DontDestroyOnLoad(gameObject);
        StartCoroutine(CheckForControllers());
        
        if (transform.position == Vector3.zero) return;

        var myTransform = transform;
        myTransform.position = Vector3.zero;
        myTransform.rotation = Quaternion.identity;
    }

    public void GetForwardFrictionCurve(int terrainType, out float asSlip, out float asValue, out float exSlip,
        out float exValue)
    {
        if (terrainType >= forwardSlipCurves.Length)
        {
            Debug.LogError("Unknown terrain type " + terrainType);
            asSlip = asValue = exSlip = exValue = 0.0f;
            return;
        }

        asSlip = forwardSlipCurves[terrainType].z;
        asValue = forwardSlipCurves[terrainType].w;
        exSlip = forwardSlipCurves[terrainType].x;
        exValue = forwardSlipCurves[terrainType].y;
    }

    public void GetSideFrictionCurve(int terrainType, out float asSlip, out float asValue, out float exSlip,
        out float exValue)
    {
        if (terrainType >= sideSlipCurves.Length)
        {
            Debug.LogError("Unknown terrain type " + terrainType);
            asSlip = asValue = exSlip = exValue = 0.0f;
            return;
        }

        asSlip = sideSlipCurves[terrainType].z;
        asValue = sideSlipCurves[terrainType].w;
        exSlip = sideSlipCurves[terrainType].x;
        exValue = sideSlipCurves[terrainType].y;
    }

    public AudioClip GetTireSound(int terrainType)
    {
        return wheelRollSounds[terrainType];
    }

    public AudioClip GetSkidSound(int terrainType)
    {
        return wheelSkidSounds[terrainType];
    }

    private void Start()
    {
        Social.localUser.Authenticate(ProcessAuthentication);
        StartCoroutine(
            DoLoad());
        _skidMarks = new MarkSection[maxMarks];
        for (var i = 0; i < maxMarks; i++)
        {
            _skidMarks[i] = new MarkSection();
        }

        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();

        if (_mr == null)
        {
            _mr = gameObject.AddComponent<MeshRenderer>();
        }

        _marksMesh = new Mesh();
        _marksMesh.MarkDynamic();

        if (_mf == null)
        {
            _mf = gameObject.AddComponent<MeshFilter>();
        }

        _mf.sharedMesh = _marksMesh;

        _vertices = new Vector3[maxMarks * 4];
        _normals = new Vector3[maxMarks * 4];
        _tangents = new Vector4[maxMarks * 4];
        _colors = new Color32[maxMarks * 4];
        _uvs = new Vector2[maxMarks * 4];
        _triangles = new int[maxMarks * 6];

        _mr.shadowCastingMode = ShadowCastingMode.Off;
        _mr.receiveShadows = false;
        _mr.material = skidMarkMaterial;
        _mr.lightProbeUsage = LightProbeUsage.Off;
    }

    private static IEnumerator DoLoad()
    {
        yield return new WaitForSeconds(0.5f);
        var loadOp = SceneManager.LoadSceneAsync(
            "menu",
            LoadSceneMode.Single);
        loadOp.allowSceneActivation = false;
        while (loadOp.progress < 0.9f)
        {
            yield return null;
        }

        loadOp.allowSceneActivation = true;
    }

    private static void ProcessAuthentication(bool success)
    {
        if (success)
        {
            // Request loaded achievements, and register a callback for processing them
            //Social.LoadAchievements (ProcessLoadedAchievements);
        }
        else
            Debug.Log("Failed to authenticate");
    }

    public void ShowScores()
    {
        Social.ShowLeaderboardUI();
    }

    private IEnumerator CheckForControllers()
    {
        var controllers = Input.GetJoystickNames();
        if (!_connectedController && controllers.Length > 0)
        {
            _connectedController = true;
        }
        else if (_connectedController && controllers.Length == 0)
        {
            _connectedController = false;
        }

        yield return new WaitForSeconds(5.0f);

        StartCoroutine("CheckForControllers");
    }

    public int AddSkidMark(Vector3 pos, Vector3 normal, float opacity, int lastIndex)
    {
        if (opacity > 1) opacity = 1.0f;
        else if (opacity < 0) return -1;

        _black.a = (byte) (opacity * 255);
        return AddSkidMark(pos, normal, _black, lastIndex);
    }

    private int AddSkidMark(Vector3 pos, Vector3 normal, Color32 color, int lastIndex)
    {
        if (color.a == 0) return -1;

        if (lastIndex > 0)
        {
            var sqrDistance = (pos - _skidMarks[lastIndex].Position).sqrMagnitude;
            var minSqrDistance = minDistance * minDistance;
            if (sqrDistance < minSqrDistance) return lastIndex;
        }

        color.a = (byte) (color.a * maxOpacity);

        var curSection = _skidMarks[_markIndex];

        curSection.Position = pos + normal * groundOffset;
        curSection.Normal = normal;
        curSection.Color = color;
        curSection.LastIndex = lastIndex;

        if (lastIndex != -1)
        {
            var lastSection = _skidMarks[lastIndex];
            var dir = (curSection.Position - lastSection.Position);
            var xDir = Vector3.Cross(dir, normal).normalized;

            curSection.PositionLeft = curSection.Position + xDir * (markWidth * 0.5f);
            curSection.PositionRight = curSection.Position - xDir * (markWidth * 0.5f);
            curSection.Tangent = new Vector4(xDir.x, xDir.y, xDir.z, 1);

            if (lastSection.LastIndex == -1)
            {
                lastSection.Tangent = curSection.Tangent;
                lastSection.PositionLeft = curSection.Position + xDir * (markWidth * 0.5f);
                lastSection.PositionRight = curSection.Position - xDir * (markWidth * 0.5f);
            }
        }

        UpdateSkidMarkMesh();

        var curIndex = _markIndex;
        _markIndex = ++_markIndex % maxMarks;

        return curIndex;
    }

    private void UpdateSkidMarkMesh()
    {
        var curr = _skidMarks[_markIndex];

        // Nothing to connect to yet
        if (curr.LastIndex == -1) return;

        var last = _skidMarks[curr.LastIndex];
        _vertices[_markIndex * 4 + 0] = last.PositionLeft;
        _vertices[_markIndex * 4 + 1] = last.PositionRight;
        _vertices[_markIndex * 4 + 2] = curr.PositionLeft;
        _vertices[_markIndex * 4 + 3] = curr.PositionRight;

        _normals[_markIndex * 4 + 0] = last.Normal;
        _normals[_markIndex * 4 + 1] = last.Normal;
        _normals[_markIndex * 4 + 2] = curr.Normal;
        _normals[_markIndex * 4 + 3] = curr.Normal;

        _tangents[_markIndex * 4 + 0] = last.Tangent;
        _tangents[_markIndex * 4 + 1] = last.Tangent;
        _tangents[_markIndex * 4 + 2] = curr.Tangent;
        _tangents[_markIndex * 4 + 3] = curr.Tangent;

        _colors[_markIndex * 4 + 0] = last.Color;
        _colors[_markIndex * 4 + 1] = last.Color;
        _colors[_markIndex * 4 + 2] = curr.Color;
        _colors[_markIndex * 4 + 3] = curr.Color;

        _uvs[_markIndex * 4 + 0] = new Vector2(0, 0);
        _uvs[_markIndex * 4 + 1] = new Vector2(1, 0);
        _uvs[_markIndex * 4 + 2] = new Vector2(0, 1);
        _uvs[_markIndex * 4 + 3] = new Vector2(1, 1);

        _triangles[_markIndex * 6 + 0] = _markIndex * 4 + 0;
        _triangles[_markIndex * 6 + 2] = _markIndex * 4 + 1;
        _triangles[_markIndex * 6 + 1] = _markIndex * 4 + 2;

        _triangles[_markIndex * 6 + 3] = _markIndex * 4 + 2;
        _triangles[_markIndex * 6 + 5] = _markIndex * 4 + 1;
        _triangles[_markIndex * 6 + 4] = _markIndex * 4 + 3;

        _meshUpdated = true;
    }
    
    protected void LateUpdate()
    {
        if (!_meshUpdated) return;

        _meshUpdated = false;

        _marksMesh.vertices = _vertices;
        _marksMesh.normals = _normals;
        _marksMesh.tangents = _tangents;
        _marksMesh.triangles = _triangles;
        _marksMesh.colors32 = _colors;
        _marksMesh.uv = _uvs;

        if (!_haveSetBounds)
        {
            _marksMesh.bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(10000, 10000, 10000));
            _haveSetBounds = true;
        }

        _mf.sharedMesh = _marksMesh;
    }
}
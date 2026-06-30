using UnityEngine;

// Port of camera_follow.gd. Chase camera: behind (+Z) and above the player, looking
// forward (-Z). Height + look-target height are fixed so the view doesn't bob on jumps.
// X follows the player partially, smoothed. FOV widens with speed; trauma drives shake.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public float back = 8f;          // distance behind the player (+Z)
    public float height = 5f;        // fixed camera height (no jump bob)
    public float xFollow = 0.5f;     // fraction of player X the camera tracks
    public float lookAhead = 12f;    // how far ahead the camera aims (-Z)
    public float followSharp = 8f;   // X smoothing rate
    public float shakeTranslate = 0.6f;
    public float shakeRoll = 0.09f;  // radians at full trauma
    public float traumaDecay = 2.4f;
    public float baseFov = 70f;
    public float maxFov = 88f;

    Camera _cam;
    PlayerRunner _player;
    float _trauma;

    void Awake() { _cam = GetComponent<Camera>(); }

    public void AddTrauma(float amount) { _trauma = Mathf.Min(1f, _trauma + amount); }

    void LateUpdate()
    {
        if (_player == null) { _player = FindObjectOfType<PlayerRunner>(); if (_player == null) return; }
        Vector3 p = _player.transform.position;

        Vector3 pos = transform.position;
        float targetX = p.x * xFollow;
        pos.x = Mathf.Lerp(pos.x, targetX, Mathf.Clamp01(followSharp * Time.deltaTime));
        pos.y = height;
        pos.z = p.z + back;
        transform.position = pos;
        transform.LookAt(new Vector3(p.x * 0.5f, 1f, p.z - lookAhead), Vector3.up);

        float targetFov = Mathf.Lerp(baseFov, maxFov, _player.GetSpeedFraction());
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Mathf.Clamp01(2.5f * Time.deltaTime));

        if (_trauma > 0f)
        {
            _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.deltaTime);
            float s = _trauma * _trauma;
            pos = transform.position;
            pos.x += Random.Range(-1f, 1f) * s * shakeTranslate;
            pos.y += Random.Range(-1f, 1f) * s * shakeTranslate;
            transform.position = pos;
            transform.Rotate(0f, 0f, Random.Range(-1f, 1f) * s * shakeRoll * Mathf.Rad2Deg, Space.Self);
        }
    }
}

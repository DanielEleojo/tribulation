using UnityEngine;

// Port of camera_follow.gd. Chase camera: behind (+Z) and above the player, looking
// forward (-Z). Height + look-target height are fixed so the view doesn't bob on jumps.
// X follows the player partially, smoothed. FOV widens with speed; trauma drives shake.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public float back = 7f;          // distance behind the player (+Z) — pulled in slightly for a more heroic frame
    public float height = 4.3f;      // fixed camera height (no jump bob) — lowered for a flatter, grander angle
    public float xFollow = 0.5f;     // fraction of player X the camera tracks
    public float lookAhead = 12f;    // how far ahead the camera aims (-Z)
    public float followSharp = 18f;  // X smoothing rate — tighter so the cam doesn't lag lane changes
    public float shakeTranslate = 0.6f;
    public float shakeRoll = 0.09f;  // radians at full trauma
    public float traumaDecay = 2.4f;
    public float baseFov = 70f;
    public float maxFov = 88f;

    // Feel Pass v1: subtle camera bank into lane changes + transient FOV kick on events.
    public float bankPerVel = 1.2f;   // roll degrees per unit/s of lateral player velocity
    public float maxBank = 7f;        // cap (subtle — the character already banks harder)
    public float bankSharp = 9f;      // bank smoothing rate
    public float fovKickDecay = 4.5f; // how fast an FOV kick fades

    Camera _cam;
    PlayerRunner _player;
    float _trauma;
    float _bank, _lastPx, _fovKick;
    bool _haveLastPx;

    void Awake() { _cam = GetComponent<Camera>(); }

    public void AddTrauma(float amount) { _trauma = Mathf.Min(1f, _trauma + amount); }
    public void AddFovKick(float amount) { _fovKick += amount; }

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

        // Camera bank: lean into lateral motion. Sign tuned so the camera rolls toward the
        // lane you're moving into. (ponytail: flip the minus if it leans the wrong way in play.)
        float dt = Time.deltaTime;
        float latVel = (_haveLastPx && dt > 1e-5f) ? (p.x - _lastPx) / dt : 0f;
        _lastPx = p.x; _haveLastPx = true;
        float targetBank = Mathf.Clamp(-latVel * bankPerVel, -maxBank, maxBank);
        _bank = Mathf.Lerp(_bank, targetBank, Mathf.Clamp01(bankSharp * dt));
        transform.Rotate(0f, 0f, _bank, Space.Self);

        // FOV: speed-driven base + transient event kick that decays back.
        float targetFov = Mathf.Lerp(baseFov, maxFov, _player.GetSpeedFraction()) + _fovKick;
        _fovKick = Mathf.Lerp(_fovKick, 0f, Mathf.Clamp01(fovKickDecay * dt));
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Mathf.Clamp01(2.5f * dt));

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

using System.Collections.Generic;
using UnityEngine;

// Tracer-bullet slice of spawner.gd: spawn ONE hazard type (a lane block you dodge by
// switching lanes) ahead of the player on a difficulty-ramping interval, cull behind.
// Full hazard/orb/pill/gate set + pooling come in Phase 3.
public class Spawner : MonoBehaviour
{
    const float SPAWN_AHEAD = 42f;     // how far in front of the player to place a block (-Z)
    const float CULL_BEHIND = 12f;     // destroy blocks this far behind the player (+Z)
    const float LANE_WIDTH = 2.5f;

    Transform _player;
    Material _mat;
    float _timer, _runTime;
    readonly List<GameObject> _live = new List<GameObject>();

    void Start()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _mat = new Material(sh) { color = new Color(0.7f, 0.12f, 0.14f) };
        _mat.EnableKeyword("_EMISSION");
        _mat.SetColor("_EmissionColor", new Color(0.5f, 0.05f, 0.06f));
    }

    void Update()
    {
        if (_player == null) { var p = FindObjectOfType<PlayerRunner>(); if (p == null) return; _player = p.transform; }

        _runTime += Time.deltaTime;
        _timer -= Time.deltaTime;
        if (_timer <= 0f) { Spawn(); _timer = Interval(); }

        // Cull blocks the player has passed.
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            if (_live[i] == null) { _live.RemoveAt(i); continue; }
            if (_live[i].transform.position.z > _player.position.z + CULL_BEHIND)
            {
                Destroy(_live[i]); _live.RemoveAt(i);
            }
        }
    }

    // Interval eases start->min over the ramp, then min->hard_min over the endless ramp.
    float Interval()
    {
        var b = Balance.D;
        if (_runTime < b.spawn_ramp_time)
            return Mathf.Lerp(b.spawn_start_interval, b.spawn_min_interval, _runTime / b.spawn_ramp_time);
        float t = Mathf.Clamp01((_runTime - b.spawn_ramp_time) / b.spawn_endless_ramp);
        return Mathf.Lerp(b.spawn_min_interval, b.spawn_hard_min_interval, t);
    }

    void Spawn()
    {
        int lane = Random.Range(0, 3);
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Hazard";
        block.transform.SetParent(transform, false);
        block.transform.position = new Vector3((lane - 1) * LANE_WIDTH, 0.7f, _player.position.z - SPAWN_AHEAD);
        block.transform.localScale = new Vector3(1.6f, 1.4f, 1f);
        block.GetComponent<Renderer>().sharedMaterial = _mat;
        block.GetComponent<Collider>().isTrigger = true;
        block.AddComponent<Hazard>();
        _live.Add(block);
    }

    public void ClearAll()
    {
        foreach (var g in _live) if (g != null) Destroy(g);
        _live.Clear();
        _timer = 0f; _runTime = 0f;
    }
}

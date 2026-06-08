extends Node
## Autoload "Balance": single source of tunable knobs. Loads res://data/balance.json
## over the built-in DEFAULTS, so the game can be re-tuned by editing one JSON file
## (no recompile) — and still runs correctly if the file is absent (defaults win).
## Read-only at runtime: systems pull their values in _ready.

const PATH := "res://data/balance.json"

const DEFAULTS := {
	# cultivation climb
	"realm_span": [50, 120, 300, 750, 1800, 999999],
	"difficulty_per_realm": 12.0,
	# player speed curve
	"player_base_speed": 12.0,
	"player_max_speed": 22.0,
	"player_speed_ramp_time": 90.0,
	"player_speed_creep": 0.07,
	"player_speed_creep_cap": 16.0,
	# spawner cadence
	"spawn_start_interval": 1.4,
	"spawn_min_interval": 0.7,
	"spawn_ramp_time": 60.0,
	"spawn_hard_min_interval": 0.42,
	"spawn_endless_ramp": 200.0,
	"spawn_gate_interval": 11.0,
	"spawn_orb_interval": 2.4,
	"spawn_pill_interval": 9.0,
	# combat / pressure economy
	"qi_max": 100.0,
	"qi_per_kill": 20.0,
	"net_close_rate": 0.025,
	"net_push_per_kill": 0.12,
	# meta rewards
	"daily_base": 80,
	"ach_reward": 150,
}

var _data: Dictionary = {}

func _ready() -> void:
	_data = DEFAULTS.duplicate(true)
	if not FileAccess.file_exists(PATH):
		return
	var f := FileAccess.open(PATH, FileAccess.READ)
	if f == null:
		return
	var txt := f.get_as_text()
	f.close()
	var parsed = JSON.parse_string(txt)
	if parsed is Dictionary:
		for k in parsed:
			_data[k] = parsed[k]   # override only the keys present in the file

func getf(k: String, d: float = 0.0) -> float:
	return float(_data.get(k, d))

func geti(k: String, d: int = 0) -> int:
	return int(_data.get(k, d))

func get_arr(k: String, d: Array = []) -> Array:
	var v = _data.get(k, d)
	return v if v is Array else d

extends Node
## Lightweight SFX hub. For each named sound it looks for a file at
## res://assets/sfx/<name>.{ogg,wav,mp3} and, if found, prepares an
## AudioStreamPlayer. play(name) is a no-op if no file exists yet, so the game
## runs silently until you drop real audio files in (no code changes needed).

const SOUND_DIR := "res://assets/sfx/"
const EXTS: Array[String] = [".ogg", ".wav", ".mp3"]
const SOUNDS: Array[String] = ["start", "slash", "kill", "jump", "slide", "gate_good", "gate_bad", "burst", "death"]

var _players: Dictionary = {}

func _ready() -> void:
	add_to_group("sound")
	for n in SOUNDS:
		var stream := _load_stream(n)
		if stream != null:
			var p := AudioStreamPlayer.new()
			p.stream = stream
			add_child(p)
			_players[n] = p

func _load_stream(n: String) -> AudioStream:
	for ext in EXTS:
		var path: String = SOUND_DIR + n + ext
		if ResourceLoader.exists(path):
			return load(path)
	return null

## Play a named sound if its file is present; otherwise do nothing.
func play(n: String) -> void:
	if _players.has(n):
		_players[n].play()

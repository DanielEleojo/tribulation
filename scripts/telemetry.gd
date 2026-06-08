extends Node
## Autoload "Telemetry": LOCAL-ONLY playtest event log. Appends one JSON object per
## line to user://events.jsonl. Never sent anywhere — pull the file off-device via
## Xcode's container to analyse your own playtests. Because nothing leaves the device,
## the App Store privacy label stays "No Data Collected".
##
## Use it to validate the weeks-to-months curve with real data instead of guesses:
## where players die (realm/li), how often they break through, daily engagement.

const PATH := "user://events.jsonl"
const MAX_BYTES := 524288   # 512 KB — rotate (truncate) past this so it can't grow forever

var enabled: bool = true

## Record an event: kind + a flat dict of fields. A unix timestamp is added.
func event(kind: String, data: Dictionary = {}) -> void:
	if not enabled:
		return
	var rec := {"t": int(Time.get_unix_time_from_system()), "e": kind}
	for k in data:
		rec[k] = data[k]
	var exists := FileAccess.file_exists(PATH)
	var f := FileAccess.open(PATH, FileAccess.READ_WRITE if exists else FileAccess.WRITE)
	if f == null:
		return
	if exists:
		if f.get_length() > MAX_BYTES:
			f.close()
			f = FileAccess.open(PATH, FileAccess.WRITE)   # rotate: start fresh
			if f == null:
				return
		else:
			f.seek_end()
	f.store_line(JSON.stringify(rec))
	f.close()

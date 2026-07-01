// Telemetry.cs — local-only JSONL event logger (nothing leaves device).
// Mirrors Godot game.gd Telemetry singleton: appends to persistentDataPath/events.jsonl,
// rotates (truncates) past 512 KB. No-op on failure. NOT in Tribulation.Core (uses System.IO
// and UnityEngine.Application). App Store: "No Data Collected" — all data stays on device.
using System;
using System.IO;
using UnityEngine;

public static class Telemetry
{
    const long MAX_BYTES = 512 * 1024;
    static string _path;

    static string Path => _path ??= System.IO.Path.Combine(Application.persistentDataPath, "events.jsonl");

    /// <summary>
    /// Append one event line: {"t":<unixSeconds>,"e":"<kind>"[,<fields>]}
    /// fields should be a raw JSON fragment without braces, e.g. "\"streak\":2,\"reward\":80".
    /// </summary>
    public static void Event(string kind, string fields = "")
    {
        try
        {
            long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string comma = string.IsNullOrEmpty(fields) ? "" : "," + fields;
            string line = $"{{\"t\":{t},\"e\":\"{kind}\"{comma}}}\n";
            // Rotate past 512 KB (truncate — matches Godot behaviour)
            var fi = new FileInfo(Path);
            if (fi.Exists && fi.Length > MAX_BYTES)
                File.WriteAllText(Path, "");
            File.AppendAllText(Path, line);
        }
        catch { /* no-op on any IO failure */ }
    }
}

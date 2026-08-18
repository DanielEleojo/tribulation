// iOS caps third-party apps at 60 FPS unless the Info.plist opts in — without this
// key, Bootstrap's targetFrameRate = display refresh silently does nothing on
// ProMotion (120Hz) iPhones. Unity has no player setting for it, so it's stamped
// into the exported Xcode project's Info.plist after every iOS build (same pattern
// as IOSDisplayNamePostProcessor).
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IOSProMotionPostProcessor
{
    [PostProcessBuild]
    public static void EnableProMotion(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
        plist.WriteToFile(plistPath);
    }
}
#endif

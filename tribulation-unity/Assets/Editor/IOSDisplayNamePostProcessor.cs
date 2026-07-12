// Home-screen label: "Tribulation" under the icon, while the App Store listing
// and productName stay "Tribulation Runner". Unity offers no player setting for
// CFBundleDisplayName separate from productName, so it's stamped into the
// exported Xcode project's Info.plist after every iOS build.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IOSDisplayNamePostProcessor
{
    const string DISPLAY_NAME = "Tribulation";

    [PostProcessBuild]
    public static void SetDisplayName(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("CFBundleDisplayName", DISPLAY_NAME);
        plist.WriteToFile(plistPath);
    }
}
#endif

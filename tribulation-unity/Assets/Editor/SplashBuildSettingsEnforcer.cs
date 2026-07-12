// SplashBuildSettingsEnforcer.cs — build-time guarantee for the branded splash setup.
//
// The desired state: NO Unity engine splash (Unity 6 allows disabling it on all
// license tiers) and the native iOS launch screen as the single branded splash
// (type Default + assigned launch images = full-bleed scaleAspectFill storyboard).
//
// These PlayerSettings values proved flaky to persist from tooling — the editor
// re-serialized stale values over them more than once. Re-applying them at the
// start of every player build guarantees the shipped app is correct regardless
// of what ProjectSettings.asset momentarily says in the editor.

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

class SplashBuildSettingsEnforcer : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -100; } } // before other build hooks

    public void OnPreprocessBuild(BuildReport report)
    {
        PlayerSettings.SplashScreen.show          = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.logos         = new PlayerSettings.SplashScreenLogo[0];
        PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.Default);
    }
}

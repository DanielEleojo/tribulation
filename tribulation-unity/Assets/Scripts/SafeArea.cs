using UnityEngine;

// Converts the device Screen.safeArea (notch / Dynamic Island / home indicator) into CANVAS-space
// insets for a Screen-Space-Overlay CanvasScaler UI. UI here is authored in canvas units, so a pixel
// inset is divided by canvas.scaleFactor to match. Every method returns 0 when there is no inset
// (editor Game view, non-notched devices) → callers behave exactly as before. Portrait-locked, so the
// safe area is stable per device and reading it once at HUD build time is sufficient.
public static class SafeArea
{
    static float Scale(Canvas c) => (c != null && c.scaleFactor > 0f) ? c.scaleFactor : 1f;

    // Canvas-space inset from the TOP edge (status bar / notch / Dynamic Island).
    public static float TopInset(Canvas c)
        => Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) / Scale(c);

    // Canvas-space inset from the BOTTOM edge (home indicator).
    public static float BottomInset(Canvas c)
        => Mathf.Max(0f, Screen.safeArea.yMin) / Scale(c);

    // Canvas-space insets from the LEFT / RIGHT edges (landscape notch; ~0 in portrait).
    public static float LeftInset(Canvas c)  => Mathf.Max(0f, Screen.safeArea.xMin) / Scale(c);
    public static float RightInset(Canvas c) => Mathf.Max(0f, Screen.width - Screen.safeArea.xMax) / Scale(c);
}

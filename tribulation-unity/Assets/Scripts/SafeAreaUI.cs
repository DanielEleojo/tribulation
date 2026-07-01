// SafeAreaUI.cs — keeps overlay UI inside Screen.safeArea (notch / Dynamic Island /
// home indicator). CreateRoot() returns a full-stretch "SafeRoot" child of the canvas;
// parent HUD elements to it instead of the canvas and they stay clear of the cutouts.
// Full-screen veils/effects (death dim, net shader image) stay on the canvas itself.
//
// The rect is re-applied whenever Screen.safeArea changes (rotation, windowed resize),
// so it works in the editor Game view, Device Simulator, and on device.

using UnityEngine;

public class SafeAreaUI : MonoBehaviour
{
    RectTransform _rt;
    Rect _applied = new Rect(-1f, -1f, -1f, -1f);

    public static GameObject CreateRoot(GameObject canvasGO)
    {
        var go = new GameObject("SafeRoot", typeof(RectTransform));
        go.transform.SetParent(canvasGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.AddComponent<SafeAreaUI>();
        return go;
    }

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update() { Apply(); }

    void Apply()
    {
        Rect sa = Screen.safeArea;
        if (sa == _applied || Screen.width <= 0 || Screen.height <= 0) return;
        _applied = sa;

        // Convert the pixel-space safe rect to normalized anchors on the canvas.
        var min = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        var max = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        _rt.anchorMin = min;
        _rt.anchorMax = max;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}

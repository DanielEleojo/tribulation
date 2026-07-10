// Haptics.mm — iOS Taptic Engine bridge for Unity. Auto-excluded from non-iOS builds
// (lives under Plugins/iOS/). C entry points invoked from Haptics.cs via DllImport("__Internal").
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator*       _impactLight  = nil;
static UIImpactFeedbackGenerator*       _impactMedium = nil;
static UIImpactFeedbackGenerator*       _impactHeavy  = nil;
static UIImpactFeedbackGenerator*       _impactRigid  = nil;
static UIImpactFeedbackGenerator*       _impactSoft   = nil;
static UINotificationFeedbackGenerator* _notify       = nil;

static void _ensure() {
    if (_notify != nil) return;
    _impactLight  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
    _impactMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
    _impactHeavy  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
    if (@available(iOS 13.0, *)) {
        _impactRigid = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleRigid];
        _impactSoft  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleSoft];
    } else {
        _impactRigid = _impactMedium;
        _impactSoft  = _impactLight;
    }
    _notify = [[UINotificationFeedbackGenerator alloc] init];
}

extern "C" {
    void _HapticPrepare() {
        _ensure();
        [_impactLight prepare]; [_impactMedium prepare]; [_impactHeavy prepare];
        [_impactRigid prepare]; [_impactSoft prepare]; [_notify prepare];
    }
    void _HapticImpact(int style) { // 0=light 1=medium 2=heavy 3=rigid 4=soft
        _ensure();
        UIImpactFeedbackGenerator* g = _impactMedium;
        switch (style) {
            case 0: g = _impactLight;  break;
            case 1: g = _impactMedium; break;
            case 2: g = _impactHeavy;  break;
            case 3: g = _impactRigid;  break;
            case 4: g = _impactSoft;   break;
        }
        [g impactOccurred];
        [g prepare];
    }
    void _HapticNotify(int type) { // 0=success 1=warning 2=error
        _ensure();
        UINotificationFeedbackType t = UINotificationFeedbackTypeSuccess;
        if (type == 1) t = UINotificationFeedbackTypeWarning;
        else if (type == 2) t = UINotificationFeedbackTypeError;
        [_notify notificationOccurred:t];
        [_notify prepare];
    }
}

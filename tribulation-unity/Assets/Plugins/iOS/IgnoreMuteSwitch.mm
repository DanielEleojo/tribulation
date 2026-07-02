// IgnoreMuteSwitch.mm — switch the AVAudioSession to the Playback category so game
// audio plays even when the iPhone's ring/silent switch is muted.
//
// Unity's default iOS audio session is Ambient, which the silent switch silences.
// Unity has no player setting for this, so the category is set natively.
// MixWithOthers keeps the user's own music/podcasts playing alongside the game.
// Called from C# via IOSAudioSession (Bootstrap re-asserts it on focus regain,
// since interruptions like phone calls can reset the session).
//
// Interruption recovery: system UI that resigns the app (screenshot markup,
// Control Center, Siri) interrupts the audio session, and mixable sessions
// (MixWithOthers) often never receive the InterruptionEnded notification — so
// Unity's internal audio output unit stays suspended and the game returns muted.
// Re-setting the category alone doesn't restart that unit; the engine-exported
// UnitySetAudioSessionActive(1) does. Observers below reassert on every
// become-active and on any interruption-ended that does arrive.

#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

// Exported by the Unity engine (UnityFramework): activates the AVAudioSession
// AND resumes Unity's internal audio output.
extern "C" void UnitySetAudioSessionActive(int active);

static void Tribulation_ApplyPlaybackCategory()
{
    AVAudioSession *session = [AVAudioSession sharedInstance];
    [session setCategory:AVAudioSessionCategoryPlayback
             withOptions:AVAudioSessionCategoryOptionMixWithOthers
                   error:nil];
    [session setActive:YES error:nil];
    UnitySetAudioSessionActive(1);
}

extern "C" void Tribulation_SetAudioSessionPlayback()
{
    static BOOL observersInstalled = NO;
    if (!observersInstalled)
    {
        observersInstalled = YES;
        [[NSNotificationCenter defaultCenter]
            addObserverForName:UIApplicationDidBecomeActiveNotification
                        object:nil
                         queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) {
                        Tribulation_ApplyPlaybackCategory();
                    }];
        [[NSNotificationCenter defaultCenter]
            addObserverForName:AVAudioSessionInterruptionNotification
                        object:nil
                         queue:[NSOperationQueue mainQueue]
                    usingBlock:^(NSNotification *note) {
                        NSNumber *type = note.userInfo[AVAudioSessionInterruptionTypeKey];
                        if (type.unsignedIntegerValue == AVAudioSessionInterruptionTypeEnded)
                            Tribulation_ApplyPlaybackCategory();
                    }];
    }
    Tribulation_ApplyPlaybackCategory();
}

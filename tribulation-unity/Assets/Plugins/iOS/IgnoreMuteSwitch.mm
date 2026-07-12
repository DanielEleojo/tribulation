// IgnoreMuteSwitch.mm — switch the AVAudioSession to the Playback category so game
// audio plays even when the iPhone's ring/silent switch is muted.
//
// Unity's default iOS audio session is Ambient, which the silent switch silences.
// Unity has no player setting for this, so the category is set natively.
// MixWithOthers keeps the user's own music/podcasts playing alongside the game.
// Called from C# via IOSAudioSession (Bootstrap re-asserts it on focus regain,
// since interruptions like phone calls can reset the session).

#import <AVFoundation/AVFoundation.h>

extern "C" void Tribulation_SetAudioSessionPlayback()
{
    AVAudioSession *session = [AVAudioSession sharedInstance];
    [session setCategory:AVAudioSessionCategoryPlayback
             withOptions:AVAudioSessionCategoryOptionMixWithOthers
                   error:nil];
    [session setActive:YES error:nil];
}

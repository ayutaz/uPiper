#import <AVFoundation/AVFoundation.h>
#import <Foundation/Foundation.h>

extern "C" {
    /// <summary>
    /// Initialize AVAudioSession for audio playback.
    /// Sets category to AVAudioSessionCategoryPlayback to:
    /// - Override silent switch
    /// - Prioritize audio playback
    /// - Enable background playback (if configured in info.plist)
    /// </summary>
    void InitializeAudioSessionForPlayback() {
        NSError *error = nil;
        AVAudioSession *audioSession = [AVAudioSession sharedInstance];

        // Set Playback category to override silent switch
        // MixWithOthers option allows audio to play alongside other apps
        BOOL success = [audioSession setCategory:AVAudioSessionCategoryPlayback
                                             mode:AVAudioSessionModeDefault
                                          options:AVAudioSessionCategoryOptionMixWithOthers
                                            error:&error];

        if (!success || error) {
            NSLog(@"[uPiper] Failed to set audio session category: %@", error.localizedDescription);
            return;
        }

        // Activate the audio session
        success = [audioSession setActive:YES error:&error];

        if (!success || error) {
            NSLog(@"[uPiper] Failed to activate audio session: %@", error.localizedDescription);
        } else {
            NSLog(@"[uPiper] AudioSession initialized successfully for playback");
            NSLog(@"[uPiper] Category: %@, Mode: %@", audioSession.category, audioSession.mode);
        }
    }

    /// <summary>
    /// Check if other audio is currently playing (e.g., Music app, YouTube).
    /// Returns false if other audio is playing, true otherwise.
    /// </summary>
    bool IsAudioSessionActive() {
        AVAudioSession *audioSession = [AVAudioSession sharedInstance];
        return [audioSession isOtherAudioPlaying] == NO;
    }

    /// <summary>
    /// Get the current AudioSession category name (for debugging).
    /// Returns a C-string that Unity can read.
    /// </summary>
    const char* GetAudioSessionCategory() {
        AVAudioSession *audioSession = [AVAudioSession sharedInstance];
        NSString *category = audioSession.category;

        // Convert NSString to C-string (Unity will marshal this)
        // Note: This returns a pointer to static memory, safe for P/Invoke
        return strdup([category UTF8String]);
    }

    /// <summary>
    /// Get the current output volume (0.0 to 1.0).
    /// This reflects the hardware volume buttons.
    /// </summary>
    float GetOutputVolume() {
        AVAudioSession *audioSession = [AVAudioSession sharedInstance];
        return audioSession.outputVolume;
    }

    /// <summary>
    /// Deactivate the audio session (call when done playing audio).
    /// This allows other apps to resume their audio.
    /// </summary>
    void DeactivateAudioSession() {
        NSError *error = nil;
        AVAudioSession *audioSession = [AVAudioSession sharedInstance];

        [audioSession setActive:NO
                    withOptions:AVAudioSessionSetActiveOptionNotifyOthersOnDeactivation
                          error:&error];

        if (error) {
            NSLog(@"[uPiper] Failed to deactivate audio session: %@", error.localizedDescription);
        } else {
            NSLog(@"[uPiper] AudioSession deactivated");
        }
    }
}

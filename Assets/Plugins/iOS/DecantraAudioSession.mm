#import <AVFoundation/AVFoundation.h>

extern "C" bool DecantraConfigureAudioSession(bool forcePlaybackCategory)
{
    @autoreleasepool
    {
        AVAudioSession* session = [AVAudioSession sharedInstance];
        NSError* categoryError = nil;
        NSError* activationError = nil;

        NSString* category = forcePlaybackCategory ? AVAudioSessionCategoryPlayback : AVAudioSessionCategoryAmbient;
        AVAudioSessionCategoryOptions options = AVAudioSessionCategoryOptionMixWithOthers;

        BOOL categoryConfigured = [session setCategory:category withOptions:options error:&categoryError];
        BOOL sessionActivated = [session setActive:YES error:&activationError];

        return categoryConfigured && sessionActivated;
    }
}

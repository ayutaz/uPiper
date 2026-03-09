mergeInto(LibraryManager.library, {
    WebGL_ResumeAudioContext: function() {
        if (typeof WEBAudio !== 'undefined' && WEBAudio.audioContext) {
            WEBAudio.audioContext.resume();
        }
    },
    WebGL_IsAudioContextResumed: function() {
        if (typeof WEBAudio !== 'undefined' && WEBAudio.audioContext) {
            return WEBAudio.audioContext.state === 'running' ? 1 : 0;
        }
        return 0;
    }
});
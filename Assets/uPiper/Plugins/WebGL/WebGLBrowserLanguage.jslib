mergeInto(LibraryManager.library, {
    WebGL_GetBrowserLanguage: function() {
        var lang = navigator.language || navigator.userLanguage || 'en';
        var bufferSize = lengthBytesUTF8(lang) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(lang, buffer, bufferSize);
        return buffer;
    }
});
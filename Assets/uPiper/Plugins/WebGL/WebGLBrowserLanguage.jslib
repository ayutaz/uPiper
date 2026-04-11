var WebGLBrowserLanguage = {
    _webgl_browser_lang_buffer: 0,

    WebGL_GetBrowserLanguage: function() {
        var lang = navigator.language || navigator.userLanguage || 'en';
        var bufferSize = lengthBytesUTF8(lang) + 1;

        if (_webgl_browser_lang_buffer) {
            _free(_webgl_browser_lang_buffer);
        }
        _webgl_browser_lang_buffer = _malloc(bufferSize);
        stringToUTF8(lang, _webgl_browser_lang_buffer, bufferSize);
        return _webgl_browser_lang_buffer;
    }
};

mergeInto(LibraryManager.library, WebGLBrowserLanguage);
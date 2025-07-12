#include "openjtalk_wrapper.h"
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>
#include <fstream>
#include <sstream>
#include <memory>
#include <thread>
#include <mutex>

#ifdef _WIN32
    #include <windows.h>
    #include <direct.h>
    #define popen _popen
    #define pclose _pclose
    #define PATH_SEPARATOR "\\"
#else
    #include <unistd.h>
    #include <sys/stat.h>
    #define PATH_SEPARATOR "/"
#endif

// Thread-local error message
thread_local std::string g_last_error;

// Mutex for thread safety
std::mutex g_mutex;

// OpenJTalk wrapper implementation
struct OpenJTalkWrapper {
    std::string dictionary_path;
    std::string voice_path;
    std::string temp_dir;
    
    OpenJTalkWrapper() {
        // Initialize temp directory
        #ifdef _WIN32
            char temp_path[MAX_PATH];
            GetTempPathA(MAX_PATH, temp_path);
            temp_dir = std::string(temp_path) + "uPiper_OpenJTalk\\";
            _mkdir(temp_dir.c_str());
        #else
            temp_dir = "/tmp/uPiper_OpenJTalk/";
            mkdir(temp_dir.c_str(), 0755);
        #endif
    }
    
    ~OpenJTalkWrapper() {
        // Cleanup temp files
        cleanup_temp_files();
    }
    
    void cleanup_temp_files() {
        // Remove temporary files if any
    }
};

// Helper function to execute command and get output
std::string exec_command(const std::string& cmd) {
    std::array<char, 128> buffer;
    std::string result;
    
    std::unique_ptr<FILE, decltype(&pclose)> pipe(popen(cmd.c_str(), "r"), pclose);
    if (!pipe) {
        throw std::runtime_error("popen() failed!");
    }
    
    while (fgets(buffer.data(), buffer.size(), pipe.get()) != nullptr) {
        result += buffer.data();
    }
    
    return result;
}

// Check if file exists
bool file_exists(const std::string& path) {
    std::ifstream f(path.c_str());
    return f.good();
}

// Get environment variable
std::string get_env_var(const std::string& var_name, const std::string& default_value = "") {
    const char* value = std::getenv(var_name.c_str());
    return value ? std::string(value) : default_value;
}

extern "C" {

OPENJTALK_API OpenJTalkHandle openjtalk_create() {
    try {
        return new OpenJTalkWrapper();
    } catch (const std::exception& e) {
        g_last_error = std::string("Failed to create OpenJTalk instance: ") + e.what();
        return nullptr;
    }
}

OPENJTALK_API void openjtalk_destroy(OpenJTalkHandle handle) {
    if (handle) {
        delete static_cast<OpenJTalkWrapper*>(handle);
    }
}

OPENJTALK_API int openjtalk_is_available() {
    std::lock_guard<std::mutex> lock(g_mutex);
    
    // Check if open_jtalk binary exists in PATH or specified location
    std::string openjtalk_path = get_env_var("OPENJTALK_PATH", "");
    
    if (!openjtalk_path.empty()) {
        return file_exists(openjtalk_path) ? 1 : 0;
    }
    
    // Try to find in PATH
    try {
        #ifdef _WIN32
            std::string result = exec_command("where open_jtalk 2>NUL");
        #else
            std::string result = exec_command("which open_jtalk 2>/dev/null");
        #endif
        return !result.empty() ? 1 : 0;
    } catch (...) {
        return 0;
    }
}

OPENJTALK_API int openjtalk_ensure_dictionary() {
    std::lock_guard<std::mutex> lock(g_mutex);
    
    // Check if dictionary exists
    std::string dict_path = get_env_var("OPENJTALK_DICTIONARY_DIR", "");
    
    if (!dict_path.empty() && file_exists(dict_path + "/sys.dic")) {
        return 1;
    }
    
    // Check common locations
    std::vector<std::string> common_paths = {
        "/usr/share/open_jtalk/dic/",
        "/usr/local/share/open_jtalk/dic/",
        "C:\\OpenJTalk\\dic\\",
        "./dic/"
    };
    
    for (const auto& path : common_paths) {
        if (file_exists(path + "sys.dic")) {
            return 1;
        }
    }
    
    // Check if auto-download is disabled
    std::string auto_download = get_env_var("PIPER_AUTO_DOWNLOAD_DICT", "1");
    std::string offline_mode = get_env_var("PIPER_OFFLINE_MODE", "0");
    
    if (auto_download == "0" || offline_mode == "1") {
        g_last_error = "Dictionary not found and auto-download is disabled";
        return 0;
    }
    
    // TODO: Implement dictionary download
    g_last_error = "Dictionary auto-download not yet implemented";
    return 0;
}

OPENJTALK_API int openjtalk_text_to_phonemes(
    OpenJTalkHandle handle,
    const char* text,
    char** phonemes_out,
    int* phonemes_len
) {
    if (!handle || !text || !phonemes_out || !phonemes_len) {
        g_last_error = "Invalid parameters";
        return 0;
    }
    
    std::lock_guard<std::mutex> lock(g_mutex);
    
    try {
        OpenJTalkWrapper* wrapper = static_cast<OpenJTalkWrapper*>(handle);
        
        // Create temporary files
        std::string input_file = wrapper->temp_dir + "input.txt";
        std::string output_file = wrapper->temp_dir + "output.lab";
        
        // Write input text to file
        std::ofstream ofs(input_file, std::ios::binary);
        if (!ofs) {
            g_last_error = "Failed to create input file";
            return 0;
        }
        ofs << text;
        ofs.close();
        
        // Get dictionary path
        std::string dict_path = get_env_var("OPENJTALK_DICTIONARY_DIR", "/usr/share/open_jtalk/dic");
        
        // Build command
        std::stringstream cmd;
        cmd << "open_jtalk";
        cmd << " -x \"" << dict_path << "\"";
        cmd << " -ot \"" << output_file << "\"";
        cmd << " \"" << input_file << "\"";
        cmd << " 2>/dev/null";
        
        // Execute OpenJTalk
        std::string result = exec_command(cmd.str());
        
        // Read phonemes from output file
        std::ifstream ifs(output_file);
        if (!ifs) {
            g_last_error = "Failed to read output file";
            return 0;
        }
        
        std::stringstream phonemes_stream;
        std::string line;
        std::vector<std::string> phonemes;
        
        // Parse label file and extract phonemes
        while (std::getline(ifs, line)) {
            // OpenJTalk label format: time time phoneme_info
            // Extract phoneme from the label
            size_t pos1 = line.find('-');
            size_t pos2 = line.find('+');
            
            if (pos1 != std::string::npos && pos2 != std::string::npos && pos2 > pos1) {
                std::string phoneme = line.substr(pos1 + 1, pos2 - pos1 - 1);
                if (phoneme != "sil" && phoneme != "pau") {  // Skip silence
                    phonemes.push_back(phoneme);
                }
            }
        }
        ifs.close();
        
        // Join phonemes with spaces
        std::string phonemes_str;
        for (size_t i = 0; i < phonemes.size(); ++i) {
            if (i > 0) phonemes_str += " ";
            phonemes_str += phonemes[i];
        }
        
        // Allocate and copy result
        *phonemes_len = phonemes_str.length();
        *phonemes_out = (char*)malloc(*phonemes_len + 1);
        if (!*phonemes_out) {
            g_last_error = "Memory allocation failed";
            return 0;
        }
        
        strcpy(*phonemes_out, phonemes_str.c_str());
        
        // Cleanup temp files
        std::remove(input_file.c_str());
        std::remove(output_file.c_str());
        
        return 1;
        
    } catch (const std::exception& e) {
        g_last_error = std::string("Exception: ") + e.what();
        return 0;
    }
}

OPENJTALK_API void openjtalk_free_phonemes(char* phonemes) {
    if (phonemes) {
        free(phonemes);
    }
}

OPENJTALK_API const char* openjtalk_get_last_error() {
    return g_last_error.c_str();
}

OPENJTALK_API const char* openjtalk_get_version() {
    return "uPiper OpenJTalk Wrapper 1.0.0";
}

} // extern "C"
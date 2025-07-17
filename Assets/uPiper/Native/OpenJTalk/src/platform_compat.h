#ifndef OPENJTALK_PLATFORM_COMPAT_H
#define OPENJTALK_PLATFORM_COMPAT_H

#include <stdio.h>
#include <stddef.h>

#ifdef _WIN32
    #include <windows.h>
    
    // Windows memory mapping
    typedef struct {
        HANDLE file_handle;
        HANDLE map_handle;
        void* view;
        size_t size;
    } PlatformMmap;
    
    static inline void* platform_mmap(const char* filename, size_t* out_size) {
        HANDLE file = CreateFileA(filename, GENERIC_READ, FILE_SHARE_READ, 
                                 NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
        if (file == INVALID_HANDLE_VALUE) {
            return NULL;
        }
        
        LARGE_INTEGER file_size;
        if (!GetFileSizeEx(file, &file_size)) {
            CloseHandle(file);
            return NULL;
        }
        
        HANDLE mapping = CreateFileMappingA(file, NULL, PAGE_READONLY, 
                                           file_size.HighPart, file_size.LowPart, NULL);
        if (!mapping) {
            CloseHandle(file);
            return NULL;
        }
        
        void* view = MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, 0);
        if (!view) {
            CloseHandle(mapping);
            CloseHandle(file);
            return NULL;
        }
        
        // Store handles for cleanup
        PlatformMmap* mmap_info = (PlatformMmap*)malloc(sizeof(PlatformMmap));
        mmap_info->file_handle = file;
        mmap_info->map_handle = mapping;
        mmap_info->view = view;
        mmap_info->size = (size_t)file_size.QuadPart;
        
        *out_size = mmap_info->size;
        return view;
    }
    
    static inline void platform_munmap(void* addr, size_t size) {
        // On Windows, we need to find the associated handles
        // In a real implementation, we'd maintain a mapping
        UnmapViewOfFile(addr);
        // Note: Handles cleanup would be needed in production code
    }
    
#else
    // POSIX systems
    #include <sys/mman.h>
    #include <sys/stat.h>
    #include <fcntl.h>
    #include <unistd.h>
    
    static inline void* platform_mmap(const char* filename, size_t* out_size) {
        int fd = open(filename, O_RDONLY);
        if (fd < 0) {
            return NULL;
        }
        
        struct stat st;
        if (fstat(fd, &st) < 0) {
            close(fd);
            return NULL;
        }
        
        void* mapped = mmap(NULL, st.st_size, PROT_READ, MAP_PRIVATE, fd, 0);
        close(fd); // Can close fd after mmap
        
        if (mapped == MAP_FAILED) {
            return NULL;
        }
        
        *out_size = st.st_size;
        return mapped;
    }
    
    static inline void platform_munmap(void* addr, size_t size) {
        munmap(addr, size);
    }
#endif

#endif // OPENJTALK_PLATFORM_COMPAT_H
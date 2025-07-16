#ifndef MEMORY_POOL_H
#define MEMORY_POOL_H

#include <stddef.h>

// Memory pool structure
typedef struct MemoryPool MemoryPool;

// Create and destroy memory pool
MemoryPool* memory_pool_create(size_t initial_size);
void memory_pool_destroy(MemoryPool* pool);

// Allocate memory from pool
void* memory_pool_alloc(MemoryPool* pool, size_t size);
char* memory_pool_strdup(MemoryPool* pool, const char* str);

// Reset pool (free all allocations)
void memory_pool_reset(MemoryPool* pool);

// Get pool statistics
size_t memory_pool_get_used(MemoryPool* pool);
size_t memory_pool_get_total(MemoryPool* pool);

#endif // MEMORY_POOL_H
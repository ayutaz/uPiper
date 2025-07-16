#include "memory_pool.h"
#include <stdlib.h>
#include <string.h>

typedef struct MemoryBlock {
    struct MemoryBlock* next;
    size_t size;
    size_t used;
    char data[];
} MemoryBlock;

struct MemoryPool {
    MemoryBlock* blocks;
    MemoryBlock* current;
    size_t block_size;
    size_t total_allocated;
    size_t total_used;
};

// Create memory pool
MemoryPool* memory_pool_create(size_t initial_size) {
    MemoryPool* pool = (MemoryPool*)malloc(sizeof(MemoryPool));
    if (!pool) {
        return NULL;
    }
    
    pool->block_size = initial_size;
    pool->total_allocated = 0;
    pool->total_used = 0;
    
    // Allocate first block
    MemoryBlock* block = (MemoryBlock*)malloc(sizeof(MemoryBlock) + initial_size);
    if (!block) {
        free(pool);
        return NULL;
    }
    
    block->next = NULL;
    block->size = initial_size;
    block->used = 0;
    
    pool->blocks = block;
    pool->current = block;
    pool->total_allocated = initial_size;
    
    return pool;
}

// Destroy memory pool
void memory_pool_destroy(MemoryPool* pool) {
    if (!pool) {
        return;
    }
    
    MemoryBlock* block = pool->blocks;
    while (block) {
        MemoryBlock* next = block->next;
        free(block);
        block = next;
    }
    
    free(pool);
}

// Allocate memory from pool
void* memory_pool_alloc(MemoryPool* pool, size_t size) {
    if (!pool || size == 0) {
        return NULL;
    }
    
    // Align size to 8 bytes
    size = (size + 7) & ~7;
    
    // Check if current block has enough space
    if (pool->current->used + size > pool->current->size) {
        // Need new block
        size_t new_block_size = pool->block_size;
        if (size > new_block_size) {
            new_block_size = size * 2; // Ensure block is large enough
        }
        
        MemoryBlock* new_block = (MemoryBlock*)malloc(sizeof(MemoryBlock) + new_block_size);
        if (!new_block) {
            return NULL;
        }
        
        new_block->next = NULL;
        new_block->size = new_block_size;
        new_block->used = 0;
        
        // Add to list
        pool->current->next = new_block;
        pool->current = new_block;
        pool->total_allocated += new_block_size;
    }
    
    // Allocate from current block
    void* ptr = pool->current->data + pool->current->used;
    pool->current->used += size;
    pool->total_used += size;
    
    return ptr;
}

// Duplicate string in pool
char* memory_pool_strdup(MemoryPool* pool, const char* str) {
    if (!pool || !str) {
        return NULL;
    }
    
    size_t len = strlen(str) + 1;
    char* copy = (char*)memory_pool_alloc(pool, len);
    if (copy) {
        memcpy(copy, str, len);
    }
    
    return copy;
}

// Reset pool (keep blocks but mark as unused)
void memory_pool_reset(MemoryPool* pool) {
    if (!pool) {
        return;
    }
    
    MemoryBlock* block = pool->blocks;
    while (block) {
        block->used = 0;
        block = block->next;
    }
    
    pool->current = pool->blocks;
    pool->total_used = 0;
}

// Get statistics
size_t memory_pool_get_used(MemoryPool* pool) {
    return pool ? pool->total_used : 0;
}

size_t memory_pool_get_total(MemoryPool* pool) {
    return pool ? pool->total_allocated : 0;
}
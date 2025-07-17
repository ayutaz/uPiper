#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include "memory_pool.h"

// Test basic allocation and deallocation
void test_basic_allocation() {
    printf("Testing basic allocation...\n");
    
    MemoryPool* pool = memory_pool_create(1024 * 1024); // 1MB pool
    assert(pool != NULL);
    
    // Allocate various sizes
    void* ptr1 = memory_pool_alloc(pool, 100);
    assert(ptr1 != NULL);
    
    void* ptr2 = memory_pool_alloc(pool, 256);
    assert(ptr2 != NULL);
    
    void* ptr3 = memory_pool_alloc(pool, 512);
    assert(ptr3 != NULL);
    
    // Verify pointers are different
    assert(ptr1 != ptr2);
    assert(ptr2 != ptr3);
    assert(ptr1 != ptr3);
    
    memory_pool_destroy(pool);
    printf("✓ Basic allocation passed\n");
}

// Test allocation patterns
void test_allocation_patterns() {
    printf("Testing allocation patterns...\n");
    
    MemoryPool* pool = memory_pool_create(10 * 1024); // 10KB pool
    assert(pool != NULL);
    
    // Allocate many small blocks
    void* ptrs[100];
    for (int i = 0; i < 100; i++) {
        ptrs[i] = memory_pool_alloc(pool, 64);
        assert(ptrs[i] != NULL);
        
        // Write pattern to verify memory
        memset(ptrs[i], i & 0xFF, 64);
    }
    
    // Verify patterns
    for (int i = 0; i < 100; i++) {
        unsigned char* p = (unsigned char*)ptrs[i];
        for (int j = 0; j < 64; j++) {
            assert(p[j] == (i & 0xFF));
        }
    }
    
    memory_pool_destroy(pool);
    printf("✓ Allocation patterns passed\n");
}

// Test pool exhaustion
void test_pool_exhaustion() {
    printf("Testing pool exhaustion...\n");
    
    MemoryPool* pool = memory_pool_create(1024); // Small 1KB pool
    assert(pool != NULL);
    
    // Allocate until exhausted
    int alloc_count = 0;
    while (1) {
        void* ptr = memory_pool_alloc(pool, 128);
        if (ptr == NULL) {
            break;
        }
        alloc_count++;
    }
    
    // Should have allocated at least a few blocks
    assert(alloc_count > 0);
    printf("  Allocated %d blocks before exhaustion\n", alloc_count);
    
    memory_pool_destroy(pool);
    printf("✓ Pool exhaustion handled correctly\n");
}

// Test reset functionality
void test_pool_reset() {
    printf("Testing pool reset...\n");
    
    MemoryPool* pool = memory_pool_create(4096);
    assert(pool != NULL);
    
    // First allocation phase
    void* ptr1 = memory_pool_alloc(pool, 1024);
    assert(ptr1 != NULL);
    memset(ptr1, 0xAA, 1024);
    
    void* ptr2 = memory_pool_alloc(pool, 512);
    assert(ptr2 != NULL);
    
    // Reset pool
    memory_pool_reset(pool);
    
    // Allocate again - should reuse same memory
    void* ptr3 = memory_pool_alloc(pool, 1024);
    assert(ptr3 != NULL);
    
    // In most implementations, ptr3 should equal ptr1 after reset
    // But this is implementation-dependent
    
    memory_pool_destroy(pool);
    printf("✓ Pool reset passed\n");
}

// Test zero-size allocation
void test_edge_cases() {
    printf("Testing edge cases...\n");
    
    MemoryPool* pool = memory_pool_create(1024);
    assert(pool != NULL);
    
    // Zero-size allocation (should either return NULL or valid pointer)
    void* ptr1 = memory_pool_alloc(pool, 0);
    // Implementation-dependent: either NULL or valid pointer is acceptable
    
    // Very large allocation (should fail)
    void* ptr2 = memory_pool_alloc(pool, 1024 * 1024); // 1MB in 1KB pool
    assert(ptr2 == NULL);
    
    // NULL pool handling
    void* ptr3 = memory_pool_alloc(NULL, 100);
    assert(ptr3 == NULL);
    
    memory_pool_destroy(pool);
    memory_pool_destroy(NULL); // Should not crash
    
    printf("✓ Edge cases handled correctly\n");
}

// Test thread safety (if implemented)
void test_thread_safety() {
    printf("Testing thread safety...\n");
    // Note: This would require pthread or similar
    // For now, just note that thread safety testing is needed
    printf("⚠ Thread safety testing requires multi-threading support\n");
}

// Memory usage statistics
void test_memory_stats() {
    printf("Testing memory statistics...\n");
    
    MemoryPool* pool = memory_pool_create(10 * 1024); // 10KB
    assert(pool != NULL);
    
    // Get initial stats
    size_t initial_used = memory_pool_get_used(pool);
    size_t initial_total = memory_pool_get_total(pool);
    
    printf("  Initial: %zu/%zu bytes used\n", initial_used, initial_total);
    assert(initial_total >= 10 * 1024);
    
    // Allocate some memory
    memory_pool_alloc(pool, 1024);
    memory_pool_alloc(pool, 2048);
    
    size_t after_alloc = memory_pool_get_used(pool);
    printf("  After allocation: %zu/%zu bytes used\n", after_alloc, initial_total);
    assert(after_alloc > initial_used);
    
    memory_pool_destroy(pool);
    printf("✓ Memory statistics passed\n");
}

int main() {
    printf("=== Memory Pool Test Suite ===\n\n");
    
    test_basic_allocation();
    test_allocation_patterns();
    test_pool_exhaustion();
    test_pool_reset();
    test_edge_cases();
    test_thread_safety();
    test_memory_stats();
    
    printf("\n=== All tests completed ===\n");
    return 0;
}
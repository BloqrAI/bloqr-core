"""
Tests for async functionality in the rules compiler.
"""

import asyncio
import tempfile
from pathlib import Path

import pytest

from bloqr_compiler import (
    BloqrCompiler,
    compile_rules_async,
    count_rules_async,
    compute_hash_async,
)


@pytest.mark.asyncio
async def test_count_rules_async() -> None:
    """Test async rule counting."""
    with tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False) as f:
        f.write("! Comment\n")
        f.write("example.com\n")
        f.write("# Another comment\n")
        f.write("test.com\n")
        f.write("\n")
        f.write("ads.net\n")
        temp_path = f.name

    try:
        count = await count_rules_async(temp_path)
        assert count == 3  # Only non-comment, non-empty lines
    finally:
        Path(temp_path).unlink()


@pytest.mark.asyncio
async def test_compute_hash_async() -> None:
    """Test async hash computation."""
    with tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False) as f:
        f.write("test content\n")
        temp_path = f.name

    try:
        hash_value = await compute_hash_async(temp_path)
        assert len(hash_value) == 96  # SHA-384 produces 96 hex characters
        assert all(c in "0123456789abcdef" for c in hash_value.lower())
    finally:
        Path(temp_path).unlink()


@pytest.mark.asyncio
async def test_compiler_async_method() -> None:
    """Test BloqrCompiler.compile_async method exists and is callable."""
    compiler = BloqrCompiler()
    
    # Just verify the method exists and is a coroutine function
    assert hasattr(compiler, "compile_async")
    assert asyncio.iscoroutinefunction(compiler.compile_async)


@pytest.mark.asyncio
async def test_compile_rules_async_function() -> None:
    """Test compile_rules_async function exists and is callable."""
    assert asyncio.iscoroutinefunction(compile_rules_async)


def test_backwards_compatibility() -> None:
    """Test that synchronous API still works."""
    from bloqr_compiler import BloqrCompiler, compile_rules
    
    compiler = BloqrCompiler()
    
    # Verify sync methods exist
    assert hasattr(compiler, "compile")
    assert callable(compiler.compile)
    assert callable(compile_rules)


@pytest.mark.asyncio
async def test_parallel_async_operations() -> None:
    """Test that multiple async operations can run in parallel."""
    # Create test files
    test_files = []
    for i in range(3):
        with tempfile.NamedTemporaryFile(mode="w", suffix=".txt", delete=False) as f:
            f.write(f"rule{i}.com\n")
            test_files.append(f.name)

    try:
        # Run multiple async operations in parallel
        tasks = [count_rules_async(path) for path in test_files]
        results = await asyncio.gather(*tasks)
        
        assert len(results) == 3
        assert all(count == 1 for count in results)
    finally:
        for path in test_files:
            Path(path).unlink()


if __name__ == "__main__":
    pytest.main([__file__, "-v"])

#!/usr/bin/env python3
"""ONNX FP16 quantization script for uPiper models.

Usage:
    uv run --with onnx --with onnxconverter-common scripts/quantize_fp16.py [input_path] [output_path]

    If no arguments, processes all models in Assets/uPiper/Resources/Models/
"""

import os
import sys
from pathlib import Path


def get_file_size_mb(path: str) -> float:
    """Return file size in megabytes."""
    return os.path.getsize(path) / (1024 * 1024)


def convert_to_fp16(input_path: str, output_path: str) -> None:
    """Convert an ONNX model from FP32 to FP16.

    Args:
        input_path: Path to the input FP32 ONNX model.
        output_path: Path to save the FP16 ONNX model.
    """
    import onnx
    from onnxconverter_common.float16 import convert_float_to_float16

    if not os.path.isfile(input_path):
        print(f"Error: Input file not found: {input_path}", file=sys.stderr)
        sys.exit(1)

    input_size = get_file_size_mb(input_path)
    print(f"Loading model: {input_path} ({input_size:.1f} MB)")

    model = onnx.load(input_path)
    model_fp16 = convert_float_to_float16(model, keep_io_types=True)

    onnx.save(model_fp16, output_path)

    output_size = get_file_size_mb(output_path)
    ratio = (1 - output_size / input_size) * 100 if input_size > 0 else 0
    print(f"Saved FP16 model: {output_path} ({output_size:.1f} MB)")
    print(f"Size reduction: {input_size:.1f} MB -> {output_size:.1f} MB ({ratio:.1f}% smaller)")


def find_models(models_dir: str) -> list:
    """Find all .onnx files in the given directory, excluding *_fp16.onnx."""
    if not os.path.isdir(models_dir):
        print(f"Error: Models directory not found: {models_dir}", file=sys.stderr)
        sys.exit(1)

    models = []
    for f in sorted(Path(models_dir).glob("*.onnx")):
        if not f.stem.endswith("_fp16"):
            models.append(str(f))
    return models


def main() -> None:
    args = sys.argv[1:]

    if len(args) == 0:
        # Process all models in default directory
        script_dir = Path(__file__).resolve().parent
        project_root = script_dir.parent
        models_dir = project_root / "Assets" / "uPiper" / "Resources" / "Models"

        models = find_models(str(models_dir))
        if not models:
            print(f"No .onnx models found in {models_dir}")
            sys.exit(0)

        print(f"Found {len(models)} model(s) to convert:\n")
        for model_path in models:
            stem = Path(model_path).stem
            output_path = str(Path(model_path).parent / f"{stem}_fp16.onnx")
            convert_to_fp16(model_path, output_path)
            print()

    elif len(args) == 1:
        input_path = args[0]
        stem = Path(input_path).stem
        output_path = str(Path(input_path).parent / f"{stem}_fp16.onnx")
        convert_to_fp16(input_path, output_path)

    elif len(args) == 2:
        convert_to_fp16(args[0], args[1])

    else:
        print("Usage: quantize_fp16.py [input_path] [output_path]", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

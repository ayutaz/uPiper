#!/usr/bin/env python3
"""
piper/piper-plus の ONNX モデルから If オペレータを除去し、
durations 出力を Unity Sentis で利用可能にする変換スクリプト。

Unity Sentis は ONNX の If オペレータをサポートしないため、
durations 出力がモデルロード時に認識されない。
このスクリプトは If ノード内の then_branch (Squeeze) を
直接グラフに展開して If を除去する。

使用方法:
    uv run --with onnx python scripts/remove_if_operator.py <input.onnx> [output.onnx]

    output.onnx を省略した場合、入力ファイルを上書きする。

例:
    # 別ファイルに出力
    uv run --with onnx python scripts/remove_if_operator.py model.onnx model_fixed.onnx

    # 上書き
    uv run --with onnx python scripts/remove_if_operator.py model.onnx
"""

import sys
import copy
import onnx
from onnx import helper


def remove_if_operator(input_path: str, output_path: str) -> bool:
    """If オペレータを除去し、durations 出力を直接公開する。

    Returns:
        True: 変換成功, False: If ノードが見つからない（変換不要）
    """
    model = onnx.load(input_path)
    graph = model.graph

    # If ノードを探す
    if_node = None
    if_idx = None
    for i, node in enumerate(graph.node):
        if node.op_type == "If":
            if_node = node
            if_idx = i
            break

    if if_node is None:
        print(f"[INFO] No If operator found in '{input_path}'. No conversion needed.")
        return False

    print(f"[INFO] Found If node at index {if_idx}")
    print(f"  inputs:  {list(if_node.input)}")
    print(f"  outputs: {list(if_node.output)}")

    # then_branch を取得
    then_branch = None
    for attr in if_node.attribute:
        if attr.name == "then_branch":
            then_branch = attr.g
            break

    if then_branch is None or len(then_branch.node) < 2:
        print("[ERROR] Unexpected then_branch structure. Aborting.")
        return False

    # then_branch: Constant (axes) -> Squeeze (durations)
    constant_node = then_branch.node[0]
    squeeze_node = then_branch.node[1]

    print(f"  then_branch: {constant_node.op_type} -> {squeeze_node.op_type}")

    # 新しい Constant ノード（Squeeze の axes）
    new_constant = copy.deepcopy(constant_node)
    new_constant.output[0] = "durations_squeeze_axes"

    # 新しい Squeeze ノード
    # 入力: squeeze_node の最初の入力（元グラフ内の中間テンソル）+ axes
    squeeze_input = squeeze_node.input[0]
    new_squeeze = helper.make_node(
        "Squeeze",
        inputs=[squeeze_input, "durations_squeeze_axes"],
        outputs=[if_node.output[0]],  # If の出力名を引き継ぐ（通常 "durations"）
        name="durations_squeeze",
    )

    # If ノードを削除し、Constant + Squeeze を挿入
    nodes = list(graph.node)
    nodes.pop(if_idx)
    nodes.insert(if_idx, new_constant)
    nodes.insert(if_idx + 1, new_squeeze)

    del graph.node[:]
    graph.node.extend(nodes)

    # 保存
    onnx.save(model, output_path)

    # 検証
    model2 = onnx.load(output_path)
    has_if = any(n.op_type == "If" for n in model2.graph.node)
    output_names = [o.name for o in model2.graph.output]

    print(f"\n[RESULT] Saved to '{output_path}'")
    print(f"  Outputs: {output_names}")
    print(f"  Has If operator: {has_if}")

    try:
        onnx.checker.check_model(model2)
        print("  ONNX validation: PASSED")
    except onnx.checker.ValidationError as e:
        print(f"  ONNX validation: FAILED ({e})")

    return True


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    input_path = sys.argv[1]
    output_path = sys.argv[2] if len(sys.argv) >= 3 else input_path

    print(f"[INFO] Input:  {input_path}")
    print(f"[INFO] Output: {output_path}")
    print()

    success = remove_if_operator(input_path, output_path)
    if not success:
        sys.exit(0)


if __name__ == "__main__":
    main()

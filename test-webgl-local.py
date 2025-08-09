#!/usr/bin/env python3
"""
Unity WebGL OpenJTalk ローカルテストサーバー
M5: Unity WebGLビルドとローカルテスト用
"""

import http.server
import socketserver
import os
import sys
import webbrowser
import time
from pathlib import Path

class CORSRequestHandler(http.server.SimpleHTTPRequestHandler):
    """CORS対応のHTTPリクエストハンドラー"""
    
    def end_headers(self):
        # CORS headers
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        
        # SharedArrayBuffer対応
        self.send_header('Cross-Origin-Resource-Policy', 'cross-origin')
        
        # Cache control
        self.send_header('Cache-Control', 'no-cache, no-store, must-revalidate')
        
        super().end_headers()
    
    def do_OPTIONS(self):
        """OPTIONSリクエストの処理"""
        self.send_response(200)
        self.end_headers()
    
    def guess_type(self, path):
        """MIMEタイプの推定（WebAssembly対応）"""
        mimetype = super().guess_type(path)
        if path.endswith('.wasm'):
            return 'application/wasm'
        if path.endswith('.js'):
            return 'application/javascript'
        if path.endswith('.json'):
            return 'application/json'
        return mimetype

def find_webgl_build():
    """WebGLビルドディレクトリを探す"""
    possible_paths = [
        'WebGLBuild',
        'Build/WebGL',
        'Builds/WebGL',
        'build',
        '.'
    ]
    
    for path_str in possible_paths:
        path = Path(path_str)
        if path.exists() and path.is_dir():
            # index.htmlがあるか確認
            if (path / 'index.html').exists():
                return path
            # Build/index.htmlパターンも確認
            if (path / 'Build').exists() and (path / 'index.html').exists():
                return path
    
    return None

def check_required_files(build_dir):
    """必要なファイルの存在確認"""
    required_files = [
        'index.html',
        'StreamingAssets/openjtalk-unity.js',
        'StreamingAssets/openjtalk-unity.wasm',
        'StreamingAssets/openjtalk-unity-wrapper.js'
    ]
    
    missing_files = []
    for file_path in required_files:
        if not (build_dir / file_path).exists():
            missing_files.append(file_path)
    
    return missing_files

def main():
    """メイン処理"""
    print("=" * 60)
    print("🚀 Unity WebGL OpenJTalk ローカルテストサーバー")
    print("   M5: Unity WebGLビルドとローカルテスト")
    print("=" * 60)
    
    # ポート設定
    port = 8000
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            print(f"⚠️  無効なポート番号: {sys.argv[1]}")
            sys.exit(1)
    
    # WebGLビルドディレクトリを探す
    build_dir = find_webgl_build()
    
    if not build_dir:
        print("❌ WebGLビルドが見つかりません")
        print("\n📝 Unity WebGLビルドの手順:")
        print("1. Unity Editorで File > Build Settings を開く")
        print("2. Platform: WebGL を選択")
        print("3. Switch Platform をクリック")
        print("4. Build をクリック")
        print("5. ビルド出力先を 'WebGLBuild' に指定")
        sys.exit(1)
    
    print(f"✅ WebGLビルドを検出: {build_dir.absolute()}")
    
    # 必要なファイルの確認
    missing_files = check_required_files(build_dir)
    if missing_files:
        print("\n⚠️  以下のファイルが見つかりません:")
        for file in missing_files:
            print(f"   - {file}")
        print("\n💡 M3/M4の成果物をStreamingAssetsにコピーしてください")
    
    # サーバー起動
    os.chdir(build_dir)
    
    Handler = CORSRequestHandler
    Handler.extensions_map.update({
        '.wasm': 'application/wasm',
        '.js': 'application/javascript',
        '.json': 'application/json',
    })
    
    try:
        with socketserver.TCPServer(("", port), Handler) as httpd:
            server_url = f"http://localhost:{port}"
            print(f"\n🌐 サーバー起動: {server_url}")
            print(f"📁 配信ディレクトリ: {build_dir.absolute()}")
            print("\n" + "=" * 60)
            print("📝 テスト手順:")
            print("1. ブラウザが自動的に開きます")
            print("2. Unity WebGLの読み込みを待つ")
            print("3. Developer Console (F12) を開く")
            print("4. 以下のコマンドでテスト:")
            print("   > window.OpenJTalkUnityAPI.phonemize('こんにちは')")
            print("=" * 60)
            print("\n⚠️  終了するには Ctrl+C を押してください\n")
            
            # ブラウザを自動的に開く
            time.sleep(1)
            webbrowser.open(server_url)
            
            # サーバーを起動
            httpd.serve_forever()
            
    except KeyboardInterrupt:
        print("\n\n✅ サーバーを停止しました")
    except OSError as e:
        if e.errno == 48 or e.errno == 10048:  # Address already in use
            print(f"\n❌ ポート {port} は既に使用されています")
            print(f"💡 別のポートを指定してください: python {sys.argv[0]} 8080")
        else:
            print(f"\n❌ サーバーエラー: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
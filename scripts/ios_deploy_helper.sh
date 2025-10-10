#!/bin/bash

# iOS Deployment Helper Script for uPiper
# このスクリプトはiOS実機テストを支援します

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
XCODE_PROJECT=""
DEVICE_NAME=""
BUNDLE_ID="com.yourcompany.uPiper"

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check prerequisites
check_prerequisites() {
    print_info "前提条件をチェック中..."

    # Check if Xcode is installed
    if ! command -v xcodebuild &> /dev/null; then
        print_error "Xcodeがインストールされていません"
        echo "https://developer.apple.com/xcode/ からXcodeをインストールしてください"
        exit 1
    fi

    # Check Xcode version
    XCODE_VERSION=$(xcodebuild -version | head -1 | awk '{print $2}')
    print_info "Xcode バージョン: $XCODE_VERSION"

    # Check if iOS device is connected
    DEVICES=$(xcrun xctrace list devices 2>&1 | grep -E "iPhone|iPad" | grep -v "Simulator" || true)
    if [ -z "$DEVICES" ]; then
        print_warning "iOS デバイスが検出されません"
        echo "デバイスをUSBケーブルで接続してください"
    else
        print_success "接続されているiOSデバイス:"
        echo "$DEVICES"
    fi
}

# Function to list available Xcode projects
list_xcode_projects() {
    print_info "Xcodeプロジェクトを検索中..."

    # Find Unity-iPhone.xcodeproj in common locations
    PROJECTS=$(find . -name "Unity-iPhone.xcodeproj" -maxdepth 3 2>/dev/null || true)

    if [ -z "$PROJECTS" ]; then
        print_warning "Unity-iPhone.xcodeproj が見つかりません"
        echo "UnityでiOSビルドを実行してください"
        return 1
    else
        print_success "見つかったプロジェクト:"
        echo "$PROJECTS"

        # Use the first found project
        XCODE_PROJECT=$(echo "$PROJECTS" | head -1)
        print_info "使用するプロジェクト: $XCODE_PROJECT"
    fi
}

# Function to open Xcode project
open_xcode_project() {
    if [ -z "$XCODE_PROJECT" ]; then
        list_xcode_projects
    fi

    if [ -n "$XCODE_PROJECT" ]; then
        print_info "Xcodeでプロジェクトを開いています..."
        open "$XCODE_PROJECT"
        print_success "Xcodeが開きました"
    fi
}

# Function to clean build
clean_build() {
    if [ -z "$XCODE_PROJECT" ]; then
        list_xcode_projects
    fi

    if [ -n "$XCODE_PROJECT" ]; then
        print_info "ビルドをクリーンアップ中..."
        xcodebuild clean -project "$XCODE_PROJECT" -scheme Unity-iPhone
        print_success "クリーンアップ完了"
    fi
}

# Function to build for device
build_for_device() {
    if [ -z "$XCODE_PROJECT" ]; then
        list_xcode_projects
    fi

    if [ -n "$XCODE_PROJECT" ]; then
        print_info "デバイス用にビルド中..."

        # Get connected device ID
        DEVICE_ID=$(xcrun xctrace list devices 2>&1 | grep -E "iPhone|iPad" | grep -v "Simulator" | head -1 | sed -n 's/.*(\(.*\)).*/\1/p')

        if [ -z "$DEVICE_ID" ]; then
            print_error "デバイスが見つかりません"
            return 1
        fi

        print_info "デバイスID: $DEVICE_ID"

        # Build for device
        xcodebuild -project "$XCODE_PROJECT" \
            -scheme Unity-iPhone \
            -destination "id=$DEVICE_ID" \
            -configuration Release \
            build

        print_success "ビルド完了"
    fi
}

# Function to check code signing
check_code_signing() {
    print_info "コード署名の状態を確認中..."

    # Check if user has valid certificates
    security find-identity -p codesigning -v | grep -E "iPhone Developer|Apple Development" || {
        print_warning "有効な開発証明書が見つかりません"
        echo "Xcodeで Preferences → Accounts → Manage Certificates を確認してください"
    }

    # Check provisioning profiles
    PROFILES_COUNT=$(ls ~/Library/MobileDevice/Provisioning\ Profiles/*.mobileprovision 2>/dev/null | wc -l || echo 0)
    print_info "プロビジョニングプロファイル数: $PROFILES_COUNT"
}

# Function to fix common issues
fix_common_issues() {
    print_info "一般的な問題を修正中..."

    # Clear derived data
    print_info "DerivedDataをクリア中..."
    rm -rf ~/Library/Developer/Xcode/DerivedData/*
    print_success "DerivedDataをクリアしました"

    # Kill simulators if running
    killall Simulator 2>/dev/null || true

    # Reset simulators
    print_info "シミュレータをリセット中..."
    xcrun simctl shutdown all 2>/dev/null || true

    print_success "修正完了"
}

# Function to show device logs
show_device_logs() {
    print_info "デバイスログを表示..."

    # Open Console app filtered for uPiper
    open -a Console
    print_info "Consoleアプリが開きました。'uPiper'でフィルタリングしてください"
}

# Function to install dependencies
install_dependencies() {
    print_info "依存関係をチェック中..."

    # Check if Homebrew is installed
    if ! command -v brew &> /dev/null; then
        print_error "Homebrewがインストールされていません"
        echo ""
        echo "セキュリティのため、Homebrewは手動でインストールしてください。"
        echo "公式インストール手順: https://brew.sh/"
        echo ""
        echo "インストール後、再度このスクリプトを実行してください。"
        exit 1
    fi

    # Check if ios-deploy is installed (useful for command-line deployment)
    if ! command -v ios-deploy &> /dev/null; then
        print_info "ios-deployをインストール中..."
        brew install ios-deploy
        print_success "ios-deployをインストールしました"
    fi

    print_success "依存関係のチェック完了"
}

# Function to show help
show_help() {
    cat << EOF
iOS Deployment Helper for uPiper

使用方法:
    $0 [コマンド]

コマンド:
    check       - 前提条件をチェック
    open        - Xcodeプロジェクトを開く
    clean       - ビルドをクリーン
    build       - デバイス用にビルド
    signing     - コード署名の状態を確認
    fix         - 一般的な問題を修正
    logs        - デバイスログを表示
    deps        - 依存関係をインストール
    help        - このヘルプを表示

例:
    $0 check    # 環境をチェック
    $0 open     # Xcodeでプロジェクトを開く
    $0 build    # デバイス用にビルド

トラブルシューティング:
    1. "信頼されていない開発者" エラー:
       設定 → 一般 → VPNとデバイス管理 → デベロッパAPP → 信頼

    2. プロビジョニングエラー:
       $0 signing でステータス確認
       $0 fix で一般的な問題を修正

    3. ビルドエラー:
       $0 clean でクリーンビルド
       $0 fix でDerivedDataをクリア

詳細なガイド:
    docs/ja/phase5-ios/ios-device-testing-guide.md を参照

EOF
}

# Main script logic
main() {
    echo -e "${GREEN}=== iOS Deployment Helper for uPiper ===${NC}"
    echo ""

    case "${1:-help}" in
        check)
            check_prerequisites
            ;;
        open)
            open_xcode_project
            ;;
        clean)
            clean_build
            ;;
        build)
            build_for_device
            ;;
        signing)
            check_code_signing
            ;;
        fix)
            fix_common_issues
            ;;
        logs)
            show_device_logs
            ;;
        deps)
            install_dependencies
            ;;
        help|--help|-h)
            show_help
            ;;
        *)
            print_error "不明なコマンド: $1"
            show_help
            exit 1
            ;;
    esac
}

# Run main function
main "$@"
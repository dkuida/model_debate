#!/usr/bin/env bash
# Verification test for task-1-directory-structure-and-config
# RED → GREEN: Run BEFORE creating files (should fail), run AGAIN after (should pass).

set -uo pipefail
PASS=0
FAIL=0
REPO=/home/dkuida/code/model_debate

check_dir() {
    local d="$1"
    if [ -d "$REPO/$d" ]; then
        echo "  PASS  dir  $d"
        ((PASS++))
    else
        echo "  FAIL  dir  $d  (missing)"
        ((FAIL++))
    fi
}

check_file() {
    local f="$1"
    if [ -f "$REPO/$f" ]; then
        echo "  PASS  file $f"
        ((PASS++))
    else
        echo "  FAIL  file $f  (missing)"
        ((FAIL++))
    fi
}

check_content() {
    local f="$1"
    local pattern="$2"
    local label="$3"
    if grep -q "$pattern" "$REPO/$f" 2>/dev/null; then
        echo "  PASS  content [$label] in $f"
        ((PASS++))
    else
        echo "  FAIL  content [$label] missing in $f"
        ((FAIL++))
    fi
}

echo "=== Directories ==="
check_dir "iFX/Utilities"
check_dir "Access/Chat/Interface"
check_dir "Access/Chat/Service.Claude"
check_dir "Access/Chat/Service.OpenAI"
check_dir "Manager/Debate/Interface"
check_dir "Manager/Debate/Service"
check_dir "Client/Runner"
check_dir "Test/Unit/Access/Chat"
check_dir "Test/Unit/Manager/Debate"
check_dir "Test/Integ"

echo ""
echo "=== Files ==="
check_file "global.json"
check_file "Directory.Build.props"
check_file "Directory.Packages.props"
check_file "Test/Unit/Directory.Packages.props"
check_file "Test/Integ/Directory.Packages.props"

echo ""
echo "=== Content: global.json ==="
check_content "global.json" '"version": "10.0.0"' "sdk version"
check_content "global.json" '"rollForward": "latestFeature"' "rollForward"

echo ""
echo "=== Content: Directory.Build.props ==="
check_content "Directory.Build.props" "net10.0" "TargetFramework"
check_content "Directory.Build.props" "<ImplicitUsings>disable</ImplicitUsings>" "ImplicitUsings"
check_content "Directory.Build.props" "<Nullable>enable</Nullable>" "Nullable"
check_content "Directory.Build.props" "LangVersion>12.0" "LangVersion"

echo ""
echo "=== Content: Directory.Packages.props ==="
check_content "Directory.Packages.props" "ManagePackageVersionsCentrally.*true" "central pkg mgmt"
check_content "Directory.Packages.props" 'Include="Anthropic"' "Anthropic pkg"
check_content "Directory.Packages.props" 'Version="12.17.0"' "Anthropic version"
check_content "Directory.Packages.props" 'Include="OpenAI"' "OpenAI pkg"
check_content "Directory.Packages.props" 'Include="Serilog"' "Serilog pkg"

echo ""
echo "=== Content: Test/Unit/Directory.Packages.props ==="
check_content "Test/Unit/Directory.Packages.props" 'Import Project=' "imports root props"
check_content "Test/Unit/Directory.Packages.props" 'Include="NUnit"' "NUnit pkg"
check_content "Test/Unit/Directory.Packages.props" 'Include="Microsoft.NET.Test.Sdk"' "TestSdk"
check_content "Test/Unit/Directory.Packages.props" 'Include="NUnit3TestAdapter"' "TestAdapter"

echo ""
echo "=== Content: Test/Integ/Directory.Packages.props ==="
check_content "Test/Integ/Directory.Packages.props" 'Import Project=' "imports root props"
check_content "Test/Integ/Directory.Packages.props" 'Include="NUnit"' "NUnit pkg"

echo ""
echo "=============================="
echo "Results: $PASS passed, $FAIL failed"
if [ "$FAIL" -gt 0 ]; then
    echo "STATUS: FAIL"
    exit 1
else
    echo "STATUS: PASS"
    exit 0
fi

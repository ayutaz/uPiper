@echo off
echo Cleaning Unity compilation cache...
echo.

REM Close Unity Editor before running this script
echo Please ensure Unity Editor is closed before proceeding.
pause

REM Delete Script Assemblies
if exist "Library\ScriptAssemblies" (
    echo Deleting Library\ScriptAssemblies...
    rmdir /s /q "Library\ScriptAssemblies"
)

REM Delete Bee artifacts
if exist "Library\Bee" (
    echo Deleting Library\Bee...
    rmdir /s /q "Library\Bee"
)

REM Delete obj folder
if exist "obj" (
    echo Deleting obj folder...
    rmdir /s /q "obj"
)

echo.
echo Cleanup completed. Please restart Unity Editor.
pause
@echo off
rem Builds the single-file FindTheNeedleEditor.exe next to this script.
rem Two steps on purpose: "publish" must not recompile (a direct publish produced a broken assembly here).
cd /d "%~dp0src"
dotnet build -c Release || exit /b 1
dotnet publish -c Release --no-build -o bin\single || exit /b 1
copy /y bin\single\FindTheNeedleEditor.exe "%~dp0FindTheNeedleEditor.exe"

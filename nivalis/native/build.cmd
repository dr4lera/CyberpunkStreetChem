@echo off
setlocal
set "ROOT=%~dp0"
if not defined NC_RED4EXT_SDK_DIR exit /b 2
if not defined NC_JSON_INCLUDE_DIR exit /b 2
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "NC_VCVARS=%%i\VC\Auxiliary\Build\vcvars64.bat"
if not defined NC_VCVARS exit /b 3
call "%NC_VCVARS%" >nul || exit /b 1
if not exist "%ROOT%build" mkdir "%ROOT%build"
cl /nologo /std:c++20 /EHsc /O2 /MT /W4 /LD /I "%NC_RED4EXT_SDK_DIR%\include" /I "%NC_RED4EXT_SDK_DIR%\vendor" /I "%NC_JSON_INCLUDE_DIR%" "%ROOT%host_bridge.cpp" /Fo"%ROOT%build\host_bridge.obj" /Fe"%ROOT%build\NivalisNightCityHost.dll" /link winhttp.lib ole32.lib user32.lib || exit /b 1

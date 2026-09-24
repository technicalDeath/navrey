@echo off
setlocal EnableExtensions
set "NAVREY_ROOT=%~dp0"
set "DOTNET_ROOT=%NAVREY_ROOT%..\dotnet"

if not exist "%DOTNET_ROOT%\dotnet.exe" if exist "%USERPROFILE%\.dotnet\dotnet.exe" set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
if not exist "%DOTNET_ROOT%\dotnet.exe" (
    for /f "delims=" %%D in ('where dotnet 2^>nul') do if not defined DOTNET_EXE set "DOTNET_EXE=%%D"
) else (
    set "DOTNET_EXE=%DOTNET_ROOT%\dotnet.exe"
)

if not defined DOTNET_EXE (
    echo .NET 10 was not found. Install it or set DOTNET_ROOT to its folder.
    exit /b 1
)

cd /d "%NAVREY_ROOT%"
"%DOTNET_EXE%" "%NAVREY_ROOT%bin\Debug\net10.0\cuo.dll" -agent %*
endlocal

@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "APP_EXE=%SCRIPT_DIR%dist\TextLayer\TextLayer.exe"

if not exist "%APP_EXE%" (
    echo TextLayer is not published yet.
    echo Run publish.bat first to create the canonical app build in .\dist\TextLayer\
    exit /b 1
)

start "" "%APP_EXE%"

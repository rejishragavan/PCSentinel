@echo off
title PC Sentinel — Autonomous Hardware Health & Diagnostics Lab
color 0B
echo ================================================================================
echo                   PC SENTINEL — AUTONOMOUS DESKTOP GUI LAUNCHER                 
echo ================================================================================
echo.
echo Launching PC Sentinel WPF Desktop UI...
echo (If prompted by Windows UAC, grant administrator rights for Ring-0 sensor access)
echo.
dotnet run --project src\PCSentinel.App
pause

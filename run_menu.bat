@echo off
cd /d "%~dp0"
title NCS menu
echo Launching the menu - make sure Nightclub Simulator is already running on the headset.
frida -U -l frida-il2cpp-bridge.js -l fridaimgui_ncs.ts Gadget
pause

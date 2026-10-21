@echo off
setlocal EnableDelayedExpansion
title Frida Gadget injector

REM ============================================================================
REM  ncs_frida_gadget.bat
REM  Nightclub Simulator ONLY - this injector refuses to run on any other apk.
REM  Drop it next to the NightclubSimulater .apk and run it. It will:
REM    1. find the .apk in this folder and verify it IS Nightclub Simulator
REM    2. download apktool, uber-apk-signer and the latest Frida Gadget (arm64)
REM    3. unpack the apk, drop libgadget.so into lib\arm64-v8a
REM    4. patch libmain.so to load libgadget.so (DT_NEEDED, via LIEF)
REM    5. write a gadget config that auto-runs your script
REM    6. repack and sign -> <name>-patched.apk
REM    7. (OBB method) on install, also push the game's .obb to
REM       /sdcard/Android/obb/<package>/ so NCS loads its assets
REM
REM  Keep the game's main.*.obb in this same folder.
REM  Needs on PATH: java, python, curl (Win10 1803+ has curl). LIEF is pip-installed.
REM  No root needed - this is Frida Gadget, for your own single-player modding.
REM ============================================================================

REM ---- settings -------------------------------------------------------------
REM  Only an apk whose manifest contains this token is accepted (keeps it NCS-only).
set "ONLY_TOKEN=nightclub"
set "ARCH=arm64-v8a"
set "FRIDA_ABI=arm64"
set "GADGET=libgadget.so"
REM  listen = attach from your PC with  frida -U -l ... -l ... Gadget  (no script on device)
REM  script = gadget auto-runs SCRIPT_DEV_PATH on the device (push agent.js there)
set "GADGET_MODE=listen"
set "SCRIPT_DEV_PATH=/data/local/tmp/fridaimgui_ncs.js"
set "BRIDGE_JS=frida-il2cpp-bridge.js"
set "MENU_TS=fridaimgui_ncs.ts"
set "MENU_JS=fridaimgui_ncs.js"
set "PKG=com.mandegames.NightClubSimulater"
set "WORK=_gadget_work"
set "TOOLS=_gadget_tools"
set "PS=powershell -NoProfile -ExecutionPolicy Bypass -Command"
REM libs to try patching, in order (first one present wins)
set "LIB_CANDIDATES=libmain.so libil2cpp.so libunity.so"

echo.
echo  ===== Frida Gadget APK injector =====
echo.

REM ---- find the apk ---------------------------------------------------------
set "APK="
for %%F in (*.apk) do (
  echo %%~nF | findstr /i /c:"patched" /c:"signed" /c:"aligned" >nul || (
    if not defined APK set "APK=%%F"
  )
)
if not defined APK (
  echo [X] No source .apk found here. Put the game apk in this folder and re-run.
  goto :fail
)
echo [*] APK:  !APK!
for %%A in ("!APK!") do set "BASE=%%~nA"

REM ---- NCS-only guard: the apk's manifest must mention Nightclub Simulator ---
where python >nul 2>&1 || ( echo [X] Python not found on PATH. Install Python 3. & goto :fail )
python -c "import sys,zipfile;d=zipfile.ZipFile(sys.argv[1]).read('AndroidManifest.xml');t=d.decode('utf-16-le','ignore').lower();sys.exit(0 if (sys.argv[2] in t) else 2)" "!APK!" "%ONLY_TOKEN%"
if errorlevel 1 (
  echo [X] This apk is not Nightclub Simulator ^(manifest has no "%ONLY_TOKEN%"^).
  echo     This injector is NCS-only and will not patch other games.
  goto :fail
)
echo [*] Verified: Nightclub Simulator.

REM ---- find the OBB and take the package name from its filename --------------
REM  OBB name is  main.<versioncode>.<package>.obb  -> the package is pushed to
REM  /sdcard/Android/obb/<package>/ so the game finds its 1.9 GB of assets.
set "OBB="
for %%O in (*.obb) do if not defined OBB set "OBB=%%~nxO"
if not defined OBB (
  echo [!] WARNING: no .obb in this folder. NCS will NOT load without it.
  echo     Put  main.214.%PKG%.obb  here so it gets pushed to the headset too.
) else (
  echo [*] OBB:  !OBB!
  echo !OBB! | findstr /i "nightclub" >nul || echo [!] That OBB name doesn't look like Nightclub Simulator.
  for /f "tokens=1,2,* delims=." %%a in ("!OBB!") do set "OBBPKG=%%c"
  if defined OBBPKG set "PKG=!OBBPKG:.obb=!"
)
echo [*] Package: !PKG!

REM ---- prerequisites --------------------------------------------------------
where java   >nul 2>&1 || ( echo [X] Java not found on PATH. Install a JDK/JRE.        & goto :fail )
where python >nul 2>&1 || ( echo [X] Python not found on PATH. Install Python 3.        & goto :fail )
where curl   >nul 2>&1 || ( echo [X] curl not found - needs Windows 10 1803 or newer.   & goto :fail )

python -c "import lief" 1>nul 2>nul
if errorlevel 1 (
  echo [*] Installing LIEF, one time...
  python -m pip install --quiet --upgrade lief || ( echo [X] pip install lief failed. & goto :fail )
)

if not exist "%TOOLS%" mkdir "%TOOLS%"

REM ---- download apktool ------------------------------------------------------
set "APKTOOL=%TOOLS%\apktool.jar"
if not exist "%APKTOOL%" (
  echo [*] Downloading apktool...
  %PS% "[Net.ServicePointManager]::SecurityProtocol='Tls12'; $a=(Invoke-RestMethod 'https://api.github.com/repos/iBotPeaches/Apktool/releases/latest').assets | Where-Object { $_.name -like 'apktool_*.jar' } | Select-Object -First 1; if(-not $a){ throw 'apktool asset not found' }; Invoke-WebRequest $a.browser_download_url -OutFile '%APKTOOL%'"
  if errorlevel 1 ( echo [X] apktool download failed. & goto :fail )
)

REM ---- download uber-apk-signer ---------------------------------------------
set "SIGNER=%TOOLS%\uber-apk-signer.jar"
if not exist "%SIGNER%" (
  echo [*] Downloading uber-apk-signer...
  %PS% "[Net.ServicePointManager]::SecurityProtocol='Tls12'; $a=(Invoke-RestMethod 'https://api.github.com/repos/patrickfav/uber-apk-signer/releases/latest').assets | Where-Object { $_.name -like 'uber-apk-signer-*.jar' } | Select-Object -First 1; if(-not $a){ throw 'signer asset not found' }; Invoke-WebRequest $a.browser_download_url -OutFile '%SIGNER%'"
  if errorlevel 1 ( echo [X] uber-apk-signer download failed. & goto :fail )
)

REM ---- download + decompress Frida Gadget -----------------------------------
set "GADGET_XZ=%TOOLS%\%GADGET%.xz"
set "GADGET_SO=%TOOLS%\%GADGET%"
if not exist "%GADGET_SO%" (
  echo [*] Downloading latest Frida Gadget - android-%FRIDA_ABI%...
  %PS% "[Net.ServicePointManager]::SecurityProtocol='Tls12'; $a=(Invoke-RestMethod 'https://api.github.com/repos/frida/frida/releases/latest').assets | Where-Object { $_.name -like '*-android-%FRIDA_ABI%.so.xz' } | Select-Object -First 1; if(-not $a){ throw 'gadget asset not found' }; Write-Host ('    ' + $a.name); Invoke-WebRequest $a.browser_download_url -OutFile '%GADGET_XZ%'"
  if errorlevel 1 ( echo [X] gadget download failed. & goto :fail )
  echo [*] Decompressing gadget...
  python -c "import lzma;open(r'%GADGET_SO%','wb').write(lzma.open(r'%GADGET_XZ%').read())" || ( echo [X] gadget decompress failed. & goto :fail )
)

REM ---- unpack the apk (keep resources + dex untouched: -s -r) ----------------
echo [*] Unpacking apk...
if exist "%WORK%" rmdir /s /q "%WORK%"
java -jar "%APKTOOL%" d -s -r -f "!APK!" -o "%WORK%" || ( echo [X] apktool decode failed. & goto :fail )

set "LIBDIR=%WORK%\lib\%ARCH%"
if not exist "%LIBDIR%" (
  echo [X] This apk has no lib\%ARCH% folder - it may not be a 64-bit build.
  goto :fail
)

REM ---- choose the library to patch ------------------------------------------
set "TARGET="
for %%L in (%LIB_CANDIDATES%) do (
  if not defined TARGET if exist "%LIBDIR%\%%L" set "TARGET=%%L"
)
if not defined TARGET (
  echo [X] No target lib in lib\%ARCH% - looked for %LIB_CANDIDATES%.
  goto :fail
)
echo [*] Will patch: %TARGET%

REM ---- drop the gadget + its config next to the libs ------------------------
copy /y "%GADGET_SO%" "%LIBDIR%\%GADGET%" >nul || ( echo [X] couldn't copy gadget. & goto :fail )
python -c "import json,sys;m=sys.argv[1];cfg={'interaction':{'type':'listen'}} if m=='listen' else {'interaction':{'type':'script','path':sys.argv[2],'on_change':'reload'}};open(r'%LIBDIR%\libgadget.config.so','w').write(json.dumps(cfg))" "%GADGET_MODE%" "%SCRIPT_DEV_PATH%" || ( echo [X] couldn't write gadget config. & goto :fail )
echo [*] Gadget + config placed in lib\%ARCH% ^(mode: %GADGET_MODE%^).

REM ---- patch the target lib to load the gadget (adds a DT_NEEDED entry) ------
echo [*] Patching %TARGET% to load %GADGET%...
python -c "import lief;p=r'%LIBDIR%\%TARGET%';b=lief.parse(p);('%GADGET%' in list(b.libraries)) or b.add_library('%GADGET%');b.write(p)" || ( echo [X] LIEF patch failed. & goto :fail )

REM ---- optional: compile the menu TS, then build the combined agent.js ------
if not exist "%MENU_JS%" if exist "%MENU_TS%" (
  where tsc >nul 2>&1 && (
    echo [*] Compiling %MENU_TS% -^> %MENU_JS% ^(type warnings are harmless^)...
    tsc "%MENU_TS%" --target es2020 --lib es2020 1>nul 2>nul
  )
)
if exist "%BRIDGE_JS%" if exist "%MENU_JS%" (
  echo [*] Building agent.js from %BRIDGE_JS% + %MENU_JS%...
  copy /b /y "%BRIDGE_JS%" + "%MENU_JS%" "agent.js" >nul
)

REM ---- repack ---------------------------------------------------------------
echo [*] Repacking...
set "UNSIGNED=%BASE%-unsigned.apk"
if exist "%UNSIGNED%" del /q "%UNSIGNED%"
java -jar "%APKTOOL%" b "%WORK%" -o "%UNSIGNED%" || ( echo [X] apktool build failed. & goto :fail )

REM ---- align + sign (uber-apk-signer zipaligns too) -------------------------
echo [*] Signing...
java -jar "%SIGNER%" -a "%UNSIGNED%" --allowResign --overwrite || ( echo [X] signing failed. & goto :fail )
set "OUT=%BASE%-patched.apk"
if exist "%OUT%" del /q "%OUT%"
ren "%UNSIGNED%" "%OUT%" 2>nul

set "OBBDIR=/sdcard/Android/obb/!PKG!"
set "LISTEN="
if /i "%GADGET_MODE%"=="listen" set "LISTEN=1"
echo.
echo  ===== DONE =====
echo   Patched apk : %OUT%
if defined OBB       echo   OBB         : !OBB!
if not defined LISTEN if exist "agent.js" echo   Agent script: agent.js
echo.
echo  Install on the headset:
echo    adb uninstall !PKG!
echo    adb install "%OUT%"
if defined OBB (
  echo    adb shell mkdir -p !OBBDIR!
  echo    adb push "!OBB!" !OBBDIR!/!OBB!
)
if defined LISTEN (
  echo  Then launch the game and attach from this PC:
  echo    frida -U -l %BRIDGE_JS% -l %MENU_TS% Gadget
  echo  ^(re-run that command to reload the menu after editing it^)
) else (
  echo    adb push agent.js %SCRIPT_DEV_PATH%
  echo  Launch the game - the gadget auto-runs the script; re-push it to reload.
)
echo.

REM ---- offer to do the device install if adb is attached --------------------
where adb >nul 2>&1 || goto :noadb
echo.
choice /c YN /m "Install apk + push OBB to the headset now"
if errorlevel 2 goto :noadb
echo [*] Uninstalling old build...
adb uninstall !PKG!
echo [*] Installing patched apk...
adb install "%OUT%"
if defined OBB adb shell mkdir -p "!OBBDIR!"
if defined OBB adb push "!OBB!" "!OBBDIR!/!OBB!"
if not defined LISTEN if exist "agent.js" adb push agent.js %SCRIPT_DEV_PATH%
echo [*] Done - launch Nightclub Simulator on the headset.
if defined LISTEN echo [*] Then attach:  frida -U -l %BRIDGE_JS% -l %MENU_TS% Gadget
:noadb

echo.
pause
exit /b 0

:fail
echo.
echo  ===== FAILED - see the message above. =====
echo.
pause
exit /b 1

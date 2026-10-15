@echo off
rem ===========================================================================
rem  Unknown's Suite - release build
rem
rem    BUILD.bat                          asks for the Gorilla Tag folder
rem    BUILD.bat "<Gorilla Tag folder>"   no questions
rem    BUILD.bat Release "<folder>"       same, with the configuration
rem
rem  Builds the client, the bootstrap and the launcher (with both embedded, and
rem  SharpMonoInjector compiled in), then the BepInEx add-ons, into output\Customer.
rem  The full output of every step goes to build.log.
rem  UNKNOWN_BUILD_NO_PAUSE=1: no questions (uses the folder it finds) and no
rem  pause at the end.
rem
rem  The build runs in a child cmd.exe: if something makes cmd.exe quit (a ")"
rem  in a path used to close an IF block), this window still stays open.
rem ===========================================================================
if /I "%~1"=="--inner" goto :inner

setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
title Unknown's Suite - Build
cmd /d /c call "%~f0" --inner %*
set "RC=%ERRORLEVEL%"
if "%RC%"=="0" goto :outer_pause
echo.
echo Build FAILED with error %RC%. The full log is in build.log.
:outer_pause
if /I "%UNKNOWN_BUILD_NO_PAUSE%"=="1" exit /b %RC%
echo.
echo Press any key to close this window...
pause >nul
exit /b %RC%

:inner
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"
call :main "%~2" "%~3"
exit /b %ERRORLEVEL%

rem ===========================================================================
:main
set "CONFIG=Release"
set "GT_ARG="
if /I "%~1"=="Release" goto :args_config
if /I "%~1"=="Debug" goto :args_config
set "GT_ARG=%~1"
goto :args_done
:args_config
set "CONFIG=%~1"
set "GT_ARG=%~2"
:args_done

set "ROOT=%CD%"
set "LOG=%ROOT%\build.log"
set "STAGE=%ROOT%\.build"
set "CAMERA_DIR=%STAGE%\camera"
set "BOOTSTRAP_DIR=%STAGE%\bootstrap"
set "LAUNCHER_DIR=%STAGE%\launcher"
set "ADDON_DIR=%STAGE%\addons"
set "OUTPUT_DIR=%ROOT%\output\Customer"
set "PLUGINS=%OUTPUT_DIR%\BepInEx Add-ons\BepInEx\plugins"
set "CAMERA_DLL=%CAMERA_DIR%\Unknowns Client.dll"
set "HARMONY_DLL=%CAMERA_DIR%\0Harmony.dll"
set "BOOTSTRAP_DLL=%BOOTSTRAP_DIR%\UnknownsCameraBootstrap.dll"
set "LAUNCHER_EXE=%LAUNCHER_DIR%\Unknown's Suite.exe"
rem the Gorilla Tag folder from last time (outside the project folder)
set "GT_SAVE=%LOCALAPPDATA%\UnknownsSuite\build_gorillatag_path.txt"

>"%LOG%" echo Unknown's Suite build log
>>"%LOG%" echo Started: %DATE% %TIME%
>>"%LOG%" echo Root: %ROOT%
>>"%LOG%" echo Configuration: %CONFIG%

echo ============================================================
echo   UNKNOWN'S SUITE  -  %CONFIG% BUILD
echo ============================================================

rem ---- .NET SDK ---------------------------------------------------------------
where dotnet >nul 2>&1
if errorlevel 1 goto :no_dotnet
set "SDK="
for /f "delims=" %%V in ('dotnet --version 2^>nul') do set "SDK=%%V"
if not defined SDK goto :no_dotnet
>>"%LOG%" echo.
dotnet --info >>"%LOG%" 2>&1
echo .NET SDK     %SDK%

rem ---- Gorilla Tag folder -----------------------------------------------------
call :find_gorilla_tag
if errorlevel 1 exit /b 20
echo Gorilla Tag  %GT%
>>"%LOG%" echo Gorilla Tag: %GT%
if not exist "%LOCALAPPDATA%\UnknownsSuite" mkdir "%LOCALAPPDATA%\UnknownsSuite" >nul 2>&1
>"%GT_SAVE%" echo(%GT%
echo.

rem ---- clean staging ----------------------------------------------------------
if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%ROOT%\output" rmdir /s /q "%ROOT%\output"
for %%D in ("%CAMERA_DIR%" "%BOOTSTRAP_DIR%" "%LAUNCHER_DIR%" "%OUTPUT_DIR%") do mkdir "%%~D" >nul 2>&1

rem ---- build steps ------------------------------------------------------------
call :step "1/5" "Utility catalog ........"
dotnet build "UnknownsUtilitySuite\UnknownsUtilitySuite.csproj" -c "%CONFIG%" -o "%STAGE%\utilitysuite" >>"%LOG%" 2>&1
call :step_done || exit /b 31

call :step "2/5" "Client ................."
dotnet build "UnknownsClient.csproj" -c "%CONFIG%" "-p:GorillaTagPath=%GT%" "-p:CameraEdition=Plus" -o "%CAMERA_DIR%" >>"%LOG%" 2>&1
call :step_done "%CAMERA_DLL%" || exit /b 32
rem Harmony normally lands next to the client; fall back to the NuGet cache
if not exist "%HARMONY_DLL%" for /d %%V in ("%USERPROFILE%\.nuget\packages\lib.harmony\*") do if exist "%%V\lib\net48\0Harmony.dll" if not exist "%HARMONY_DLL%" copy /y "%%V\lib\net48\0Harmony.dll" "%HARMONY_DLL%" >nul
if not exist "%HARMONY_DLL%" goto :no_harmony

call :step "3/5" "Bootstrap .............."
dotnet build "UnknownsCameraBootstrap\UnknownsCameraBootstrap.csproj" -c "%CONFIG%" "-p:GorillaTagPath=%GT%" -o "%BOOTSTRAP_DIR%" >>"%LOG%" 2>&1
call :step_done "%BOOTSTRAP_DLL%" || exit /b 35

rem RequireEmbeddedPayloads makes the launcher build fail if any of the three is missing
call :step "4/5" "Launcher ..............."
dotnet build "UnknownsCameraLauncher\UnknownsCameraLauncher.csproj" -c "%CONFIG%" "-p:BootstrapDll=%BOOTSTRAP_DLL%" "-p:HarmonyDll=%HARMONY_DLL%" "-p:CameraDll=%CAMERA_DLL%" "-p:RequireEmbeddedPayloads=true" -o "%LAUNCHER_DIR%" >>"%LOG%" 2>&1
call :step_done "%LAUNCHER_EXE%" || exit /b 39

copy /b /y "%LAUNCHER_EXE%" "%OUTPUT_DIR%\Unknown's Suite.exe" >nul
if errorlevel 1 goto :copy_failed
if exist "%LAUNCHER_EXE%.config" copy /y "%LAUNCHER_EXE%.config" "%OUTPUT_DIR%\Unknown's Suite.exe.config" >nul
>"%OUTPUT_DIR%\README.txt" echo Unknown's Suite
>>"%OUTPUT_DIR%\README.txt" echo No license key or key server is needed.
>>"%OUTPUT_DIR%\README.txt" echo The client, bootstrap, Harmony and injector are all inside Unknown's Suite.exe.

call :step "5/5" "BepInEx add-ons ........"
if not exist "%GT%\BepInEx\core\BepInEx.dll" goto :addons_skipped
call :build_addons
if errorlevel 1 goto :addons_failed
echo OK
>>"%OUTPUT_DIR%\README.txt" echo BepInEx add-ons: copy the BepInEx folder from "BepInEx Add-ons" into your Gorilla Tag folder.
goto :build_done
:addons_skipped
echo SKIPPED, BepInEx 5 is not installed in Gorilla Tag
>>"%LOG%" echo [WARN] BepInEx 5 not found in the Gorilla Tag folder, add-ons skipped.

:build_done
>>"%LOG%" echo.
>>"%LOG%" echo SUCCESS: %OUTPUT_DIR%\Unknown's Suite.exe
echo.
echo ============================================================
echo   DONE   output\Customer\Unknown's Suite.exe
if exist "%PLUGINS%" echo          output\Customer\BepInEx Add-ons\
echo ============================================================
exit /b 0

rem ---- failures ---------------------------------------------------------------
:no_dotnet
echo [ERROR] No .NET SDK found (dotnet.exe). Install the .NET 8 SDK or newer,
echo         then open a new window and run BUILD.bat again.
>>"%LOG%" echo [ERROR] dotnet.exe / .NET SDK not found.
exit /b 10

:no_harmony
echo [ERROR] 0Harmony.dll is missing after the client build. NuGet restore may have failed.
>>"%LOG%" echo [ERROR] Missing %HARMONY_DLL%
exit /b 34

:copy_failed
echo [ERROR] Could not copy the launcher into output\Customer. Is Unknown's Suite still running?
>>"%LOG%" echo [ERROR] Final EXE copy failed.
exit /b 42

:addons_failed
echo FAILED
call :show_log_tail
exit /b 43

rem ===========================================================================
rem  Gorilla Tag folder: a folder given on the command line is used as it is.
rem  Otherwise it asks, offering the folder used last time or the one it finds
rem  through Steam (Enter keeps it). Sets GT.
rem ===========================================================================
:find_gorilla_tag
set "GT="
if not defined GT_ARG goto :fgt_guess
call :check_gt "%GT_ARG%"
if defined GT exit /b 0
echo [ERROR] Gorilla Tag was not found in: %GT_ARG%
echo         The folder needs Gorilla Tag_Data\Managed\Assembly-CSharp.dll.
>>"%LOG%" echo [ERROR] Not a Gorilla Tag folder: %GT_ARG%
exit /b 1

:fgt_guess
call :guess_gt
set "GT_DEFAULT=%GT%"
set "GT="
if /I not "%UNKNOWN_BUILD_NO_PAUSE%"=="1" goto :fgt_ask
if not defined GT_DEFAULT goto :fgt_none
set "GT=%GT_DEFAULT%"
exit /b 0

:fgt_ask
echo.
if defined GT_DEFAULT echo Found Gorilla Tag in: %GT_DEFAULT%
if defined GT_DEFAULT echo Press Enter to use it, or paste a different folder.
if not defined GT_DEFAULT echo Paste your Gorilla Tag folder. In Steam: Gorilla Tag, gear icon, Manage, Browse local files.
set "GT_IN="
set /p "GT_IN=Gorilla Tag folder: "
if not defined GT_IN set "GT_IN=%GT_DEFAULT%"
if not defined GT_IN goto :fgt_none
set "GT_IN=%GT_IN:"=%"
call :check_gt "%GT_IN%"
echo.
if defined GT exit /b 0
echo That is not a Gorilla Tag folder (it needs Gorilla Tag_Data\Managed\Assembly-CSharp.dll).
goto :fgt_ask

:fgt_none
echo [ERROR] No Gorilla Tag folder was given.
>>"%LOG%" echo [ERROR] No Gorilla Tag folder.
exit /b 1

rem Sets GT when %1 is a Gorilla Tag folder. Also takes Gorilla Tag.exe, the
rem Gorilla Tag_Data or Managed folder, or Assembly-CSharp.dll.
:check_gt
set "GT="
set "P=%~1"
if not defined P exit /b 1
for %%I in ("%P%") do if /I "%%~xI"==".exe" set "P=%%~dpI"
for %%I in ("%P%") do if /I "%%~xI"==".dll" set "P=%%~dpI"
rem no trailing backslash: it would escape the closing quote on the MSBuild line
if "%P:~-1%"=="\" set "P=%P:~0,-1%"
for %%I in ("%P%") do set "P=%%~fI"
for %%I in ("%P%\..") do set "P1=%%~fI"
for %%I in ("%P%\..\..") do set "P2=%%~fI"
if not exist "%P%\Gorilla Tag_Data\Managed\Assembly-CSharp.dll" if exist "%P1%\Gorilla Tag_Data\Managed\Assembly-CSharp.dll" set "P=%P1%"
if not exist "%P%\Gorilla Tag_Data\Managed\Assembly-CSharp.dll" if exist "%P2%\Gorilla Tag_Data\Managed\Assembly-CSharp.dll" set "P=%P2%"
if not exist "%P%\Gorilla Tag_Data\Managed\Assembly-CSharp.dll" exit /b 1
if not exist "%P%\Gorilla Tag_Data\Managed\UnityEngine.CoreModule.dll" exit /b 1
set "GT=%P%"
exit /b 0

rem Sets GT to the folder from last time, else Steam's own library folders,
rem else the usual Steam folders.
:guess_gt
set "GT="
set "GT_SAVED="
if exist "%GT_SAVE%" set /p "GT_SAVED=" <"%GT_SAVE%"
if defined GT_SAVED call :check_gt "%GT_SAVED%"
if defined GT exit /b 0
set "STEAM="
for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul ^| find /i "SteamPath"') do set "STEAM=%%B"
if defined STEAM for %%I in ("%STEAM%") do set "STEAM=%%~fI"
if defined STEAM call :check_library "%STEAM%"
if defined GT exit /b 0
if defined STEAM if exist "%STEAM%\steamapps\libraryfolders.vdf" for /f "usebackq tokens=1,*" %%A in ("%STEAM%\steamapps\libraryfolders.vdf") do if /I %%A=="path" if not defined GT call :check_library %%B
if defined GT exit /b 0
for %%L in ("C:\Program Files (x86)\Steam" "C:\Program Files\Steam") do if not defined GT call :check_library "%%~L"
for %%D in (C D E F) do for %%L in ("%%D:\SteamLibrary" "%%D:\Steam" "%%D:\Games\Steam" "%%D:\Games\SteamLibrary" "%%D:\Steam Games") do if not defined GT call :check_library "%%~L"
exit /b 0

:check_library
call :check_gt "%~1\steamapps\common\Gorilla Tag"
exit /b 0

rem ===========================================================================
rem  BepInEx add-ons, into output\Customer\BepInEx Add-ons\BepInEx\plugins:
rem    UnknownsClient\             the core: UnknownsClientCore.dll + Unknowns Client.core
rem    UnknownsClient-<Add-on>\    one folder per add-on
rem    UnknownsUpdateChanger\      the Update Changer (also works on its own)
rem ===========================================================================
:build_addons
set "ADDON_PROJ=Addons\UnknownsClientAddon\UnknownsClientAddon.csproj"
set "KEYS=%ADDON_DIR%\keys.txt"
mkdir "%ADDON_DIR%" >nul 2>&1
mkdir "%PLUGINS%\UnknownsClient" >nul 2>&1
>>"%LOG%" echo.
>>"%LOG%" echo ===== add-on core =====
dotnet build "Addons\UnknownsClientCore\UnknownsClientCore.csproj" -c Release "-p:GorillaTagPath=%GT%" -o "%ADDON_DIR%\core" >>"%LOG%" 2>&1 || exit /b 1
copy /y "%ADDON_DIR%\core\UnknownsClientCore.dll" "%PLUGINS%\UnknownsClient\" >nul || exit /b 1
rem not a .dll, so BepInEx does not load it; the core loads it itself
copy /y "%CAMERA_DLL%" "%PLUGINS%\UnknownsClient\Unknowns Client.core" >nul || exit /b 1

dotnet restore "%ADDON_PROJ%" "-p:GorillaTagPath=%GT%" "-p:AddonAssembly=UnknownsAddon" >>"%LOG%" 2>&1 || exit /b 1
rem id;name;open key;folder - same order and keys as CorePlugin.Addons
for %%A in (
  "casting-mod;Casting Mod;Q;CastingMod"
  "utility-tablet;Utility Tablet;E;UtilityTablet"
  "ref-tablet;Ref Tablet;R;RefTablet"
  "auto-ref;Auto Ref;Y;AutoRef"
  "time-changer;Time Changer;U;TimeChanger"
  "self-rig-lerping;Self Rig Lerping;I;SelfRigLerping"
  "environment-colors;Environment Colors;O;EnvironmentColors"
  "name-tags;Name Tags;P;NameTags"
  "tts;TTS;G;Tts"
  "wasd;WASD;H;Wasd"
  "room;Room Joiner;J;RoomJoiner"
) do for /f "tokens=1-4 delims=;" %%a in ("%%~A") do call :build_addon "%%a" "%%b" "%%c" "%%d" || exit /b 1

>>"%LOG%" echo.
>>"%LOG%" echo ===== Update Changer =====
mkdir "%PLUGINS%\UnknownsUpdateChanger\updates" >nul 2>&1
dotnet build "Addons\UnknownsUpdateChanger\UnknownsUpdateChanger.csproj" -c Release "-p:GorillaTagPath=%GT%" -o "%ADDON_DIR%\updatechanger" >>"%LOG%" 2>&1 || exit /b 1
copy /y "%ADDON_DIR%\updatechanger\UnknownsUpdateChanger.dll" "%PLUGINS%\UnknownsUpdateChanger\" >nul || exit /b 1

call :write_addons_readme
exit /b 0

:build_addon
>>"%LOG%" echo.
>>"%LOG%" echo ===== add-on %~2 =====
set "ASM=Unknowns%~4"
dotnet build "%ADDON_PROJ%" -c Release --no-restore "-p:GorillaTagPath=%GT%" "-p:AddonId=%~1" "-p:AddonName=%~2" "-p:AddonKey=%~3" "-p:AddonAssembly=%ASM%" "-p:IntermediateOutputPath=obj\%~1\\" -o "%ADDON_DIR%\%~1" >>"%LOG%" 2>&1 || exit /b 1
mkdir "%PLUGINS%\UnknownsClient-%~4" >nul 2>&1
copy /y "%ADDON_DIR%\%~1\%ASM%.dll" "%PLUGINS%\UnknownsClient-%~4\" >nul || exit /b 1
rem a line for the README's key list, the name padded to 20
set "NAME=%~2                    "
set "NAME=%NAME:~0,20%"
>>"%KEYS%" echo   %NAME% %~3   (UnknownsClient-%~4)
exit /b 0

:write_addons_readme
set "R=%PLUGINS%\README.txt"
>"%R%" echo UNKNOWN'S CLIENT - BEPINEX ADD-ONS
>>"%R%" echo ==================================
>>"%R%" echo(
>>"%R%" echo Needs: BepInEx 5 in Gorilla Tag.
>>"%R%" echo(
>>"%R%" echo Install:
>>"%R%" echo   Copy the BepInEx folder from here into your Gorilla Tag folder. Or pick:
>>"%R%" echo   always copy BepInEx\plugins\UnknownsClient (the core), then only the
>>"%R%" echo   UnknownsClient-^<Add-on^> folders you want. An add-on is on when its folder is
>>"%R%" echo   installed. The Utility and Ref tablets need Casting Mod.
>>"%R%" echo(
>>"%R%" echo Keys (in game, on the desktop):
type "%KEYS%" >>"%R%"
>>"%R%" echo   Update Changer       K   (UnknownsUpdateChanger)
>>"%R%" echo   Installed add-ons    L   (the list)
>>"%R%" echo(
>>"%R%" echo Casting Mod's manager also still opens with its own key (Tab by default).
>>"%R%" echo With Casting Mod installed, K opens its Updates tab and the Update Changer
>>"%R%" echo plugin steps aside; without it, the Update Changer runs on its own window.
>>"%R%" echo The client's own keys that clash with an installed add-on's key (scoreboard
>>"%R%" echo U/I/O/P, Room J, Player Manager K) give way to the add-on.
>>"%R%" echo(
>>"%R%" echo Update Changer:
>>"%R%" echo   Season packs download from GitHub (Unknowncastingdev/Old-gtag-updates) the
>>"%R%" echo   first time you turn an update on, into Gorilla Tag\Unknowns Client\updates.
>>"%R%" echo   Only you see it and it's only looks: you still collide with today's map.
exit /b 0

rem ===========================================================================
rem  helpers
rem ===========================================================================
:step
>>"%LOG%" echo.
>>"%LOG%" echo ===== [%~1] %~2 =====
<nul set /p "=[%~1] %~2 "
exit /b 0

rem Prints OK, or FAILED and the end of the log. Reads the errorlevel of the
rem dotnet build just before it; %1 is a file that build must have made.
:step_done
if errorlevel 1 goto :step_failed
if not "%~1"=="" if not exist "%~1" goto :step_missing
echo OK
exit /b 0
:step_failed
echo FAILED
call :show_log_tail
exit /b 1
:step_missing
echo FAILED
echo [ERROR] The build did not make: %~1
>>"%LOG%" echo [ERROR] Missing %~1
exit /b 1

:show_log_tail
echo.
echo ---------------- last lines of build.log ----------------
powershell.exe -NoProfile -Command "Get-Content -LiteralPath $env:LOG -Tail 40" 2>nul
if errorlevel 1 type "%LOG%"
echo ---------------------------------------------------------
exit /b 0

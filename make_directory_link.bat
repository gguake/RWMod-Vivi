@echo off

setlocal
for /f "tokens=*" %%a in ('cd') do set "current_directory=%%a"
for %%I in ("%current_directory%") do set "folder_name=%%~nxI"

IF "%RW_MODS_DIR%" == "" (
    @ECHO RW_MODS_DIR environment path is not exists.
) ELSE (
    mklink /J %RW_MODS_DIR%\%folder_name% Mod
)

endlocal
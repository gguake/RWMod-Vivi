@ECHO OFF
IF "%RW_MODS_DIR%" == "" (
    @ECHO RW_MODS_DIR environment path is not exists.
) ELSE (
    mklink /J %RW_MODS_DIR%\%1 Mod
)
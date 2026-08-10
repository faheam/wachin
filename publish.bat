@echo off
REM ============================================================
REM  Wachin - Publicar EXE portátil (un solo archivo)
REM  El resultado queda en: publish\win-x64\
REM
REM  OJO: NO usar EnableCompressionInSingleFile=true
REM  - Con compresion el EXE pesa ~69MB pero tarda en arrancar
REM    (descomprime ensamblados en cada inicio).
REM  - Sin compresion pesa ~163MB pero arranca casi al instante.
REM ============================================================
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o publish\win-x64

echo.
echo Listo. El EXE portátil esta en: publish\win-x64\Wachin.exe
pause

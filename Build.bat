@echo off
echo Publishing TextNarrator (Release, non-self-contained)...
dotnet publish TextNarrator.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishReadyToRun=true
echo Publish completed. Output is in bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\
echo.
pause
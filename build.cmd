@echo off
echo Building CyberScreenSaver...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:CyberScreenSaver.exe /resource:screensaver_oscilloscope.html,screensaver_oscilloscope.html /resource:screensaver_datarain.html,screensaver_datarain.html /resource:screensaver_network.html,screensaver_network.html /resource:screensaver_livenetwork.html,screensaver_livenetwork.html /reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll CyberScreenSaver.cs
if errorlevel 1 goto :error
copy CyberScreenSaver.exe CyberScreenSaver.scr >nul
copy CyberScreenSaver.exe CyberOscilloscope.scr >nul
copy CyberScreenSaver.exe CyberClock.scr >nul
copy CyberScreenSaver.exe CyberNetwork.scr >nul
copy CyberScreenSaver.exe CyberLiveNetwork.scr >nul
echo Build complete!
echo.
echo Building Uninstaller...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:exe /out:CyberScreenSaver-Uninstall.exe Uninstall.cs
if errorlevel 1 goto :error
echo Done!
goto :eof
:error
echo BUILD FAILED
pause

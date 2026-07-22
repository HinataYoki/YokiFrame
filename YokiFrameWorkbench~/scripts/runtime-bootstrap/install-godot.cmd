@echo off
setlocal
call "%~dp0build-current-platform.cmd" %* --open-installer
exit /b %errorlevel%

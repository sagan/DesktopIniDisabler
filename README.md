# DesktopIniDisabler

Prevent the "desktop.ini" files from being created by Windows. Just run the "DesktopIniDisabler.exe" and
it will run in the background with an icon in tray area. The app is written using C# / .NET Framework 4.8
and the [EasyHook](https://github.com/EasyHook/EasyHook) framework.

I write this app for my personal usage and have only tested it on Windows 11 machines.

# How it works

It will find and hook some "explorer.exe", "Dropbox.exe", "GoogleDriveFS.exe" processes to intercept the
CreateFileW win32 API to prevent these processes from creating the desktop.ini files.

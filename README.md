CitizenMP Server Updater
========================

_by Carl Kittelberger_

Updates, compiles and sets up your CitizenMP server automagically!

Features
--------

- Compatible with both Microsoft .NET Framework and Mono (Mac OS X support planned)
- Creates ready-to-use start scripts for your platform
- No extra setup besides usual requirements needed

Requirements
------------

First of all, you'll need Windows or Linux to run this tool. Currently, since no libgit2 binaries can be provided for Mac OS X, this tool won't work there. Here's the guaranteed compatible list of OS this tool can be run on:

- Ubuntu 12.04 (old LTS) or higher, including 14.xx
- Debian 7.x (Wheezy) or higher, including testing and sid installations
- Every other currently stable Linux distribution should work just fine as well
- Any Windows operating system with .NET Framework 4 Full on it

To run this program and the resulting server, you need the following components:

- _On Windows:_ .NET Framework 4 or newer
- _On Linux:_ Mono 2.10.x/3.x or newer

On Ubuntu and Debian you can just install all requirements with this command:

```
sudo apt-get install mono-runtime libmono-system-core4.0-cil
```

Usage
-----

This tool ships with a usage screen that will be printed if this tool is run without arguments. It looks similar to the following:

```
CitizenMP Server Updater 1.0.1+Branch.master.Sha.4475e9becd5fa22563a92fe202f8df21329969fc
© 2014-2015 Carl Kittelberger

This is free software. You may redistribute copies of it under the terms of the MIT License <http://www.opensource.org/licenses/mit-license.php>.

citimp_upd [options...] "<targetpath>"

  -v, --verbosity    (Default: Quiet) Sets the build output verbosity. Possible values: Minimal, Quiet, Normal, Detailed, Diagnostic

  --source           (Default: src) Sets the path where the source files will be stored.

  --log              (Default: True) Write a log file "build.log" to the output folder.

  --version          (Default: False) Shows this tool's version.

  --help             Display this help screen.
```

Examples:

- Install server on Linux/Windows into a folder named "MyServer" via command-line: ```citimp_upd MyServer``` (on Windows) or ```mono citimp_upd.exe MyServer``` (on Linux)
- Install server on Windows into a folder named "MyServer" without command-line: Just create a folder named "MyServer" and drag-n-drop it onto citimp_upd.exe!
- Install server on Windows into "C:\Server\CitizenMP" and put the source code to "C:\Source\CitizenMP": ```citimp_upd.exe --source "C:\Source\CitizenMP" "C:\Server\CitizenMP"```
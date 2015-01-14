Easy server installation on Windows
-----------------------------------

On Windows, you can mostly avoid using the command line for easy usage. Instead, just drag and drop an empty folder onto it. Below is a detailed explanation:

1. Make sure at least .NET Framework 4 is installed on your server computer. If not, you can download it [here](http://microsoft.com/download/details.aspx?id=17718).
2. Download citimp_upd.exe from [here](https://github.com/icedream/citizenmp-server-updater/releases). The newest version is always at the top. Alternatively, you can also [get the latest build from the build server](http://builds.icedream.kthx.at/citiserver-updater-master/lastSuccessfulBuild/citimp_upd.exe), which is a little bit slower.
3. Create a new dedicated folder for citimp_upd.exe. It will later contain both your new server and the source code the tool downloads. Name it something like "CitizenMP" or similar. Move the file into the folder.
4. Inside the folder, create a new folder which will contain your server build. Name it something like "Server" or similar.
5. Drag and drop your newly created folder onto citimp_upd.exe. Wait for the tool to download, compile and prepare your server. The window should go away when done.
6. Now open your folder and like magic, some new files will be in there. Usually, you will now have to edit the configuration file before starting the server, so now would be a good time to do your custom changes.
7. When done, save any files you have open and start the server using the start.bat script that the tool generated for you. The server should display a new window and start up properly.
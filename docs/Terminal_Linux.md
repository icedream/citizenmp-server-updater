Server installation on Linux
----------------------------

On Linux, you should use a terminal (SSH connection/XTerm/...) to set up your server.

1. Make sure at least Mono 2.10.x is installed on your server computer. If not, you can install it using your distribution's package manager. For Debian/Ubuntu the command would normally be ```sudo apt-get install mono-runtime libmono-system-core4.0-cil```.
2. Download the latest citimp_upd.exe from [here](https://github.com/icedream/citizenmp-server-updater/releases/latest). You automate this using this command: ```wget https://github.com/icedream/citizenmp-server-updater/releases/download/v1.0.3/citimp_upd.exe```
3. Create a new dedicated folder for citimp_upd.exe. It will later contain both your new server and the source code the tool downloads. Name it something like "CitizenMP" or similar. Move the file into the folder. Command for that would be ```mkdir CitizenMP```
4. Inside the folder, create a new folder which will contain your server build. Name it something like "Server" or similar. Command for this would be ```cd CitizenMP && mkdir Server```
5. Run the tool and tell it to install files into your newly created folder. Command: ```mono citimp_upd.exe Server```
6. Now open your folder and like magic, some new files will be in there. Usually, you will now have to edit the configuration file before starting the server, so now would be a good time to do your custom changes. Use whatever editor you have to edit the main configuration file (the file that ends with ".yml"), I personally recommend nano for people who don't like complicated editing. :)
7. When done, save any files you have open and start the server using the start.sh script that the tool generated for you. The server should start up properly. The direct command would be ```./start.sh``` but that would spawn the server in the foreground. If you were on an SSH server and disconnected, your server would instantly shut down. Use some tool like "screen", "nohup" or "disown" to detach your server properly.

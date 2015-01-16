using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using CommandLine;
using LibGit2Sharp;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Mono.Unix.Native;
using UnixSyscall = Mono.Unix.Native.Syscall;

namespace CitizenMP.Server.Installer
{
    internal static class Program
    {
        // TODO: Get rid of this if possible
        internal static readonly Signature GitSignature = new Signature("Downloader", "downloader@localhost",
            new DateTimeOffset());

        private static void PrepareDirectory(DirectoryInfo sourcePath, DirectoryInfo targetPath)
        {
            if (!sourcePath.Exists)
            {
                throw new DirectoryNotFoundException("Source directory " + sourcePath.FullName + " does not exist.");
            }

            targetPath.Create();

            foreach (var file in sourcePath.EnumerateFiles())
            {
                var overwrite = !file.Extension.Equals("yml", StringComparison.OrdinalIgnoreCase);
                var target = Path.Combine(targetPath.FullName, file.Name);

                // If file exists and should not be directly overwritten, just remember the user to manually update the file as needed.
                if (File.Exists(target) && !overwrite)
                {
                    var oldTarget = target;
                    target += ".dist";
                    Console.Error.WriteLine(
                        "WARNING: File {0} needs a manual update! Compare with {1} and rewrite your file.", oldTarget,
                        target);
                }

                file.CopyTo(target, overwrite);
            }

            foreach (var subdirectory in sourcePath.EnumerateDirectories())
            {
                PrepareDirectory(subdirectory, targetPath.CreateSubdirectory(subdirectory.Name));
            }
        }

        private static int Main(string[] args)
        {
            var monoRun = RunWithMonoConfiguration(args);
            if (monoRun.Item1)
            {
                // The main stuff already ran in a subprocess
                return monoRun.Item2;
            }

            // Parse cmdline arguments
            var options = new CommandLineOptions();
            //args = args.DefaultIfEmpty("--help").ToArray();
            if (!Parser.Default.ParseArgumentsStrict(args, options, () => { Environment.Exit(-2); }))
            {
                return -2;
            }

            if (options.ShowVersion)
            {
                var version =
                    Regex.Match(
                        Assembly.GetExecutingAssembly()
                            .GetCustomAttributes(typeof (AssemblyInformationalVersionAttribute), false)
                            .OfType<AssemblyInformationalVersionAttribute>().First().InformationalVersion,
                        @"^(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<revision>[0-9]+)(\-(?<prerelease>[A-z0-9\.]+))?(\+(?<meta>.+?))?$");

                var meta = new Queue<string>(version.Groups["meta"].Value.Split('.'));
                while (meta.Any() && meta.First() != "Branch")
                {
                    meta.Dequeue();
                }

                var metaDict = new Dictionary<string, string>();
                while (meta.Any())
                {
                    var name = meta.Dequeue();
                    var value = meta.Dequeue();
                    if (meta.Any() && char.IsDigit(meta.First().First()))
                    {
                        value += "." + meta.Dequeue();
                    }
                    metaDict.Add(name, value);
                }

                Console.WriteLine("{0}.{1}.{2}{3}{4}",
                    version.Groups["major"].Value,
                    version.Groups["minor"].Value,
                    version.Groups["revision"].Value,
                    version.Groups["prerelease"].Success ? "-" + version.Groups["prerelease"].Value : "",
                    metaDict.Any() ? " (" + metaDict["Sha"].Substring(0, 7) + ")" : "");
                return 0;
            }

            if (string.IsNullOrEmpty(options.OutputPath))
            {
                Console.Error.WriteLine("ERROR: No output directory given.");
                Console.Write(options.GetUsage());
                return -2;
            }

            var sourceDirectory = new DirectoryInfo(options.SourceDir);
            var dataSourceDirectory = sourceDirectory
                // Who knows if this directory will somewhen cease to exist...
                .CreateSubdirectory("CitizenMP.Server")
                .CreateSubdirectory("data");
            var outputDirectory = new DirectoryInfo(options.OutputPath);
            var binOutputDirectory = new DirectoryInfo(Path.Combine(outputDirectory.FullName, "bin"));

            // Do we even have a copy or do we need to clone?
            if (!Repository.IsValid(sourceDirectory.FullName))
            {
                if (sourceDirectory.Exists)
                {
                    Console.WriteLine("Deleting source code folder...");
                    sourceDirectory.Delete(true);
                }

                Console.WriteLine("Cloning source code repository...");
                Repository.Clone("http://tohjo.ez.lv/citidev/citizenmp-server.git", sourceDirectory.FullName);
            }
            else
            {
                // Update working dir
                Console.WriteLine("Updating source code...");
                using (var git = new Repository(sourceDirectory.FullName))
                {
                    //git.Network.Pull(GitSignature, new PullOptions());
                    git.UpdateRepository("HEAD");
                }
            }

            // Check if we need to update by parsing AssemblyConfigurationAttribute in server assembly.
            // Should have a space-separated segment saying "CommitHash=<commit hash here>".
            if (binOutputDirectory.Exists)
            {
                var serverBins = binOutputDirectory
                    .EnumerateFiles("*Server.exe", SearchOption.TopDirectoryOnly)
                    .ToArray();
                if (serverBins.Any())
                {
                    var serverAssembly = Assembly.LoadFile(serverBins.First().FullName);
                    var configurationAttribs =
                        serverAssembly.GetCustomAttributes(typeof (AssemblyConfigurationAttribute), false);
                    if (configurationAttribs.Any())
                    {
                        var configurationAttrib = (AssemblyConfigurationAttribute) configurationAttribs.First();
                        foreach (var commitHash in configurationAttrib.Configuration.Split(' ')
                            .Where(section => section.StartsWith("CommitHash="))
                            .Select(section => section.Split('=').Last()))
                        {
                            using (var repo = new Repository(sourceDirectory.FullName))
                            {
                                if (commitHash != repo.Head.Tip.Sha)
                                    continue;

                                // Yup, same commit.
                                Console.WriteLine("Server is already up-to-date!");
                                return 0;
                            }
                        }
                    }
                }
            }

            // Get submodules
            using (var git = new Repository(sourceDirectory.FullName))
            {
                Console.WriteLine("Downloading dependencies...");
                git.UpdateSubmodules();
            }

            // Patch AssemblyInfo.cs to include commit hash in an AssemblyConfigurationAttribute
            Console.WriteLine("Patching assembly information...");
            var assemblyGuidRegex =
                new Regex(
                    @"^[\s]*\[assembly[\s]*:[\s]*Guid[\s]*\([\s]*(?<verbatimPrefix>[@]?)""(?<oldValue>.*?)""[\s]*\)[\s]*\][\s]*$",
                    RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
            var assemblyConfigurationRegex =
                new Regex(
                    @"^[\s]*\[assembly[\s]*:[\s]*AssemblyConfiguration[\s]*\([\s]*(?<verbatimPrefix>[@]?)""(?<oldValue>.*?)""[\s]*\)[\s]*\][\s]*$",
                    RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
            foreach (var assemblyInfoFile in sourceDirectory
                .EnumerateFiles("AssemblyInfo.cs", SearchOption.AllDirectories))
            {
                var sourceCode = File.ReadAllText(assemblyInfoFile.FullName);

                // Parse GUID
                var guid = assemblyGuidRegex.Match(sourceCode).Groups["oldValue"].Value;
                if (!guid.Equals("b14ff4c2-a2e5-416b-ae79-4580cda4d9d1", StringComparison.OrdinalIgnoreCase))
                {
                    //Console.WriteLine("\tSkipping assembly info for GUID \"{0}\" ({1}).", guid, assemblyInfoFile.Directory);
                    continue;
                }
                //Console.WriteLine("\tPatching assembly info for GUID \"{0}\" ({1}).", guid, assemblyInfoFile.Directory);

                if (!assemblyConfigurationRegex.IsMatch(sourceCode))
                {
                    sourceCode += Environment.NewLine;
                    sourceCode += @"// Inserted by CitizenMP Server Updater for version comparison";
                    sourceCode += @"[assembly: AssemblyConfiguration("""")]";
                }

                using (var git = new Repository(sourceDirectory.FullName))
                {
                    sourceCode = assemblyConfigurationRegex.Replace(sourceCode,
                        m => string.Format("[assembly: AssemblyConfiguration({0}\"{1}CommitHash={2}\")]",
                            m.Groups["verbatimPrefix"].Value,
                            m.Groups["oldValue"].Length > 0
                                ? m.Groups["oldValue"].Value + " "
                                : "",
                            // ReSharper disable once AccessToDisposedClosure
                            git.Head.Tip.Sha));
                }

                File.WriteAllText(assemblyInfoFile.FullName, sourceCode);
            }


            // Build project
            //Console.WriteLine("Building server binaries...");
            var slnPath = sourceDirectory.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly)
                .First().FullName;
            outputDirectory.Create();
            var logpath = options.WriteLogFile ? Path.Combine(outputDirectory.FullName, "build.log") : null;
            if (!Build(slnPath, new Dictionary<string, string>
            {
                {"Configuration", "Release"},
                {"Platform", "Any CPU"},
                {"DebugType", Environment.OSVersion.IsWin32() ? "None" : "pdbonly"},
                {"DebugSymbols", false.ToString()},
                {"OutputPath", binOutputDirectory.FullName},
                {"AllowedReferenceRelatedFileExtensions", "\".mdb\"=\"\";\".pdb\"=\"\";\".xml\"=\"\""}
            }, options.Verbosity, logpath))
            {
                Console.Error.WriteLine("Build failed! Please look at {0} for more information.", logpath);
                return 1;
            }

            // Prepare with default files
            PrepareDirectory(dataSourceDirectory, outputDirectory);

            // Write startup scripts
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                {
                    var startScriptPath = Path.Combine(outputDirectory.FullName, "start.sh");
                    File.WriteAllText(
                        startScriptPath,
                        string.Join(
                            Environment.NewLine,
                            @"#!/bin/bash",
                            @"",
                            @"# switch to the script directory",
                            @"cd ""$( dirname ""${BASH_SOURCE[0]}"" )""",
                            @"",
                            @"# run with mono",
                            @"mono ""bin/" + binOutputDirectory.EnumerateFiles("*.exe").First().Name + @""" $@",
                            @""));

                    // TODO: Pretty sure there is an easier way to do a programmatical chmod +x
                    Stat stat;
                    FilePermissions perms;
                    if (UnixSyscall.stat(startScriptPath, out stat) != 0)
                    {
                        perms = FilePermissions.S_IRUSR | FilePermissions.S_IRGRP | FilePermissions.S_IROTH
                                | FilePermissions.S_IWUSR
                                | FilePermissions.S_IXUSR;
                    }
                    else
                    {
                        perms = stat.st_mode;
                    }
                    UnixSyscall.chmod(startScriptPath,
                        perms
                        | FilePermissions.S_IXUSR | FilePermissions.S_IXGRP | FilePermissions.S_IXOTH);
                }
                    break;
                case PlatformID.Win32NT:
                case PlatformID.Win32Windows:
                {
                    var startScriptPath = Path.Combine(outputDirectory.FullName, "start.bat");
                    File.WriteAllText(
                        startScriptPath,
                        string.Join(Environment.NewLine,
                            "@echo off",
                            @"",
                            @":: switch to the script directory",
                            @"pushd ""%~dp0""",
                            @"",
                            @":: run",
                            @"""bin\" + binOutputDirectory.EnumerateFiles("*.exe").First().Name + @""" %*",
                            @""));
                }
                    break;
                default:
                    Console.Error.WriteLine("WARNING: No startup script created. Platform not supported.");
                    break;
            }

            Console.WriteLine("Done.");
            return 0;
        }

        private static Tuple<bool, int> RunWithMonoConfiguration(string[] args)
        {
            if (Environment.OSVersion.IsWin32())
            {
                // .NET Framework does everything properly anyways
                return new Tuple<bool, int>(false, 0);
            }

            var engineAssembly =
                Assembly.Load(
                    "Microsoft.Build.Engine, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
                    .GetName();
            if (engineAssembly.Version.Major < 12)
            {
                //var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var baseDir = Path.GetDirectoryName(Assembly.Load("CommandLine").Location);
                if (baseDir == null)
                    throw new DirectoryNotFoundException("Could not find base directory");

                var tempExePath = Path.Combine(baseDir, new Random().Next(10000, 99999) + ".exe");
                File.Copy(Assembly.GetExecutingAssembly().Location, tempExePath);

                var tempConfigPath = tempExePath + ".config";
                using (var tempConfig = File.OpenWrite(tempConfigPath))
                {
                    using (
                        var srcConfig =
                            Assembly.GetExecutingAssembly()
                                .GetManifestResourceStream(
                                    "CitizenMP.Server.Installer.AppDomainConfigurations.BuildNET45.xml"))
                    {
                        if (srcConfig == null)
                            throw new FileNotFoundException("Could not find config resource");
                        srcConfig.CopyTo(tempConfig);
                        srcConfig.Flush();
                    }
                }

                AppDomain.CurrentDomain.DomainUnload += (sender, e) =>
                {
                    File.Delete(tempConfigPath);
                    File.Delete(tempExePath);
                };

                var arguments = string.Join(" ",
                    new[] {tempExePath}.Concat(args)
                        .Select(a => "\"" + a.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
                var exitCode = RunProcess("mono", arguments, new Dictionary<string, string>
                {
                    {"MONO_IOMAP", "all"}
                });
                return new Tuple<bool, int>(true, exitCode);
            }
            return new Tuple<bool, int>(false, 0);
        }

        private static bool Build(string projectFilePath, IDictionary<string, string> buildProperties,
            LoggerVerbosity verbosity, string logPath = null)
        {
            var workspace = new FileInfo(projectFilePath).Directory;
            if (workspace == null)
                throw new DirectoryNotFoundException(
                    "Somehow the project file is not in a directory. Report this to Icedream!");

            // Mono compatibility
            Environment.SetEnvironmentVariable("MONO_IOMAP", "all");

            // Make sure we restore nuget packages automatically and without permissions interfering with /tmp/nuget/ (edge case)
            var newTempDir = workspace.CreateSubdirectory(".tmp");
            Environment.SetEnvironmentVariable("EnableNuGetPackageRestore", "true");
            Environment.SetEnvironmentVariable("TEMP", newTempDir.FullName);
            Environment.SetEnvironmentVariable("TMP", newTempDir.FullName);

            try
            {
                var pc = new ProjectCollection();
                pc.RegisterLogger(new ConsoleLogger(verbosity));

                var loggers = new List<ILogger>();
                if (logPath != null)
                    loggers.Add(new FileLogger
                    {
                        Parameters = string.Format("LogFile={0}", logPath),
                        Verbosity = LoggerVerbosity.Detailed,
                        ShowSummary = true,
                        SkipProjectStartedText = true
                    });
                loggers.Add(new ConsoleLogger(verbosity) {ShowSummary = false});

                // Import/Update Mozilla certs for NuGet to not fail out on non-Windows machines
                if (!Environment.OSVersion.IsWin32())
                {
                    try
                    {
                        var automaticYesResponder = new Action<string, StreamWriter>((s, writer) =>
                        {
                            if (s == null || !s.Contains("X.509 Certificate"))
                                return;
                            writer.WriteLine("yes");
                        });

                        // TODO: Make sure this does not fail out by checking if mozroots is installed
                        Console.WriteLine("Updating SSL certificates for NuGet...");
                        RunProcessThrowOnException("mozroots", "--import --sync --quiet");
                        RunProcessThrowOnException("certmgr", "-ssl https://go.microsoft.com",
                            lineProcessor: automaticYesResponder);
                        RunProcessThrowOnException("certmgr", "-ssl https://nugetgallery.blob.core.windows.net",
                            lineProcessor: automaticYesResponder);
                        RunProcessThrowOnException("certmgr", "-ssl https://nuget.org",
                            lineProcessor: automaticYesResponder);
                    }
                    catch (Exception error)
                    {
                        Console.Error.WriteLine("ERROR: {0}", error.Message);
                        throw;
                    }
                }

                {
                    // The NuGet.exe that is shipped with CitizenMP.Server has a few bugs...
                    var nugetExePath = Path.Combine(workspace.FullName, ".nuget", "NuGet.exe");
                    Console.WriteLine("Updating NuGet...");
                    File.Delete(nugetExePath);
                    using (var wc = new WebClient())
                    {
                        wc.DownloadFile("https://nuget.org/NuGet.exe", nugetExePath);
                    }
                }

                // Use a different build route if running on the Mono interpreter
                if (!Environment.OSVersion.IsWin32())
                {
                    Console.WriteLine("Building server binaries...");
#pragma warning disable 618
                    // Build doesn't work with the new API on Mono, use the deprecated api
                    var engine = Engine.GlobalEngine;

                    // loggers
                    foreach (var logger in loggers)
                        engine.RegisterLogger(logger);

                    // Apply build properties
                    var buildPropertiesConverted = new BuildPropertyGroup();
                    engine.GlobalProperties = buildPropertiesConverted;
                    foreach (var property in buildProperties)
                        buildPropertiesConverted.SetProperty(property.Key, property.Value);

                    // Load project
                    var result = engine.BuildProjectFile(projectFilePath, new string[0], null, null,
                        BuildSettings.None,
                        null);
#pragma warning restore 618
                    return result;
                }

                // Windows build can make use of the new API which is more efficient
                {
                    Console.WriteLine("Building server binaries...");

                    var buildReq = new BuildRequestData(projectFilePath, buildProperties, null, new[] {"Build"},
                        null);

                    var result = BuildManager.DefaultBuildManager.Build(
                        new BuildParameters(pc)
                        {
                            Loggers = loggers.ToArray(),
                            MaxNodeCount = Environment.ProcessorCount
                        }, buildReq);

                    return result.OverallResult == BuildResultCode.Success;
                }
            }
            finally
            {
                newTempDir.Delete(true);
            }
        }

        private static void RunProcessThrowOnException(string name, string args,
            IDictionary<string, string> envVars = null,
            Action<string, StreamWriter> lineProcessor = null)
        {
            var exitCode = RunProcess(name, args, envVars, lineProcessor);

            if (exitCode != 0)
            {
                throw new Exception(string.Format("Process \"{0} {1}\" exited with error code {2}",
                    name, args, exitCode));
            }
        }

        private static int RunProcess(string name, string args, IDictionary<string, string> envVars = null,
            Action<string, StreamWriter> lineProcessor = null)
        {
            using (var p = new Process
            {
                StartInfo =
                {
                    Arguments = args,
                    FileName = name,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardInput = lineProcessor != null,
                    RedirectStandardOutput = lineProcessor != null
                }
            })
            {
                if (envVars != null)
                    foreach (KeyValuePair<string, string> i in envVars)
                        p.StartInfo.EnvironmentVariables.Add(i.Key, i.Value);

                p.Start();

                if (lineProcessor == null)
                {
                    p.WaitForExit();
                }
                else
                {
                    while (!p.HasExited)
                    {
                        string line = p.StandardOutput.ReadLine();
                        lineProcessor.Invoke(line, p.StandardInput);
                    }
                }

                return p.ExitCode;
            }
        }
    }
}
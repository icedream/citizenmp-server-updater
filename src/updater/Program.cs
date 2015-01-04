﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            // Parse cmdline arguments
            var options = new CommandLineOptions();
            //args = args.DefaultIfEmpty("--help").ToArray();
            if (!Parser.Default.ParseArgumentsStrict(args, options, () => { Environment.Exit(-2); }))
            {
                return -2;
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
                    var configurationAttribs = serverAssembly.GetCustomAttributes(typeof(AssemblyConfigurationAttribute), false);
                    if (configurationAttribs.Any())
                    {
                        var configurationAttrib = (AssemblyConfigurationAttribute)configurationAttribs.First();
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
            Console.WriteLine("Building server binaries...");
            var slnPath = sourceDirectory.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly)
                .First().FullName;
            outputDirectory.Create();
            if (Build(slnPath, new Dictionary<string, string>
            {
                {"Configuration", "Release"},
                {"Platform", "Any CPU"},
                {"DebugType", "None"},
                {"DebugSymbols", false.ToString()},
                {"OutputPath", binOutputDirectory.FullName},
                {"AllowedReferenceRelatedFileExtensions", "\".mdb\"=\"\";\".pdb\"=\"\";\".xml\"=\"\""}
            }, Path.Combine(outputDirectory.FullName, "build.log")).OverallResult == BuildResultCode.Failure)
            {
                Console.Error.WriteLine("Build failed!");
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
                            @"cd ""$( dirname ""${BASH_SOURCE[0]}""",
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

        private static BuildResult Build(string solutionFilePath, IDictionary<string, string> buildProperties,
            string logPath = null)
        {
            var pc = new ProjectCollection();
            pc.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Minimal));

            var buildReq = new BuildRequestData(solutionFilePath, buildProperties, null, new[] {"Build"}, null);

            // Save environment
            var oldMonoInputOutputMapping = Environment.GetEnvironmentVariable("MONO_IOMAP");

            // Mono compatibility
            Environment.SetEnvironmentVariable("MONO_IOMAP", "all");

            var result = BuildManager.DefaultBuildManager.Build(
                new BuildParameters(pc)
                {
                    Loggers =
                        new[]
                        {
                            logPath != null
                                ? new FileLogger
                                {
                                    Parameters =
                                        "logfile=" + logPath,
                                    Verbosity = LoggerVerbosity.Detailed,
                                    ShowSummary = true,
                                    SkipProjectStartedText = true
                                }
                                : new ConsoleLogger(LoggerVerbosity.Quiet)
                        },
                    MaxNodeCount = Environment.ProcessorCount
                }, buildReq);

            // Restore environment
            Environment.SetEnvironmentVariable("MONO_IOMAP", oldMonoInputOutputMapping);

            return result;
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using Microsoft.Build.Framework;

namespace CitizenMP.Server.Installer
{
    internal class CommandLineOptions
    {
        [Option('v', "verbosity", DefaultValue = LoggerVerbosity.Quiet, HelpText = "Sets the build output verbosity. Possible values: Minimal, Quiet, Normal, Detailed, Diagnostic")]
        public LoggerVerbosity Verbosity { get; set; }

        [Option("source", DefaultValue = "src", HelpText = "Sets the path where the source files will be stored.")]
        public string SourceDir { get; set; }

        [Option("log", DefaultValue = true, HelpText = "Write a log file \"build.log\" to the output folder.")]
        public bool WriteLogFile { get; set; }

        [ValueOption(0)]
        public string OutputPath { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var programInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            var assembly = Assembly.GetExecutingAssembly();

            var help = new HelpText
            {
                AddDashesToOption = true,
                AdditionalNewLineAfterOption = true,
                Copyright = programInfo.LegalCopyright,
                Heading = new HeadingInfo(programInfo.ProductName, programInfo.ProductVersion),
                MaximumDisplayWidth = Console.BufferWidth
            };

            var errors = help.RenderParsingErrorsText(this, 2);
            if (!string.IsNullOrEmpty(errors))
            {
                help.AddPreOptionsLine(string.Concat(Environment.NewLine, "ERROR(S):"));
                help.AddPreOptionsLine(errors);
            }

            help.AddPreOptionsLine(" ");
#if NO_COMMANDLINE
            help.AddPreOptionsLine(((AssemblyLicenseAttribute)assembly
                .GetCustomAttributes(typeof(AssemblyLicenseAttribute), false)
                .Single()).Value.Trim());
#endif
            help.AddPreOptionsLine(
                "This is free software. You may redistribute copies of it under the terms of the MIT License <http://www.opensource.org/licenses/mit-license.php>.");
            help.AddPreOptionsLine(" ");
            help.AddPreOptionsLine(string.Format("{0}{1} [options...] \"<targetpath>\"",
                Process.GetCurrentProcess().ProcessName,
                new FileInfo(Assembly.GetExecutingAssembly().Location).Extension));

            help.AddOptions(this);

            return help.ToString();
        }
    }
}
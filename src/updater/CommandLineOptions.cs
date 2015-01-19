using System;
using System.Diagnostics;
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

        [Option("force", DefaultValue = false, HelpText = "Enforce rebuilding the server even if the server's version is up-to-date.")]
        public bool ForceBuild { get; set; }

        [ValueOption(0)]
        public string OutputPath { get; set; }

        [Option("version", DefaultValue=false, HelpText="Shows this tool's version.")]
        public bool ShowVersion { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var asm = Assembly.GetExecutingAssembly();

            var productName =
                asm.GetCustomAttributes(typeof (AssemblyTitleAttribute), false)
                    .OfType<AssemblyTitleAttribute>()
                    .Single()
                    .Title;
            var productVersion =
                asm.GetCustomAttributes(typeof (AssemblyInformationalVersionAttribute), false)
                    .OfType<AssemblyInformationalVersionAttribute>()
                    .Single()
                    .InformationalVersion;
            var productCopyright =
                asm.GetCustomAttributes(typeof (AssemblyCopyrightAttribute), false)
                    .OfType<AssemblyCopyrightAttribute>()
                    .Single()
                    .Copyright;

            var help = new HelpText
            {
                AddDashesToOption = true,
                AdditionalNewLineAfterOption = true,
                Copyright = productCopyright,
                Heading = new HeadingInfo(productName, productVersion),
                MaximumDisplayWidth = Console.BufferWidth
            };

            var errors = help.RenderParsingErrorsText(this, 2);
            if (!string.IsNullOrEmpty(errors))
            {
                help.AddPreOptionsLine(string.Concat(Environment.NewLine, "ERROR(S):"));
                help.AddPreOptionsLine(errors);
            }

            help.AddPreOptionsLine(" ");
#if !NO_COMMANDLINE
            help.AddPreOptionsLine(((AssemblyLicenseAttribute)assembly
                .GetCustomAttributes(typeof(AssemblyLicenseAttribute), false)
                .Single()).Value.Trim());
#else
            help.AddPreOptionsLine(
                "This is free software. You may redistribute copies of it under the terms of the MIT License <http://www.opensource.org/licenses/mit-license.php>.");
#endif
            help.AddPreOptionsLine(" ");
            help.AddPreOptionsLine(string.Format("{0} [options...] \"<targetpath>\"",
                Process.GetCurrentProcess().ProcessName));

            help.AddOptions(this);

            return help.ToString();
        }
    }
}
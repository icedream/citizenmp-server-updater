using System;
using System.IO;
using System.Reflection;

// ReSharper disable once CheckNamespace
internal static class Program
{
    private static int Main(string[] args)
    {
        // Mono's XBuild uses assembly redirects to make sure it uses .NET 4.5 target binaries.
        // We emulate it using our own assembly redirector.
        AppDomain.CurrentDomain.AssemblyLoad += (sender, e) =>
        {
            var assemblyName = e.LoadedAssembly.GetName();
            Console.WriteLine("Assembly load: {0}", assemblyName);
        };

        var mainAsm = Assembly.Load("citizenmp_server_updater");

        mainAsm.GetType("Costura.AssemblyLoader")
            .GetMethod("Attach")
            .Invoke(null, null);

        return (int) mainAsm.GetType("CitizenMP.Server.Installer.Program")
            .GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] {args});
    }
}
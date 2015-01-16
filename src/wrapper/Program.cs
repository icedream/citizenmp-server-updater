using System;
using System.IO;
using System.Reflection;

// ReSharper disable once CheckNamespace
internal static class Program
{
    private static int Main(string[] args)
    {
        var mainAsm = Assembly.Load("citizenmp_server_updater");

        mainAsm.GetType("Costura.AssemblyLoader")
            .GetMethod("Attach")
            .Invoke(null, null);

        return (int) mainAsm.GetType("CitizenMP.Server.Installer.Program")
            .GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] {args});
    }
}
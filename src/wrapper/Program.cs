using System.Linq;
using System.Reflection;

// ReSharper disable once CheckNamespace
internal class Program
{
    private static void Main(string[] args)
    {
        var mainAsm = Assembly.Load("citizenmp_server_updater");

        mainAsm.GetType("Costura.AssemblyLoader")
            .GetMethod("Attach")
            .Invoke(null, null);

        mainAsm.GetType("CitizenMP.Server.Installer.Program")
            .GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] {args});
    }
}
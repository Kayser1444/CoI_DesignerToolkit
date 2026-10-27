using System;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]), managed = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
        {
            foreach (string directory in new[] { root, managed })
            {
                string path = Path.Combine(directory, new AssemblyName(request.Name).Name + ".dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try
        {
            Assembly mod = Assembly.LoadFrom(Path.Combine(root, "DesignerToolkit.dll"));
            Type harmonyType = Assembly.LoadFrom(Path.Combine(root, "0Harmony.dll"))
                .GetType("HarmonyLib.Harmony", true);
            object harmony = Activator.CreateInstance(harmonyType, "BDT.UndoPatchContracts");
            Type[] patches = mod.GetType("CoIDesignerToolkit.UndoPatches", true)
                .GetNestedTypes(BindingFlags.NonPublic);
            if (patches.Length != 4) throw new InvalidOperationException("Expected all four undo hook groups.");
            foreach (Type patch in patches.OrderBy(p => p.Name))
            {
                object processor = harmonyType.GetMethod("CreateClassProcessor").Invoke(harmony, new object[] { patch });
                var methods = (System.Collections.IEnumerable)processor.GetType().GetMethod("Patch").Invoke(processor, null);
                if (methods == null || !methods.Cast<object>().Any())
                    throw new InvalidOperationException("No hook installed for " + patch.Name);
                Console.WriteLine("PASS: " + patch.Name + " installs against the actual game assembly.");
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}

using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        // Load the Dock.Avalonia assembly
        var assembly = Assembly.LoadFrom("/home/chen/.nuget/packages/dock.avalonia/12.1.0/lib/net10.0/Dock.Avalonia.dll");
        
        // Find DeferredContentControl
        var type = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("DeferredContentControl"));
        if (type == null)
        {
            Console.WriteLine("DeferredContentControl not found!");
            // List all types
            foreach (var t in assembly.GetTypes().Where(t => t.Name.Contains("Deferred") || t.Name.Contains("Content")))
            {
                Console.WriteLine($"  Found: {t.FullName}");
            }
            return;
        }
        
        Console.WriteLine($"Found: {type.FullName}");
        Console.WriteLine($"Base: {type.BaseType?.FullName}");
        Console.WriteLine();
        
        // List all properties
        Console.WriteLine("Properties:");
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name} (CanRead={prop.CanRead}, CanWrite={prop.CanWrite})");
        }
        
        Console.WriteLine();
        
        // List all fields
        Console.WriteLine("Fields:");
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Console.WriteLine($"  {field.FieldType.Name} {field.Name}");
        }
        
        Console.WriteLine();
        
        // List all methods
        Console.WriteLine("Methods:");
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.DeclaringType == type)
            {
                var parms = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({parms})");
            }
        }
    }
}

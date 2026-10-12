// RoslynCheck：单文件 C# 编译诊断工具（供 devkit analyzer 调用）。
//
// 用法：
//   dotnet RoslynCheck.dll <file.cs> [<file2.cs> ...] [--reference <dll> ...] [--warnings]
//
// 输出（stdout，JSON 数组，每文件一项）：
//   [
//     {
//       "file": "<file.cs>",
//       "ok": true,
//       "syntax_diagnostics": [ {"severity","code","message","line","column"} ],
//       "compile_diagnostics": [ ... ]
//     }
//   ]
//
// 说明：单文件编译不包含项目上下文（项目引用/宏定义等），因此
//   - 纯语法诊断（SyntaxTree 级）准确可靠，不依赖引用
//   - 编译诊断（语义/引用）可能因缺少引用出现误报（如未解析的命名空间）
//   调用方（devkit analyzer）默认只采用语法诊断的 error 级；--warnings 时输出 warning 级。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class RoslynCheck
{
    private static int Main(string[] args)
    {
        var files = args.Where(a => !a.StartsWith("--")).ToList();
        if (files.Count == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.Error.WriteLine("用法: RoslynCheck <file.cs> [<file2.cs>...] [--reference <dll>...] [--warnings]");
            return 2;
        }

        bool includeWarnings = args.Contains("--warnings");
        var extraRefs = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--reference" && i + 1 < args.Length)
            {
                extraRefs.Add(args[++i]);
            }
        }

        // 引用：运行时可信平台程序集 + 用户额外指定的 dll（所有文件共享）
        var refs = new List<MetadataReference>();
        string? tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (tpa != null)
        {
            foreach (var path in tpa.Split(Path.PathSeparator).Where(p => !string.IsNullOrEmpty(p)))
            {
                refs.Add(MetadataReference.CreateFromFile(path));
            }
        }
        foreach (var r in extraRefs.Where(File.Exists))
        {
            refs.Add(MetadataReference.CreateFromFile(r));
        }

        var results = new List<object>();
        foreach (var file in files)
        {
            results.Add(CheckFile(file, refs, includeWarnings));
        }

        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false }));
        return results.All(r => (bool)r.GetType().GetProperty("ok")!.GetValue(r)!) ? 0 : 1;
    }

    private sealed record DiagnosticPayload(string severity, string code, string message, int line, int column);

    private static object CheckFile(string file, List<MetadataReference> refs, bool includeWarnings)
    {
        if (!File.Exists(file))
        {
            return new
            {
                file,
                ok = false,
                syntax_diagnostics = new[] { new DiagnosticPayload("error", "FILE_NOT_FOUND", "文件不存在", 0, 0) },
                compile_diagnostics = Array.Empty<DiagnosticPayload>(),
            };
        }

        string source = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(source, path: file);

        // 1) 纯语法诊断（SyntaxTree 级，不依赖引用，可靠）
        var syntaxDiagnostics = tree.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(ToPayload)
            .ToList();

        // 2) 编译诊断（含语义/引用，可能有缺引用误报）
        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetFileNameWithoutExtension(file),
            syntaxTrees: new[] { tree },
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compileDiagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(ToPayload)
            .ToList();
        if (!includeWarnings)
        {
            compileDiagnostics = compileDiagnostics.Where(d => d.severity == "error").ToList();
        }

        return new
        {
            file,
            ok = !syntaxDiagnostics.Any(d => d.severity == "error"),
            syntax_diagnostics = syntaxDiagnostics,
            compile_diagnostics = compileDiagnostics,
        };
    }

    private static DiagnosticPayload ToPayload(Diagnostic d)
    {
        var span = d.Location.GetLineSpan();
        return new DiagnosticPayload(
            d.Severity.ToString().ToLowerInvariant(),
            d.Id,
            d.GetMessage(),
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1);
    }
}

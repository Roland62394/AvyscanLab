using System.Collections.Generic;
using System.IO;
using CleanScan.Services;
using Xunit;

namespace CleanScan.Tests.Services;

public class ScriptModuleAssemblyTests
{
    [Fact]
    public void AssembleModules_ProducesIdenticalOutput_ToOriginalTemplate()
    {
        var repoRoot = FindRepoRoot();
        var originalPath = Path.Combine(repoRoot, "Tests", "Fixtures", "ScriptMaster.original.avs");
        if (!File.Exists(originalPath)) return;

        var original = NormalizeLineEndings(File.ReadAllText(originalPath));
        var template = File.ReadAllText(Path.Combine(repoRoot, "ScriptMaster.en.avs"));
        var modules = ScriptModuleRegistry.GetBuiltInModules();
        var assembled = NormalizeLineEndings(ScriptService.AssembleModules(template, modules));

        // Write assembled to file for external diff
        File.WriteAllText(Path.Combine(repoRoot, "Tests", "Fixtures", "ScriptMaster.assembled.avs"), assembled);

        var origLines = original.Split('\n');
        var asmLines = assembled.Split('\n');

        for (int i = 0; i < System.Math.Max(origLines.Length, asmLines.Length); i++)
        {
            var origLine = i < origLines.Length ? origLines[i] : "<EOF>";
            var asmLine = i < asmLines.Length ? asmLines[i] : "<EOF>";
            if (origLine != asmLine)
            {
                // Show 3 lines of context
                var ctx = new System.Text.StringBuilder();
                ctx.AppendLine($"First difference at line {i + 1} (orig has {origLines.Length} lines, asm has {asmLines.Length} lines):");
                for (int j = System.Math.Max(0, i - 3); j <= System.Math.Min(i + 3, System.Math.Max(origLines.Length, asmLines.Length) - 1); j++)
                {
                    var marker = j == i ? ">>>" : "   ";
                    var oL = j < origLines.Length ? origLines[j] : "<EOF>";
                    var aL = j < asmLines.Length ? asmLines[j] : "<EOF>";
                    ctx.AppendLine($"{marker} L{j + 1:D3} ORIG: [{oL}]");
                    ctx.AppendLine($"{marker} L{j + 1:D3} ASM:  [{aL}]");
                }
                Assert.Fail(ctx.ToString());
            }
        }

        Assert.Equal(original.Length, assembled.Length);
    }

    private static string NormalizeLineEndings(string s) =>
        s.Replace("\r\n", "\n").Replace("\r", "\n");

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}

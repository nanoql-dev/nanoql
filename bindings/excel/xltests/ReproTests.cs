// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// Repro export inside Excel: the swap's cells exported in each format and run to the cell's bits (the vector replayed
/// through the experimental tests' build, the C# compiled and run, the Python run); every committed vector whose
/// functions the build holds exported again from its workbook, the committed bytes, the generator's version and the
/// checkout's own path aside; the export's refusals; <c>"recordCalls": false</c>; and the package's stable add-in, from
/// its zip, priced.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public partial class ReproTests : RunnerTest
{
    /// <summary>The swap's NPV cell and a nested call's, each format to the NPV's bits; the swap's handle cell to its handle.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TheSwapInEachFormat()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, "B1", "=nqlInstrumentResultsNPV(nqlDiscountingSwapEngineCalculate2(A9,A2,A5))");
        Xl.Calc(ws.Application);
        foreach (string cell in (string[])["A11", "B1"])
        {
            Assert.Equal(Desk.SwapNpv, Xl.Bits(Xl.Get(ws, cell)));
            Assert.Contains("bit for bit 1 of 1", Replay(Export(ws, cell, "vector")), StringComparison.Ordinal);
            Assert.Equal($"{{\"rows\": 1, \"cols\": 1, \"cells\": [{{\"d\": \"{Desk.SwapNpv}\"}}]}}", RunCSharp(Export(ws, cell, "csharp")));
        }

        string swap = Export(ws, "A9", "vector");
        Assert.Contains($"\"expected\": {{ \"handle\": \"{Xl.Text(ws, "A9")}\" }}", swap, StringComparison.Ordinal);
        Assert.Contains("1 of 1 replayed to their outcome", Replay(swap), StringComparison.Ordinal);
        Assert.Equal("{\"rows\": 1, \"cols\": 1, \"cells\": [{\"h\": \"VanillaSwap\"}]}", RunCSharp(Export(ws, "A9", "csharp")));
    }

    /// <summary>The swap's NPV as Python over nanoql's Python package, run by the interpreter run.py was given (--python).</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TheSwapsPythonRepro()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Calc(ws.Application);
        string dir = AddIns.Scratch();
        string py = Path.Combine(dir, "repro.py");
        File.WriteAllText(py, Export(ws, "A11", "python"));
        (int code, string output) = Run(File.ReadAllText(AddIns.PythonFile).Trim(), dir, "-I", py);
        Assert.True(code == 0, output);
        Assert.Equal($"{{\"rows\": 1, \"cols\": 1, \"cells\": [{{\"d\": \"{Desk.SwapNpv}\"}}]}}", output.Trim());
    }

    /// <summary>
    /// Each committed vector whose functions the test build holds, exported again from its committed workbook after a full
    /// recalculation: the committed bytes, the generator's version and the checkout's path aside; a 1904 workbook's
    /// cell exported from a 1900 workbook's. In an Excel of its own: the workbooks' objects go with it, where in the
    /// runner's instance Excel releases them only later, under the next tests' counts of live objects.
    /// </summary>
    [Fact]
    public void EachCommittedWorkbookExportsItsVector()
    {
        var held = Held();
        Assert.Equal(44, held.Count);
        var failed = new List<string>();
        int n = 0;
        OwnExcel.Run(AddIns.TestXll, app =>
        {
            foreach (string name in held.Where(v => !Conventions(v)))
            {
                n++;
                string got = ExportBook(app, name);
                if (Normal(got) != Normal(File.ReadAllText(Path.Combine(Folder, name + ".json"))))
                {
                    failed.Add($"{name}:\n{got}");
                }
            }
        });
        Assert.Equal(42, n);
        Assert.True(failed.Count == 0, string.Join("\n", failed));
    }

    /// <summary>The desk's two vectors, exported again under the settings file naming the desk's conventions.</summary>
    [Fact]
    public void TheDesksVectorsExportAgain()
    {
        string dir = AddIns.Scratch();
        string xll = Path.Combine(dir, Path.GetFileName(AddIns.TestXll));
        File.Copy(AddIns.TestXll, xll);
        File.Copy(AddIns.Fixture("desk-conventions.xml"), Path.Combine(dir, "desk.xml"));
        File.WriteAllText(Path.ChangeExtension(xll, ".settings.json"), "{\"conventions\": \"desk.xml\"}");
        var desk = Held().Where(Conventions).ToList();
        Assert.Equal(new[] { "b23-settings", "r7-names-desk" }, desk);
        OwnExcel.Run(xll, app =>
        {
            foreach (string name in desk)
            {
                Assert.Equal(Normal(File.ReadAllText(Path.Combine(Folder, name + ".json"))), Normal(ExportBook(app, name)));
            }
        });
    }

    /// <summary>
    /// The export's refusals, each #VALUE! with nqlxlError's reason: a scaled cell, a cell edited to a constant, a tree
    /// mixing a 1904 workbook's calls with a 1900 one's, and a cell reporting a read file's path.
    /// </summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TheExportsRefusals()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, ("B1", "=2*nqlYieldTermStructureDiscount(A5,A1+365)"), ("B2", "=nqlYieldTermStructureDiscount(A5,A1+365)"));
        Xl.Calc(ws.Application);
        Xl.Retry(() => ws.Range["B2"].Value2 = 0.5);
        Refused(ws, "B1", "the cell's value isn't its nanoql call's result");
        Refused(ws, "B2", "the cell's value isn't its nanoql call's result");

        Worksheet w1904 = NewSheet();
        Xl.Retry(() => ((Workbook)w1904.Parent).Date1904 = true);
        Xl.Put(w1904, "A1", "=nqlFlatForward(DATE(2015,9,15),0.03,\"Actual365Fixed\")");
        string book = Xl.Retry(() => ((Workbook)w1904.Parent).Name);
        Xl.Put(ws, "B3", $"=nqlYieldTermStructureDiscount('[{book}]Sheet1'!$A$1,A1+365)");
        Xl.Calc(ws.Application);
        Assert.IsType<double>(Xl.Get(ws, "B3"));
        Refused(ws, "B3", "the cell's calls were made in workbooks of both date systems (1900 and 1904)");

        Xl.Put(ws, "B4", $"=nqlConventionSnapshotSources(nqlConventionsXmlRead(\"{AddIns.Fixture("desk-conventions.xml")}\"))");
        Xl.Calc(ws.Application);
        Assert.Equal(AddIns.Fixture("desk-conventions.xml"), Xl.Text(ws, "B4"));
        Refused(ws, "B4", "the cell reports a read file's path");
    }

    /// <summary>With <c>"recordCalls": false</c> a value cell can't export; a handle cell still does, from its handle's recipe.</summary>
    [Fact]
    public void WithoutRecordedCallsAValueCellDoesntExport()
    {
        string dir = AddIns.Scratch();
        string xll = Path.Combine(dir, Path.GetFileName(AddIns.TestXll));
        File.Copy(AddIns.TestXll, xll);
        File.WriteAllText(Path.ChangeExtension(xll, ".settings.json"), "{\"recordCalls\": false}");
        OwnExcel.Run(xll, app =>
        {
            Worksheet ws = Xl.Sheet(Xl.Retry(() => app.Workbooks.Add()));
            Xl.Put(ws, Desk.Swap);
            Xl.Calc(app);
            Assert.Equal(Desk.SwapNpv, Xl.Bits(Xl.Get(ws, "A11")));
            Refused(ws, "A11", "no nanoql call recorded for that cell");
            Assert.Contains("\"fn\":\"nqlMakeVanillaSwapToVanillaSwap\"", Export(ws, "A9", "vector"), StringComparison.Ordinal);
        });
    }

    /// <summary>The package's stable add-in, extracted from its zip, prices the swap to the C ABI's bits; the
    /// experimental API isn't in it.</summary>
    [Fact]
    public void ThePackagesStableAddInPrices()
        => OwnExcel.Run(AddIns.Package, app =>
        {
            Worksheet ws = Xl.Sheet(Xl.Retry(() => app.Workbooks.Add()));
            Xl.Put(ws, Desk.Swap);
            Xl.Calc(app);
            Assert.Equal(Desk.SwapNpv, Xl.Bits(Xl.Get(ws, "A11")));
            Assert.Equal(Xl.Name, Probe(ws, "=nqlDefaultType(\"FailureToPay\",\"NoRestructuring\")"));
        });

    private static string Folder => Path.Combine(AddIns.Repository, "bindings", "excel", "tests", "repro");

    // A cell's export in a format, its lines' text, each line ended.
    private static string Export(Worksheet ws, string cell, string format)
    {
        Xl.Put(ws, "Z1", $"=nqlxlRepro({cell},\"{format}\")");
        Xl.Calc(ws.Application);
        object?[][] rows = Xl.Spill(ws, "Z1");
        Assert.True(rows.Length > 1, $"{cell} didn't export: {rows[0][0]} {Xl.Text(ws, "Z1")}");
        string text = string.Concat(rows.Select(r => Convert.ToString(r[0], CultureInfo.InvariantCulture) + "\n"));
        Xl.Retry(() => ws.Range["Z1"].ClearContents());
        return text;
    }

    // A cell's export refused: #VALUE!, nqlxlError giving the reason.
    private static void Refused(Worksheet ws, string cell, string why)
    {
        Xl.Put(ws, ("Z1", $"=nqlxlRepro({cell})"), ("Z2", "=nqlxlError(Z1)"));
        Xl.Calc(ws.Application);
        Assert.Equal(Xl.Value, Xl.Get(ws, "Z1"));
        Assert.Contains(why, Xl.Text(ws, "Z2"), StringComparison.Ordinal);
        Xl.Retry(() => ws.Range["Z1:Z2"].ClearContents());
    }

    // A formula's value in a scratch cell, for a function the build may lack (#NAME?).
    private static object? Probe(Worksheet ws, string formula)
    {
        Xl.Put(ws, "Z9", formula);
        Xl.Calc(ws.Application);
        return Xl.Get(ws, "Z9");
    }

    // A committed workbook opened read-only, recalculated whole, and its root cell exported; a -from-1900 vector's cell
    // from a new 1900 workbook.
    private static string ExportBook(Application app, string name)
    {
        Workbook wb = Xl.Retry(() => app.Workbooks.Open(Path.Combine(Folder, "workbooks", name + ".xlsx"), 0, true));
        try
        {
            Xl.Retry(app.CalculateFull);
            if (name.EndsWith("-from-1900", StringComparison.Ordinal))
            {
                Worksheet calls = Xl.Retry(() => (Worksheet)wb.Worksheets["Calls"]);
                int last = Xl.Retry(() => calls.Cells[calls.Rows.Count, 2].End[XlDirection.xlUp].Row);
                Workbook other = Xl.Retry(() => app.Workbooks.Add());
                try
                {
                    return Export(Xl.Sheet(other), $"'[{name}.xlsx]Calls'!$B${last.ToString(CultureInfo.InvariantCulture)}", "vector");
                }
                finally
                {
                    Xl.Retry(() => other.Close(false));
                }
            }

            Worksheet repro = Xl.Retry(() => (Worksheet)wb.Worksheets["Repro"]);
            return string.Concat(Xl.Spill(repro, "A1").Select(r => Convert.ToString(r[0], CultureInfo.InvariantCulture) + "\n"));
        }
        finally
        {
            Xl.Retry(() => wb.Close(false));
        }
    }

    // The committed vectors whose functions the test build holds, by name.
    private static List<string> Held()
    {
        using JsonDocument m = AddIns.Manifest("test");
        var names = m.RootElement.GetProperty("functions").EnumerateArray().Select(f => f.GetProperty("excel").GetString()!)
            .Concat(m.RootElement.GetProperty("own").EnumerateArray().Select(o => o.GetString()!)).ToHashSet(StringComparer.Ordinal);
        return [.. Directory.GetFiles(Folder, "*.json").Order(StringComparer.Ordinal)
            .Where(p => FunctionName().Matches(File.ReadAllText(p)).All(x => names.Contains(x.Groups[1].Value)))
            .Select(p => Path.GetFileNameWithoutExtension(p))];
    }

    private static bool Conventions(string name) => File.ReadAllText(Path.Combine(Folder, name + ".json")).Contains("\"conventions\": ", StringComparison.Ordinal);

    // The generator's version and the checkout's own path, which a read file's path holds, left out.
    private static string Normal(string text)
    {
        string t = Generator().Replace(text, "\"generator\": \"nanoql-excel <version>\"");
        return Checkout().Replace(t, "\"<checkout>\\\\bindings\\\\excel\\\\tests\\\\repro\\\\");
    }

    // The vector replayed through the experimental tests' build (its replayer), its report.
    private static string Replay(string vector)
    {
        string dir = AddIns.Scratch();
        File.WriteAllText(Path.Combine(dir, "cell.json"), vector);
        (int code, string output) = Run("dotnet", dir, AddIns.Replayer, "vectors", dir, AddIns.Folder("texp"));
        Assert.True(code == 0, output);
        return output;
    }

    // The C# compiled against nanoql alone and run (dotnet run of a scratch project), its printed outcome.
    private static string RunCSharp(string csharp)
    {
        string dir = AddIns.Scratch();
        File.WriteAllText(Path.Combine(dir, "Program.cs"), csharp);
        File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project />\n");
        File.WriteAllText(Path.Combine(dir, "Directory.Build.targets"), "<Project />\n");
        string lib = Path.Combine(AddIns.Repository, "src", "NanoQL.Bind", "bin", "Release", "net10.0");
        File.WriteAllText(Path.Combine(dir, "repro.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="NanoQL"><HintPath>{Path.Combine(lib, "NanoQL.dll")}</HintPath></Reference>
                <Reference Include="NanoQL.Extensions"><HintPath>{Path.Combine(lib, "NanoQL.Extensions.dll")}</HintPath></Reference>
              </ItemGroup>
            </Project>

            """);
        (int code, string output) = Run("dotnet", dir, "run", "-c", "Release", "--project", Path.Combine(dir, "repro.csproj"));
        Assert.True(code == 0, output);
        return output.Trim().Split('\n')[^1].Trim();
    }

    private static (int Code, string Output) Run(string file, string dir, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }

        // The test platform's MSBuild settings aren't a scratch build's.
        foreach (string key in psi.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            psi.Environment.Remove(key);
        }

        var output = new System.Text.StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => Append(e.Data);
        p.ErrorDataReceived += (_, e) => Append(e.Data);
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        lock (output)
        {
            return (p.ExitCode, output.ToString());
        }

        void Append(string? line)
        {
            if (line != null)
            {
                lock (output)
                {
                    output.Append(line).Append('\n');
                }
            }
        }
    }

    [GeneratedRegex("\"fn\":\"([A-Za-z0-9]+)\"")]
    private static partial Regex FunctionName();

    [GeneratedRegex("\"generator\": \"nanoql-excel [^\"]*\"")]
    private static partial Regex Generator();

    [GeneratedRegex("\"[A-Za-z]:(?:\\\\\\\\[^\"\\\\]+)*?\\\\\\\\bindings\\\\\\\\excel\\\\\\\\tests\\\\\\\\repro\\\\\\\\")]
    private static partial Regex Checkout();
}

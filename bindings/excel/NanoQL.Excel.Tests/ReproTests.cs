// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>
/// Repro export on the test host: each call's recipe kept by its cell with its outcome, a cell's calls as the vector,
/// and the export's refusals: a cell whose value isn't its call's outcome, an Excel error argument, an object inside a
/// returned map or tuple, a tree mixing date systems, a tree reporting a read file's path, and value cells with the
/// settings' <c>"recordCalls"</c> off.
/// </summary>
[Collection(TestSettings.Name)]
public sealed class ReproTests
{
    /// <summary>Initializes a new instance of the <see cref="ReproTests"/> class, on a clean runtime.</summary>
    public ReproTests() => Reset();

    /// <summary>
    /// Every committed vector's calls made again and exported: the committed bytes, the generator's version and the
    /// checkout's own path aside; each kind and construct the vectors hold among them, a 1904 workbook's calls exported
    /// from a 1900 one's cell.
    /// </summary>
    [Fact]
    public void EveryCommittedVectorExportsAgainAsCommitted()
    {
        var vectors = Vectors.All();
        Assert.Equal(55, vectors.Count);
        var failed = new List<string>();
        foreach ((string name, string text) in vectors)
        {
            Reset();
            ExcelReference root = Vectors.Make(text);

            // The date system is the calls' own, not the exporting workbook's.
            if (name.EndsWith("-from-1900", StringComparison.Ordinal))
            {
                Host.Shift = 0;
            }

            string got = Vectors.Export(root);
            if (Vectors.Normal(got) != Vectors.Normal(text))
            {
                failed.Add($"{name}:\n{got}");
            }
        }

        Assert.True(failed.Count == 0, string.Join("\n", failed));
    }

    /// <summary>A cell whose value isn't its call's outcome (scaled, or edited to a constant) is refused, saying why.</summary>
    [Fact]
    public void ACellWhoseValueIsntItsCallsIsRefused()
    {
        Host.Cell = TestHost.At(0, 0);
        string curve = H(Call("nqlFlatForward", 46328.0, 0.03, "Actual365Fixed"));
        Host.Show(0, 0, curve);
        Host.Cell = TestHost.At(1, 0);
        double df = D(Call("nqlYieldTermStructureDiscount", curve, 46693.0));
        Host.Show(1, 0, 2 * df);
        Assert.Equal("refused: nqlxlRepro: the cell's value isn't its nanoql call's result: export a cell that holds the call alone", Vectors.Export(TestHost.At(1, 0)));
        Host.Show(2, 0, 5.0);
        Assert.Equal("refused: nqlxlRepro: no nanoql call recorded for that cell", Vectors.Export(TestHost.At(2, 0)));
        Host.Show(1, 0, df);
        Assert.StartsWith("{ \"upstream\": null,", Vectors.Export(TestHost.At(1, 0)), StringComparison.Ordinal);
    }

    /// <summary>A call given an Excel error passes it on, and its cell is refused: the error's own cell exports.</summary>
    [Fact]
    public void AnExcelErrorArgumentIsRefused()
    {
        Host.Cell = TestHost.At(0, 0);
        string curve = H(Call("nqlFlatForward", 46328.0, 0.03, "Actual365Fixed"));
        Host.Show(0, 0, curve);
        Host.Cell = TestHost.At(1, 0);
        Assert.Equal(ExcelError.ExcelErrorDiv0, Call("nqlYieldTermStructureDiscount", curve, ExcelError.ExcelErrorDiv0));
        Host.Show(1, 0, ExcelError.ExcelErrorDiv0);
        Assert.Equal("refused: nqlxlRepro: an argument of the cell's nanoql call is an Excel error: export the cell that gave it", Vectors.Export(TestHost.At(1, 0)));
    }

    /// <summary>An object inside a returned tuple has no export form yet: a call on it is refused (an array's element exports).</summary>
    [Fact]
    public void AnObjectInsideATupleIsRefused()
    {
        // The pairs' spill: its anchor shows the first pair's index.
        ExcelReference pairs = Vectors.Make(File.ReadAllText(Path.Combine(Vectors.Folder, "b2-handle-pairs.json")));
        string index = H(Host.ValueOf(pairs));
        Host.Cell = TestHost.At(5, 0);
        Assert.Equal("Euribor", Call("nqlInterestRateIndexFamilyName", index));
        Host.Show(5, 0, "Euribor");
        Assert.Equal("refused: nqlxlRepro: an object inside a returned map or tuple has no export form yet: export the cell that made it", Vectors.Export(TestHost.At(5, 0)));
    }

    /// <summary>A tree whose calls were made in workbooks of both date systems is refused.</summary>
    [Fact]
    public void ATreeMixingDateSystemsIsRefused()
    {
        Host.Shift = 1462;
        Host.Cell = TestHost.At(0, 0);
        string curve = H(Call("nqlFlatForward", 44866.0, 0.03, "Actual365Fixed"));
        Host.Show(0, 0, curve);
        Host.Shift = 0;
        Host.Cell = TestHost.At(1, 0);
        double df = D(Call("nqlYieldTermStructureDiscount", curve, 46693.0));
        Host.Show(1, 0, df);
        Assert.Equal("refused: nqlxlRepro: the cell's calls were made in workbooks of both date systems (1900 and 1904): export from one workbook's calls", Vectors.Export(TestHost.At(1, 0)));
    }

    /// <summary>A tree calling a path reporter, by the name the generated build hands the runtime, is
    /// refused.</summary>
    [Fact]
    public void ATreeReportingAReadFilesPathIsRefused()
    {
        Assert.Contains("nqlConventionSnapshotSources", X.PathReporters);
        Host.Cell = TestHost.At(0, 0);
        string snapshot = H(Call("nqlConventionsXmlRead", Path.Combine(Vectors.Folder, "workbooks", "files", "b23-conventions.xml")));
        Host.Show(0, 0, snapshot);
        Host.Cell = TestHost.At(1, 0);
        object sources = Call("nqlConventionSnapshotSources", snapshot);
        Host.Show(1, 0, sources is object[,] m ? m[0, 0] : sources);
        Assert.Equal("refused: nqlxlRepro: the cell reports a read file's path, which a replay can't reproduce: export a cell that uses the conventions", Vectors.Export(TestHost.At(1, 0)));
    }

    /// <summary>With <c>"recordCalls": false</c> a value cell can't export; a handle cell still does, from its handle's recipe.</summary>
    [Fact]
    public void WithoutRecordedCallsAValueCellDoesntExport()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nanoql-excel-norecord-" + Environment.ProcessId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "nanoql64.settings.json"), "{\"recordCalls\": false}");
        XlSettings.Load(Path.Combine(dir, "nanoql64.xll"), null);
        Host.Cell = TestHost.At(0, 0);
        string curve = H(Call("nqlFlatForward", 46328.0, 0.03, "Actual365Fixed"));
        Host.Show(0, 0, curve);
        Host.Cell = TestHost.At(1, 0);
        Host.Show(1, 0, D(Call("nqlYieldTermStructureDiscount", curve, 46693.0)));
        Assert.Equal("refused: nqlxlRepro: no nanoql call recorded for that cell", Vectors.Export(TestHost.At(1, 0)));
        Assert.Contains("\"fn\":\"nqlFlatForward\"", Vectors.Export(TestHost.At(0, 0)), StringComparison.Ordinal);
    }

    /// <summary>A text result holding "#" is text, not a handle: a handle is only what the store holds.</summary>
    [Fact]
    public void TextHoldingAHashIsText()
    {
        Host.Cell = TestHost.At(0, 0);
        string snapshot = H(Call("nqlConventionsXmlRead", Path.Combine(Vectors.Folder, "workbooks", "files", "desk-conventions.xml")));
        Host.Show(0, 0, snapshot);
        Host.Cell = TestHost.At(1, 0);
        string noted = H(Call("nqlConventionSnapshotAddRootElement", snapshot, "<x:note xmlns:x=\"urn:example\">desk #1</x:note>"));
        Host.Show(1, 0, noted);
        Host.Cell = TestHost.At(2, 0);
        object[,] root = Assert.IsType<object[,]>(Call("nqlConventionSnapshotRootElements", noted));
        Host.Spill(2, 0, root);
        string vector = Vectors.Export(TestHost.At(2, 0));
        Assert.Contains("\"cells\":[1,\"<x:note xmlns:x=\\\"urn:example\\\">desk #1</x:note>\"]", vector, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"handle\":\"<x:note", vector, StringComparison.Ordinal);
    }

    /// <summary>An export with a line past the 32,767 characters a cell holds is refused, naming the call it holds.</summary>
    [Fact]
    public void ALineACellCantHoldIsRefused()
    {
        object[] dates = [.. Enumerable.Range(0, 2000).Select(i => (object)(40000.0 + i))];
        object[] values = [.. Enumerable.Range(0, 2000).Select(i => (object)(0.1 + (i * 1e-7)))];
        Host.Cell = TestHost.At(0, 0);
        Host.Show(0, 0, H(Call("nqlTimeSeries3", "double", Col(dates), Col(values))));
        string refused = Vectors.Export(TestHost.At(0, 0));
        Assert.StartsWith("refused: nqlxlRepro: line 5 of the vector holds ", refused, StringComparison.Ordinal);
        Assert.EndsWith(" characters, past the 32,767 a cell holds: the call to nqlTimeSeries3", refused, StringComparison.Ordinal);

        // A spill of 2,000 draws: the outcome's line.
        Reset();
        Host.Cell = TestHost.At(0, 0);
        string rng = H(Call("nqlMersenneTwisterUniformRng", 42.0));
        Host.Cell = TestHost.At(1, 0);
        string draws = H(Call("nqlMersenneTwisterUniformRngDraws", rng, 2000.0));
        Host.Cell = TestHost.At(2, 0);
        Host.Spill(2, 0, Assert.IsType<object[,]>(OwnFunctions.DrawsValues(draws)));
        Assert.EndsWith(" characters, past the 32,767 a cell holds: the cell's outcome", Vectors.Export(TestHost.At(2, 0)), StringComparison.Ordinal);
    }

    /// <summary>The three formats from one cell: the vector, the C# and the Python; another format is refused.</summary>
    [Fact]
    public void TheFormats()
    {
        ExcelReference root = Vectors.Make(File.ReadAllText(Path.Combine(Vectors.Folder, "r7-names-curve.json")));
        Assert.StartsWith("{ \"upstream\": null, \"generator\": \"nanoql-excel ", Vectors.Export(root), StringComparison.Ordinal);
        Assert.StartsWith("// nqlxlRepro: a cell's nanoql calls as C#.", Vectors.Export(root, "csharp"), StringComparison.Ordinal);
        Assert.Contains("import nanoql", Vectors.Export(root, "python"), StringComparison.Ordinal);
        Assert.Equal("refused: nqlxlRepro: format: \"vector\", \"csharp\" or \"python\", not \"xml\" (Parameter 'format')", Vectors.Export(root, "xml"));
    }}

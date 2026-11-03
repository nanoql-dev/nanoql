// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// The shipped builds load, each in an instance of its own: the rows Excel lists for the <c>.xll</c> are its surface's
/// functions, its own and Excel-DNA's two; a function off the list and one the completeness check withholds are #NAME?;
/// the desk's swap prices to the C ABI's bits.
/// </summary>
public class LoadTests
{
    /// <summary>The experimental build: its surface the checked-in one, byte for byte, and loaded whole.</summary>
    [Fact]
    public void TheExperimentalBuildLoads()
    {
        Assert.Equal(File.ReadAllText(Path.Combine(AddIns.Repository, "bindings", "excel", "functions.json")), File.ReadAllText(Path.Combine(AddIns.Folder("experimental"), "functions.json")));
        Loads(AddIns.Experimental, "experimental", ws =>
        {
            Xl.Put(ws, ("B1", "=nqlBrent()"), ("B2", "=nqlHullWhiteTree()"), ("B3", "=nqlIssuer()"));
            Xl.Calc(ws.Application);
            Assert.Equal(Xl.Name, Xl.Get(ws, "B1"));
            Assert.Equal(Xl.Name, Xl.Get(ws, "B2"));
            Assert.StartsWith("Issuer#", Xl.Text(ws, "B3"), StringComparison.Ordinal);
        });
    }

    /// <summary>The stable build: loaded whole, the experimental API absent.</summary>
    [Fact]
    public void TheStableBuildLoads()
        => Loads(AddIns.Stable, "stable", ws =>
        {
            Xl.Put(ws, "B1", "=nqlIssuer()");
            Xl.Calc(ws.Application);
            Assert.Equal(Xl.Name, Xl.Get(ws, "B1"));
        });

    /// <summary>The test build the runner's instance loads holds the shipped experimental build's functions, each as it ships, and nqlxlStats.</summary>
    [Fact]
    public void TheTestBuildHoldsTheShippedFunctions()
    {
        using JsonDocument shipped = AddIns.CheckedIn(), test = AddIns.Manifest("test");
        var tests = test.RootElement.GetProperty("functions").EnumerateArray().ToDictionary(f => f.GetProperty("excel").GetString()!, f => f.GetRawText());
        Assert.All(shipped.RootElement.GetProperty("functions").EnumerateArray(), f => Assert.Equal(f.GetRawText(), tests.GetValueOrDefault(f.GetProperty("excel").GetString()!)));
        Assert.Contains("nqlxlStats", test.RootElement.GetProperty("own").EnumerateArray().Select(e => e.GetString()));
    }

    private static void Loads(string xll, string build, Action<Worksheet> more)
        => OwnExcel.Run(xll, app =>
        {
            using JsonDocument m = AddIns.Manifest(build);
            int functions = m.RootElement.GetProperty("functions").GetArrayLength(), own = m.RootElement.GetProperty("own").GetArrayLength();
            var rows = (object[,])Xl.Retry(() => app.RegisteredFunctions);
            int registered = Enumerable.Range(rows.GetLowerBound(0), rows.GetLength(0)).Count(i => string.Equals(rows[i, rows.GetLowerBound(1)] as string, xll, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(functions + own + 2, registered);

            Worksheet ws = Xl.Sheet(Xl.Retry(() => app.Workbooks.Add()));
            Xl.Put(ws, Desk.Swap);
            Xl.Calc(app);
            Assert.Equal(Desk.SwapNpv, Xl.Bits(Xl.Get(ws, "A11")));
            more(ws);
        });
}

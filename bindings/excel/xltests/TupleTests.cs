// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// The tuple forms through listed functions: an issuer's pairs of keys and curves in, and back out with their handles;
/// another width refused; a conventions snapshot's root elements out; cash flows' NPV and BPS as one tuple across a
/// row.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class TupleTests : RunnerTest
{
    /// <summary>Pairs holding handles in and out: an issuer's default-probability curves by key.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void AnIssuersPairs()
    {
        Worksheet ws = NewSheet();
        Xl.Put(
            ws,
            ("A1", "=DATE(2015,9,15)"),
            ("A4", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"),
            ("B1", "=nqlPoolAdd(nqlPool(),\"acme\",nqlIssuer())"),
            ("B3", "=nqlPoolDefaultKey(B1,\"acme\")"),
            ("C3", "=nqlFlatHazardRate(A1,0.01,A4)"),
            ("D3", "0.5"),
            ("E1", "=nqlIssuer(B3:C3)"),
            ("E2", "=nqlIssuerProbabilities(E1)"),
            ("E5", "=nqlIssuer(B3:D3)"),
            ("E6", "=nqlxlError(E5)"),
            ("H1", "=nqlDefaultProbabilityTermStructureSurvivalProbability2(F2,2,FALSE)"),
            ("H2", "=nqlDefaultProbabilityTermStructureSurvivalProbability2(C3,2,FALSE)"));
        Xl.Calc(Util.Application);
        Assert.StartsWith("Issuer#", Xl.Text(ws, "E1"), StringComparison.Ordinal);
        object?[][] pairs = Xl.Spill(ws, "E2");
        Assert.Single(pairs);
        Assert.Equal(2, pairs[0].Length);
        Assert.Matches("^[A-Za-z]*Key#[0-9A-F]{16}$", pairs[0][0] as string);
        Assert.StartsWith("FlatHazardRate#", pairs[0][1] as string, StringComparison.Ordinal);
        Assert.Equal(Xl.Bits(Xl.Get(ws, "H2")), Xl.Bits(Xl.Get(ws, "H1")));
        Assert.Equal(Xl.Value, Xl.Get(ws, "E5"));
        Assert.Contains("2 columns, a row per element", Xl.Text(ws, "E6"), StringComparison.Ordinal);
    }

    /// <summary>Pairs out: a snapshot with no foreign root element, an empty collection (#N/A); one added, a row of its position and its text.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ASnapshotsRootElements()
    {
        string dir = AddIns.Scratch();
        string path = Path.Combine(dir, "desk.xml");
        File.Copy(AddIns.Fixture("desk-conventions.xml"), path);
        Worksheet ws = NewSheet();
        const string note = "<x:note xmlns:x=\"\"urn:example\"\">desk</x:note>";
        Xl.Put(
            ws,
            ("G1", $"=nqlConventionSnapshotRootElements(nqlConventionsXmlRead(\"{path}\"))"),
            ("G3", $"=nqlConventionSnapshotRootElements(nqlConventionSnapshotAddRootElement(nqlConventionsXmlRead(\"{path}\"),\"{note}\"))"));
        Xl.Calc(Util.Application);
        Assert.Equal([[Xl.NA]], Xl.Spill(ws, "G1"));
        object?[][] root = Xl.Spill(ws, "G3");
        Assert.Single(root);
        Assert.IsType<double>(root[0][0]);
        Assert.Contains("urn:example", root[0][1] as string, StringComparison.Ordinal);
    }

    /// <summary>A tuple out across a row: a leg's NPV and BPS, each its own function's bits.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ALegsNpvAndBps()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(
            ws,
            ("J1", "=nqlSwapLeg(A9,0)"),
            ("K1", "=nqlCashFlowsNpvBps(J1#,A5,A2)"),
            ("K3", "=nqlCashFlowsNpv(J1#,A5,A2)"),
            ("L3", "=nqlCashFlowsBps(J1#,A5,A2)"));
        Xl.Calc(Util.Application);
        object?[][] t = Xl.Spill(ws, "K1");
        Assert.Single(t);
        Assert.Equal(2, t[0].Length);
        Assert.Equal(Xl.Bits(Xl.Get(ws, "K3")), Xl.Bits(t[0][0]));
        Assert.Equal(Xl.Bits(Xl.Get(ws, "L3")), Xl.Bits(t[0][1]));
    }
}

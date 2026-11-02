// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// nqlxlError's message by cell around a looped call (XLR15, XLP78, XLP79): a nested looped call's cell gives its first
/// failed element's message and place; a neighbour's own failed call keeps its own message, where the nested formula's
/// value equals the first element, under MAX, after a looped call becomes single, and beside a looped call that
/// succeeds; a spill's cells give their elements', an element-wise spill too, a reordered spill the formula's.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class MessageTests : RunnerTest
{
    /// <summary>A looped call nested in SUMPRODUCT with its second element failing (F7-1).</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ANestedLoopedCall()
    {
        Worksheet ws = Dates();
        Xl.Put(
            ws,
            ("A4", "=\"x\""),
            ("E2", "=nqlYieldTermStructureDiscount($C$1,\"y\")"),
            ("E1", "=SUMPRODUCT(nqlYieldTermStructureDiscount($C$1,A3:A12),B3:B12)"),
            ("G1", "=nqlxlError(E1)"),
            ("G2", "=nqlxlError(E2)"),
            ("H3", "=nqlYieldTermStructureDiscount($C$1,A3:A12)"),
            ("I4", "=nqlxlError(H4)"),
            ("I5", "=nqlxlError(H5)"));
        Xl.Calc(Util.Application);
        Assert.Equal(Xl.Value, Xl.Get(ws, "E1"));
        Assert.Contains("row 2, column 1", Xl.Text(ws, "G1"), StringComparison.Ordinal);
        Own(ws, "G2");
        Assert.Contains("row 2, column 1", Xl.Text(ws, "I4"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Xl.Text(ws, "I5"));
    }

    /// <summary>The first element failing, so the formula's #VALUE! equals the spill's first element (F8-1).</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TheFirstElementFailing()
    {
        Worksheet ws = Dates();
        Xl.Put(
            ws,
            ("A3", "=\"x\""),
            ("E2", "=nqlYieldTermStructureDiscount($C$1,\"y\")"),
            ("E1", "=SUMPRODUCT(nqlYieldTermStructureDiscount($C$1,A3:A12),B3:B12)"),
            ("G1", "=nqlxlError(E1)"),
            ("G2", "=nqlxlError(E2)"));
        Xl.Calc(Util.Application);
        Assert.Equal(Xl.Value, Xl.Get(ws, "E1"));
        Assert.Contains("row 1, column 1", Xl.Text(ws, "G1"), StringComparison.Ordinal);
        Own(ws, "G2");
    }

    /// <summary>MAX over falling discount factors, the first element the maximum (F8-1).</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void MaxOverALoopedCall()
    {
        Worksheet ws = Dates();
        Xl.Put(ws, ("E2", "=nqlYieldTermStructureDiscount($C$1,\"y\")"), ("E1", "=MAX(nqlYieldTermStructureDiscount($C$1,A3:A12))"), ("G2", "=nqlxlError(E2)"));
        Xl.Calc(Util.Application);
        Assert.True(Xl.Get(ws, "E1") is double d && d < 1);
        Own(ws, "G2");
    }

    /// <summary>A looped call edited to a single call with the same first value (F8-1).</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ALoopedCallBecomingSingle()
    {
        Worksheet ws = Dates();
        Xl.Put(ws, "E1", "=nqlYieldTermStructureDiscount($C$1,A3:A12)");
        Xl.Calc(Util.Application);
        Assert.Equal(10, Xl.Column(ws, "E1").Length);
        Xl.Put(ws, ("E1", "=nqlYieldTermStructureDiscount($C$1,A3)"), ("E2", "=nqlYieldTermStructureDiscount($C$1,\"y\")"), ("G2", "=nqlxlError(E2)"));
        Xl.Calc(Util.Application);
        Own(ws, "G2");
    }

    /// <summary>A failed single call beside a looped call that succeeds, in one formula (F9-1).</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void AFailedCallBesideALoopedOne()
    {
        Worksheet ws = Dates();
        Xl.Put(ws, ("E1", "=nqlYieldTermStructureDiscount($C$1,\"y\")+SUM(nqlYieldTermStructureDiscount($C$1,A3:A4))"), ("G1", "=nqlxlError(E1)"));
        Xl.Calc(Util.Application);
        Assert.Equal(Xl.Value, Xl.Get(ws, "E1"));
        Own(ws, "G1");
    }

    /// <summary>An element-wise spill (the looped call times 1) gives each element's message; a reordered one the formula's.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void WrappedSpills()
    {
        Worksheet ws = Dates();
        Xl.Put(
            ws,
            ("A4", "=\"x\""),
            ("E1", "=nqlYieldTermStructureDiscount($C$1,A3:A12)*1"),
            ("H1", "=INDEX(nqlYieldTermStructureDiscount($C$1,A3:A12),SEQUENCE(10,1,10,-1))"),
            ("F2", "=nqlxlError(E2)"),
            ("F3", "=nqlxlError(E3)"),
            ("I10", "=nqlxlError(H9)"));
        Xl.Calc(Util.Application);
        Assert.Equal(Xl.Value, Xl.Get(ws, "E2"));
        Assert.Contains("row 2, column 1", Xl.Text(ws, "F2"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Xl.Text(ws, "F3"));
        Assert.Equal(Xl.Value, Xl.Get(ws, "H9"));
        Assert.Contains("row 2, column 1", Xl.Text(ws, "I10"), StringComparison.Ordinal);
    }

    /// <summary>A spill's own cells: a failed element's its message, a value's nothing, the anchor's nothing; a cell outside its own.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ASpillsCells()
    {
        Worksheet ws = Dates();
        Xl.Put(
            ws,
            ("A4", "=\"x\""),
            ("H3", "=nqlYieldTermStructureDiscount($C$1,A3:A12)"),
            ("E2", "=nqlYieldTermStructureDiscount($C$1,\"y\")"),
            ("I3", "=nqlxlError(H3)"),
            ("I4", "=nqlxlError(H4)"),
            ("I5", "=nqlxlError(H5)"),
            ("G2", "=nqlxlError(E2)"));
        Xl.Calc(Util.Application);
        Assert.Contains("row 2, column 1", Xl.Text(ws, "I4"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Xl.Text(ws, "I5"));
        Assert.Equal(string.Empty, Xl.Text(ws, "I3"));
        Own(ws, "G2");
    }

    // A neighbour's own failed call's message: its own refusal, not a spill's element's.
    private static void Own(Worksheet ws, string cell)
    {
        string m = Xl.Text(ws, cell);
        Assert.Contains("got text \"y\"", m, StringComparison.Ordinal);
        Assert.DoesNotContain("of the spill", m, StringComparison.Ordinal);
        Assert.DoesNotContain("row ", m, StringComparison.Ordinal);
    }

    // A curve (C1) and ten dates (A3:A12), each with a weight of 1 (B3:B12).
    private Worksheet Dates()
    {
        Worksheet ws = NewSheet();
        Xl.Put(
            ws,
            ("A1", "=nqlEvaluationContext(DATE(2026,11,2))"),
            ("B1", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"),
            ("C1", "=nqlFlatForward(DATE(2026,11,2),0.03,B1)"));
        for (int k = 0; k < 10; k++)
        {
            Xl.Put(ws, ($"A{3 + k}", $"=DATE(2026,11,2)+{30 * (k + 1)}"), ($"B{3 + k}", "1"));
        }

        return ws;
    }
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A desk's tasks through the listed functions (XLP6, XLP85): the swap and the European call to their bits, a 5-year
/// discount, a curve bootstrapped from deposits and swaps, a calendar by name, the members a listed type inherits, a leg
/// priced, Sobol draws, and a quanto call priced by the engine the list holds.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class TaskTests : RunnerTest
{
    /// <summary>The swap's NPV and fair rate to S-bind's bits; its fixed rate, inherited from FixedVsFloatingSwap; its index's name.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TheSwap()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, ("A12", "=nqlFixedVsFloatingSwapResultsFairRate(A10)"), ("A17", "=nqlFixedVsFloatingSwapFixedRate(A9)"), ("A18", "=nqlInterestRateIndexName(A6)"));
        Xl.Calc(Util.Application);
        Assert.Equal(Desk.SwapNpv, Xl.Bits(Xl.Get(ws, "A11")));
        Assert.Equal(Desk.SwapFairRate, Xl.Bits(Xl.Get(ws, "A12")));
        Assert.Equal(0.031, Xl.Get(ws, "A17"));
        Assert.Equal("Euribor6M Actual/360", Xl.Get(ws, "A18"));
    }

    /// <summary>A 5-year discount on the flat curve, and the European call's NPV and delta, to XLP6's bits.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void TheDiscountAndTheCall()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, Desk.Call);
        Xl.Put(ws, "A13", "=nqlYieldTermStructureDiscount2(A5,5)");
        Xl.Calc(Util.Application);
        Assert.Equal(Desk.Discount5Y, Xl.Bits(Xl.Get(ws, "A13")));
        Assert.Equal(Desk.CallNpv, Xl.Bits(Xl.Get(ws, "C8")));
        Assert.Equal(Desk.CallDelta, Xl.Bits(Xl.Get(ws, "C9")));
    }

    /// <summary>
    /// A curve bootstrapped from three deposits and two swaps, each set of helpers looped from one formula (XLR15): its
    /// 2-year discount, and its nodes out as two columns, dates and values.
    /// </summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ABootstrappedCurve()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(
            ws,
            ("E1", "0.031"), ("E2", "0.032"), ("E3", "0.033"), ("F1", "=\"3M\""), ("F2", "=\"6M\""), ("F3", "=\"1Y\""),
            ("G1", "=nqlDepositRateHelper(E1:E3,F1:F3,2,\"TARGET\",\"ModifiedFollowing\",FALSE,A4,A2)"),
            ("E5", "0.034"), ("E6", "0.036"), ("F5", "=\"2Y\""), ("F6", "=\"5Y\""),
            ("G4", "=nqlSwapRateHelper2(E5:E6,F5:F6,\"TARGET\",\"Annual\",\"Unadjusted\",A4,A6,A2)"),
            ("H1", "=nqlPiecewiseFlatForwardCreate(0,\"TARGET\",G1:G5,A4,A2)"),
            ("H2", "=nqlYieldTermStructureDiscount2(H1,2)"),
            ("H3", "=nqlxlError(H1)"),
            ("J1", "=nqlPiecewiseFlatForwardNodes(H1)"));
        Xl.Calc(Util.Application);
        Assert.True(Xl.Get(ws, "H2") is double d && d > 0.9 && d < 1, $"{Xl.Get(ws, "H2")}: {Xl.Get(ws, "H3")}");
        object?[][] nodes = Xl.Spill(ws, "J1");
        Assert.Equal(6, nodes.Length);
        Assert.All(nodes, r => Assert.Equal(2, r.Length));
        Assert.Equal(42262.0, nodes[0][0]);
        Assert.Equal(nodes.Select(r => (double)r[0]!).Order(), nodes.Select(r => (double)r[0]!));
    }

    /// <summary>A calendar by name where its handle goes: TARGET advances a date by a month.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ACalendarByName()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, ("A1", "=DATE(2015,9,15)"), ("A16", "=nqlCalendarAdvance2(\"TARGET\",A1,\"1M\")"));
        Xl.Calc(Util.Application);
        Assert.Equal(42292.0, Xl.Get(ws, "A16"));
    }

    /// <summary>A method inherited from Swap: the fixed leg spilled, a handle a coupon, priced by CashFlows.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void ALegPriced()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, ("J1", "=nqlSwapLeg(A9,0)"), ("A19", "=nqlCashFlowsNpv(J1#,A5,A2)"), ("A20", "=nqlxlError(A19)"));
        Xl.Calc(Util.Application);
        object?[] leg = Xl.Column(ws, "J1");
        Assert.Equal(10, leg.Length);
        Assert.All(leg, c => Assert.StartsWith("FixedRateCoupon#", c as string, StringComparison.Ordinal));
        Assert.True(Xl.Get(ws, "A19") is double npv && npv > 0, $"{Xl.Get(ws, "A19")}: {Xl.Get(ws, "A20")}");
    }

    /// <summary>A Sobol sequence's draws: four rows of two, in (0, 1).</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void SobolDraws()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, ("L1", "=nqlSobolRsg(2,42)"), ("L2", "=nqlSobolRsgDraws(L1,4)"), ("L3", "=nqlxlDrawsValues(L2)"));
        Xl.Calc(Util.Application);
        object?[][] d = Xl.Spill(ws, "L3");
        Assert.Equal(4, d.Length);
        Assert.All(d, r => Assert.All(r, x => Assert.True(x is double v && v > 0 && v < 1, $"{x}")));
        Assert.Equal([0.5, 0.5], d[0]);
    }

    /// <summary>A quanto call priced by the quanto engine, its underlying engine bound with nothing (XLR10): its NPV and qvega.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void AQuantoCall()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(ws, Desk.Call);
        Xl.Put(
            ws,
            ("C11", "=nqlAnalyticEuropeanEngineBind()"),
            ("C12", "=nqlQuantoVanillaOption(C1,C2)"),
            ("C13", "=nqlQuantoEngineCalculate(C12,A2,C6,C5,C4,0.3,C11)"),
            ("C14", "=nqlInstrumentResultsNPV(C13)"),
            ("C15", "=nqlQuantoOptionResultsQvega(C13)"),
            ("C16", "=nqlxlError(C13)"));
        Xl.Calc(Util.Application);
        Assert.True(Xl.Get(ws, "C14") is double npv && npv > 0, $"{Xl.Get(ws, "C14")}: {Xl.Get(ws, "C16")}");
        Assert.IsType<double>(Xl.Get(ws, "C15"));
    }
}

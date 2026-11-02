// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// C1's kinds' forms in real ranges, where a listed function has them (XLR2, XLP40): a complex number as Excel's text,
/// in and out; a vector in and a matrix out; a map in from two columns; jagged rows in and out; a range of optional
/// numbers with an empty cell inside; draws from a snapshot and the next snapshot's, and a sequence's rows.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class FormsTests : RunnerTest
{
    /// <summary>Ten Mersenne Twister draws from seed 42, as C# draws them (XLP40).</summary>
    private static readonly string[] Mt10 =
    [
        "3FD7F87719A00000", "3FE97D47B6700000", "3FEE6C406B900000", "3FC77ACA87400000", "3FE76C7E8D500000",
        "3FE8F33A88F00000", "3FE32835D7900000", "3FE3196582900000", "3FC3F86B33400000", "3FDC88861E600000",
    ];

    /// <summary>A complex number in Excel's text form: given as text and as Excel's own COMPLEX, read back by Excel's IMREAL and by the function.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void AComplexNumber()
    {
        Worksheet ws = Heston();
        Xl.Put(
            ws,
            ("C1", "=nqlAnalyticHestonEngineChF(B3,\"0.5+0.25i\",1)"),
            ("C2", "=nqlAnalyticHestonEngineChF(B3,COMPLEX(0.5,0.25),1)"),
            ("C3", "=IMREAL(C1)"),
            ("C4", "=IMAGINARY(C1)"),
            ("C5", "=nqlAnalyticHestonEngineChF(B3,C1,1)"),
            ("C6", "=nqlxlError(C1)"));
        Xl.Calc(Util.Application);
        string z = Xl.Text(ws, "C1");
        Assert.Matches(@"^-?[0-9.E+-]+[+-][0-9.E+-]+i$", z);
        Assert.Equal(z, Xl.Text(ws, "C2"));
        Assert.IsType<double>(Xl.Get(ws, "C3"));
        Assert.IsType<double>(Xl.Get(ws, "C4"));
        Assert.EndsWith("i", Xl.Text(ws, "C5"), StringComparison.Ordinal);
    }

    /// <summary>A vector in from a column and a matrix out as a spill: a Heston process's covariance, 2 by 2, symmetric.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void AVectorInAMatrixOut()
    {
        Worksheet ws = Heston();
        Xl.Put(ws, ("D1", "100"), ("D2", "0.04"), ("E1", "=nqlStochasticProcessCovariance(B1,0,D1:D2,0.1)"));
        Xl.Calc(Util.Application);
        object?[][] m = Xl.Spill(ws, "E1");
        Assert.Equal(2, m.Length);
        Assert.All(m, r => Assert.Equal(2, r.Length));
        Assert.All(m.SelectMany(r => r), x => Assert.IsType<double>(x));
        Assert.Equal(m[0][1], m[1][0]);
    }

    /// <summary>
    /// A map in from two columns (XLP40): a default event's recovery rates by seniority, a trailing empty row trimmed,
    /// each rate read back; a duplicate key refused.
    /// </summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void AMapFromTwoColumns()
    {
        Worksheet ws = NewSheet();
        Xl.Put(
            ws,
            ("A1", "=DATE(2015,9,15)"),
            ("B1", "=nqlDefaultType(\"FailureToPay\",\"NoRestructuring\")"),
            ("B2", "=nqlConventionSetCurrency(,\"EUR\")"),
            ("D1", "SnrFor"), ("E1", "0.4"), ("D2", "SubLT2"), ("E2", "0.2"),
            ("B3", "=nqlDefaultEvent(A1,B1,B2,\"SnrFor\",A1+30,D1:E3)"),
            ("B4", "=nqlDefaultEventRecoveryRate(B3,\"SnrFor\")"),
            ("B5", "=nqlDefaultEventRecoveryRate(B3,\"SubLT2\")"),
            ("D5", "SnrFor"), ("E5", "0.4"), ("D6", "snrfor"), ("E6", "0.5"),
            ("B6", "=nqlDefaultEvent(A1,B1,B2,\"SnrFor\",A1+30,D5:E6)"),
            ("B7", "=nqlxlError(B6)"),
            ("B8", "=nqlxlError(B3)"));
        Xl.Calc(Util.Application);
        Assert.True(Xl.Text(ws, "B3").StartsWith("DefaultEvent#", StringComparison.Ordinal), $"{Xl.Get(ws, "B3")}: {Xl.Text(ws, "B8")}");
        Assert.Equal(0.4, Xl.Get(ws, "B4"));
        Assert.Equal(0.2, Xl.Get(ws, "B5"));
        Assert.Equal(Xl.Value, Xl.Get(ws, "B6"));
        Assert.Contains("SnrFor", Xl.Text(ws, "B7"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Jagged rows in and out: a latent model's factor weights, a row per name; a ragged range refused by the model's own check.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void JaggedRows()
    {
        Worksheet ws = NewSheet();
        Xl.Put(
            ws,
            ("K1", "0.5"), ("L1", "0.3"), ("K2", "0.4"), ("L2", "0.2"), ("K3", "0.6"), ("L3", "0.1"),
            ("N1", "=nqlGaussianDefProbLM(K1:L3,\"GaussianQuadrature\")"),
            ("N2", "=nqlGaussianDefProbLMFactorWeights(N1)"),
            ("Q1", "0.5"), ("R1", "0.3"), ("Q2", "0.4"),
            ("T1", "=nqlGaussianDefProbLM(Q1:R2,\"GaussianQuadrature\")"),
            ("T2", "=nqlxlError(T1)"));
        Xl.Calc(Util.Application);
        Assert.StartsWith("GaussianDefProbLM#", Xl.Text(ws, "N1"), StringComparison.Ordinal);
        Assert.Equal(Xl.Range(ws, "K1:L3"), Xl.Spill(ws, "N2"));
        Assert.True(Xl.Get(ws, "T1") is int, $"{Xl.Get(ws, "T1")}");
        Assert.Contains("factors", Xl.Text(ws, "T2"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A range of optional numbers with an empty cell inside (XLP40): that element none, so the leg's second coupon is a
    /// plain floating one between two capped ones; text inside the range refused.
    /// </summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void OptionalNumbersWithAnEmptyCell()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(
            ws,
            ("B20", "=DATE(2016,3,15)"), ("B21", "=DATE(2016,9,15)"), ("B22", "=DATE(2017,3,15)"), ("B23", "=DATE(2017,9,15)"),
            ("C20", "0.05"), ("C22", "0.06"),
            ("D20", "0.05"), ("D21", "=\"x\""), ("D22", "0.06"),
            ("E20", "=nqlIborLegWithNotionals(nqlIborLeg(nqlSchedule(B20:B23),A6),100)"),
            ("E21", "=nqlIborLegWithCaps2(E20,C20:C22)"),
            ("E22", "=nqlIborLegWithCaps2(E20,D20:D22)"),
            ("E23", "=nqlxlError(E22)"),
            ("F20", "=nqlIborLegToLeg(E21,A2)"),
            ("H20", "=nqlxlError(F20)"));
        Xl.Calc(Util.Application);
        object?[] coupons = Xl.Column(ws, "F20");
        Assert.True(coupons.Length == 3, $"{coupons[0]}: {Xl.Text(ws, "H20")}");
        Assert.StartsWith("CappedFlooredIborCoupon#", coupons[0] as string, StringComparison.Ordinal);
        Assert.StartsWith("IborCoupon#", coupons[1] as string, StringComparison.Ordinal);
        Assert.StartsWith("CappedFlooredIborCoupon#", coupons[2] as string, StringComparison.Ordinal);
        Assert.Equal(Xl.Value, Xl.Get(ws, "E22"));
        Assert.Contains("got text \"x\"", Xl.Text(ws, "E23"), StringComparison.Ordinal);
    }

    /// <summary>Draws from a snapshot: ten as C# draws them; five, then five from the next snapshot, the same ten; a sequence's four rows of three.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void DrawsAndSequences()
    {
        Worksheet ws = NewSheet();
        Xl.Put(
            ws,
            ("Z1", "=nqlMersenneTwisterUniformRng(42)"),
            ("Z2", "=nqlMersenneTwisterUniformRngDraws(Z1,10)"),
            ("Z3", "=nqlxlDrawsValues(Z2)"),
            ("AA1", "=nqlMersenneTwisterUniformRngDraws(Z1,5)"),
            ("AA2", "=nqlMersenneTwisterUniformRngDraws(nqlxlDrawsNext(AA1),5)"),
            ("AA3", "=nqlxlDrawsValues(AA2)"),
            ("AB1", "=nqlHaltonRsg(3,42)"),
            ("AB2", "=nqlxlDrawsValues(nqlHaltonRsgDraws(AB1,4))"));
        Xl.Calc(Util.Application);
        Assert.Equal(Mt10, Xl.Column(ws, "Z3").Select(Xl.Bits));
        Assert.Equal(Mt10[5..], Xl.Column(ws, "AA3").Select(Xl.Bits));
        object?[][] h = Xl.Spill(ws, "AB2");
        Assert.Equal(4, h.Length);
        Assert.All(h, r => Assert.Equal(3, r.Length));
    }

    // A Heston process on the swap's sheet's curves (B1), its model (B2) and analytic engine (B3).
    private Worksheet Heston()
    {
        Worksheet ws = NewSheet();
        Xl.Put(ws, Desk.Swap);
        Xl.Put(
            ws,
            ("B5", "=nqlFlatForward(A1,0.01,A4)"),
            ("B1", "=nqlHestonProcess(A5,B5,100,0.04,1,0.04,0.5,-0.7)"),
            ("B2", "=nqlHestonModel(B1)"),
            ("B3", "=nqlAnalyticHestonEngine2(B2)"));
        return ws;
    }
}

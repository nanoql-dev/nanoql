// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel.XlTests;

/// <summary>A desk's cells the tests share, and the bits the plan's readings give for them (XLP6, XLP39, XLP85).</summary>
internal static class Desk
{
    /// <summary>S-bind's swap (BIP4): 15 September 2015, a flat 3% curve, Euribor 6M, 10 years at 3.1%; its NPV in A11.</summary>
    public static readonly (string Cell, string Formula)[] Swap =
    [
        ("A1", "=DATE(2015,9,15)"),
        ("A2", "=nqlEvaluationContext(A1)"),
        ("A3", "=nqlConventionSetQuantLib()"),
        ("A4", "=nqlConventionSetDayCounter(A3,\"Actual365Fixed\")"),
        ("A5", "=nqlFlatForward(A1,0.03,A4)"),
        ("A6", "=nqlConventionSetIborIndex2(A3,\"Euribor\",\"6M\",A5)"),
        ("A7", "=nqlMakeVanillaSwap(\"10Y\",A6)"),
        ("A8", "=nqlMakeVanillaSwapWithFixedRate(A7,0.031)"),
        ("A9", "=nqlMakeVanillaSwapToVanillaSwap(A8,A2)"),
        ("A10", "=nqlDiscountingSwapEngineCalculate2(A9,A2,A5)"),
        ("A11", "=nqlInstrumentResultsNPV(A10)"),
    ];

    /// <summary>The swap's NPV, S-bind's bits through the C ABI.</summary>
    public const string SwapNpv = "BF721BC276DF73C0";

    /// <summary>The swap's fair rate, S-bind's bits.</summary>
    public const string SwapFairRate = "3F9F363E5F9DD6C0";

    /// <summary>The fixed rate the builder solves through the bound swap engine (XLP39), the C# reference's bits.</summary>
    public const string BoundFixedRate = "3F9F363E5F9DD6B4";

    /// <summary>The flat curve's 5-year discount factor (XLP6).</summary>
    public const string Discount5Y = "3FEB8AEB7444DCE0";

    /// <summary>A one-year European call's NPV (XLP6).</summary>
    public const string CallNpv = "4021A796A5CC591A";

    /// <summary>Its delta (XLP6).</summary>
    public const string CallDelta = "3FE25A143D49FA3E";

    /// <summary>
    /// A one-year European call at 100 on the swap's sheet (C1 to C9): a 20% TARGET volatility, a 1% dividend curve, the
    /// swap's 3% curve as the risk-free one; its NPV in C8 and its delta in C9.
    /// </summary>
    public static readonly (string Cell, string Formula)[] Call =
    [
        ("A14", "=nqlConventionSetCalendar(,\"TARGET\")"),
        ("C1", "=nqlPlainVanillaPayoff(\"Call\",100)"),
        ("C2", "=nqlEuropeanExercise(A1+365)"),
        ("C3", "=nqlVanillaOption(C1,C2)"),
        ("C4", "=nqlBlackConstantVol(A1,A14,0.2,A4)"),
        ("C5", "=nqlFlatForward(A1,0.01,A4)"),
        ("C6", "=nqlBlackScholesMertonProcess(100,C5,A5,C4)"),
        ("C7", "=nqlAnalyticEuropeanEngineCalculate(C3,A2,C6)"),
        ("C8", "=nqlInstrumentResultsNPV(C7)"),
        ("C9", "=nqlOneAssetOptionResultsDelta(C7)"),
    ];
}

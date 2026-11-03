// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A range where a function takes one value: a column of dates, a column by a row as a grid of handles, a shorter range
/// padded #N/A, a failed element and its message, looped helpers each its own call's handle, and a curve from them the
/// curve from the single cells.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class LoopTests : RunnerTest
{
    /// <summary>Each looped form against the single calls' cells.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void LoopingOverARange()
    {
        Worksheet ws = NewSheet();
        string[] tenors = ["1M", "3M", "6M"];
        Xl.Put(
            ws,
            ("A1", "=nqlEvaluationContext(DATE(2026,11,2))"),
            ("B1", "=nqlConventionSetDayCounter(,\"Actual365Fixed\")"),
            ("C1", "=nqlFlatForward(DATE(2026,11,2),0.03,B1)"));
        for (int k = 0; k < 10; k++)
        {
            Xl.Put(ws, ($"A{3 + k}", $"=DATE(2026,11,2)+{30 * (k + 1)}"), ($"D{3 + k}", $"=nqlYieldTermStructureDiscount($C$1,A{3 + k})"));
        }

        Xl.Put(
            ws,
            ("C3", "=nqlYieldTermStructureDiscount(C1,A3:A12)"),
            ("E3", "=nqlYieldTermStructureForwardRate2(C1,A3:A5,{\"1M\",\"3M\",\"6M\"},B1,\"Compounded\",\"Annual\")"),
            ("I3", "=nqlInterestRateRate(E3#)"));
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                Xl.Put(
                    ws,
                    ($"{"MNO"[j]}{3 + i}", $"=nqlYieldTermStructureForwardRate2($C$1,A{3 + i},\"{tenors[j]}\",$B$1,\"Compounded\",\"Annual\")"),
                    ($"{"PQR"[j]}{3 + i}", $"=nqlInterestRateRate({"MNO"[j]}{3 + i})"));
            }
        }

        Xl.Put(
            ws,
            ("S3", "=nqlYieldTermStructureDiscount(C1,A3:A5,{TRUE;FALSE})"),
            ("T3", "=A3"), ("T4", "=\"x\""), ("T5", "=A5"),
            ("U3", "=nqlYieldTermStructureDiscount(C1,T3:T5)"),
            ("V3", "=nqlxlError(U4)"),
            ("V4", "=nqlxlError(U3)"),
            ("W3", "0.03"), ("W4", "0.031"), ("W5", "0.032"), ("X3", "=\"1M\""), ("X4", "=\"3M\""), ("X5", "=\"6M\""),
            ("Y1", "=nqlConventionSetCalendar(,\"TARGET\")"),
            ("Y2", "=nqlConventionSetDayCounter(,\"Actual360\")"),
            ("Z3", "=nqlDepositRateHelper(W3:W5,X3:X5,2,Y1,\"ModifiedFollowing\",FALSE,Y2,A1)"));
        for (int k = 0; k < 3; k++)
        {
            Xl.Put(ws, $"AA{3 + k}", $"=nqlDepositRateHelper(W{3 + k},X{3 + k},2,$Y$1,\"ModifiedFollowing\",FALSE,$Y$2,$A$1)");
        }

        Xl.Put(
            ws,
            ("AB1", "=nqlPiecewiseYieldCurve(\"Discount\",\"LogLinear\",\"IterativeBootstrap\",DATE(2026,11,2),Z3#,B1,A1)"),
            ("AB2", "=nqlPiecewiseYieldCurve(\"Discount\",\"LogLinear\",\"IterativeBootstrap\",DATE(2026,11,2),AA3:AA5,B1,A1)"),
            ("AC1", "=nqlYieldTermStructureDiscount(AB1,DATE(2027,3,1))"),
            ("AC2", "=nqlYieldTermStructureDiscount(AB2,DATE(2027,3,1))"));
        Xl.Calc(Util.Application);

        object?[] singles = [.. Enumerable.Range(3, 10).Select(r => Xl.Get(ws, $"D{r}"))];
        Assert.Equal(singles.Select(Xl.Bits), Xl.Column(ws, "C3").Select(Xl.Bits));
        object?[][] grid = [.. Enumerable.Range(0, 3).Select(i => Enumerable.Range(0, 3).Select(j => Xl.Get(ws, $"{"MNO"[j]}{3 + i}")).ToArray())];
        Assert.Equal(grid, Xl.Spill(ws, "E3"));
        object?[][] rates = [.. Enumerable.Range(0, 3).Select(i => Enumerable.Range(0, 3).Select(j => Xl.Get(ws, $"{"PQR"[j]}{3 + i}")).ToArray())];
        Assert.Equal(rates, Xl.Spill(ws, "I3"));
        Assert.Equal([singles[0], Xl.Get(ws, "D4"), Xl.NA], Xl.Column(ws, "S3"));
        Assert.Equal([singles[0], Xl.Value, singles[2]], Xl.Column(ws, "U3"));
        Assert.Contains("row 2, column 1", Xl.Text(ws, "V3"), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Xl.Text(ws, "V4"));
        Assert.Equal(Xl.Range(ws, "AA3:AA5").Select(r => r[0]), Xl.Column(ws, "Z3"));
        Assert.Equal(Xl.Get(ws, "AB2"), Xl.Get(ws, "AB1"));
        Assert.Equal(Xl.Bits(Xl.Get(ws, "AC2")), Xl.Bits(Xl.Get(ws, "AC1")));
        Assert.IsType<double>(Xl.Get(ws, "AC1"));
    }
}

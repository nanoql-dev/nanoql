// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Testing;
using Microsoft.Office.Interop.Excel;

namespace NanoQL.Excel.XlTests;

/// <summary>
/// A calendar's or a day counter's name where its handle goes (XLR16, XLP76): each name form equal to the handle's, the
/// FpML aliases, a misspelt name, a handle the session doesn't hold, a name where another object goes, a joint calendar
/// from two names, and looping with a name.
/// </summary>
[ExcelTestSettings(OutOfProcess = true)]
public class NameTests : RunnerTest
{
    /// <summary>The names against the handles' cells.</summary>
    [ExcelFact(AddIn = AddIns.Test)]
    public void NamesWhereHandlesGo()
    {
        Worksheet ws = NewSheet();
        Xl.Put(
            ws,
            ("A1", "=nqlEvaluationContext(DATE(2026,11,2))"),
            ("B1", "=nqlCalendarAdjust(\"TARGET\",DATE(2026,12,24))"),
            ("B2", "=nqlCalendarAdjust(nqlConventionSetCalendar(,\"TARGET\"),DATE(2026,12,24))"),
            ("C1", "=nqlDayCounterYearFraction(\"Actual360\",DATE(2026,11,2),DATE(2027,5,2))"),
            ("C2", "=nqlDayCounterYearFraction(nqlConventionSetDayCounter(,\"Actual360\"),DATE(2026,11,2),DATE(2027,5,2))"),
            ("D1", "=nqlYieldTermStructureDiscount(nqlFlatForward(DATE(2026,11,2),0.03,\"Actual365Fixed\"),DATE(2027,11,2))"),
            ("D2", "=nqlYieldTermStructureDiscount(nqlFlatForward(DATE(2026,11,2),0.03,nqlConventionSetDayCounter(,\"Actual365Fixed\")),DATE(2027,11,2))"),
            ("E1", "=nqlCalendarAdjust(\"EUTA\",DATE(2026,12,25))"),
            ("E2", "=nqlCalendarAdjust(\"TARGET\",DATE(2026,12,25))"),
            ("F1", "=nqlDayCounterYearFraction(\"ACT/360\",DATE(2026,11,2),DATE(2027,5,2))"),
            ("H1", "=nqlCalendarAdjust(\"TARGETX\",DATE(2026,12,25))"),
            ("H2", "=nqlxlError(H1)"),
            ("I1", "=nqlCalendarAdjust(\"Calendar#0123456789ABCDEF\",DATE(2026,12,25))"),
            ("J1", "=nqlYieldTermStructureDiscount(\"TARGET\",DATE(2027,1,1))"),
            ("K1", "=nqlJointCalendar(\"TARGET\",\"UnitedStates.Settlement\")"),
            ("K2", "=nqlCalendarIsBusinessDay(K1,DATE(2026,11,26))"),
            ("K3", "=nqlCalendarIsBusinessDay(\"TARGET\",DATE(2026,11,26))"));
        for (int k = 0; k < 10; k++)
        {
            Xl.Put(ws, ($"L{3 + k}", $"=DATE(2026,12,20)+{k}"), ($"N{3 + k}", $"=nqlCalendarAdjust(nqlConventionSetCalendar(,\"TARGET\"),L{3 + k},\"Following\")"));
        }

        Xl.Put(ws, "M3", "=nqlCalendarAdjust(\"TARGET\",L3:L12,\"Following\")");
        Xl.Calc(Util.Application);
        Assert.Equal(46380.0, Xl.Get(ws, "B1"));
        Assert.Equal(46380.0, Xl.Get(ws, "B2"));
        Assert.Equal(Xl.Bits(Xl.Get(ws, "C2")), Xl.Bits(Xl.Get(ws, "C1")));
        Assert.Equal(Xl.Bits(Xl.Get(ws, "D2")), Xl.Bits(Xl.Get(ws, "D1")));
        Assert.Equal(Xl.Get(ws, "E2"), Xl.Get(ws, "E1"));
        Assert.Equal(Xl.Bits(Xl.Get(ws, "C1")), Xl.Bits(Xl.Get(ws, "F1")));
        Assert.Equal(Xl.Value, Xl.Get(ws, "H1"));
        Assert.Contains("TARGETX", Xl.Text(ws, "H2"), StringComparison.Ordinal);
        Assert.Equal(Xl.Ref, Xl.Get(ws, "I1"));
        Assert.Equal(Xl.Ref, Xl.Get(ws, "J1"));
        Assert.Equal(false, Xl.Get(ws, "K2"));
        Assert.Equal(true, Xl.Get(ws, "K3"));
        Assert.Equal(Enumerable.Range(3, 10).Select(r => Xl.Get(ws, $"N{r}")), Xl.Column(ws, "M3"));
    }
}

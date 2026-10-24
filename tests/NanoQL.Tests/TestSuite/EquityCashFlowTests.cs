// SPDX-FileCopyrightText: Copyright (C) 2023 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/equitycashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: QuantLib's fixture links relinkable handles that the index and the pricer hold; here they are
// built on the fixture's curves, volatilities and quotes, a handle linked to another value is a fixture built on it
// (bumpMarketData, before the flow is built), and a handle linked to nothing is a null input. The fixture's evaluation
// date and fixings are its context.
[Trait("Oracle", "QuantLib")]
public class EquityCashFlowTests
{
    [Fact]
    public void TestSimpleEquityCashFlow()
    {
        // Testing simple equity cash flow...
        const Real tolerance = 1.0e-6;

        var vars = new CommonVars();

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow(false);

        Real indexStart = vars.EquityIndex.Fixing(cf.BaseDate()!.Value, vars.Context);
        Real indexEnd = vars.EquityIndex.Fixing(cf.FixingDate, vars.Context);

        Real expectedAmount = ((indexEnd / indexStart) - 1.0) * vars.Notional;

        Real actualAmount = cf.Amount(vars.Context);

        Assert.False(
            Math.Abs(actualAmount - expectedAmount) > tolerance,
            $"could not replicate simple equity quanto cash flow\n    actual amount:    {actualAmount}\n"
            + $"    expected amount:    {expectedAmount}\n    index start:    {indexStart}\n    index end:    {indexEnd}\n");
    }

    [Fact]
    public void TestQuantoCorrection()
    {
        // Testing quanto correction...
        CheckQuantoCorrection(true);
        CheckQuantoCorrection(false);

        // Checks whether observers are being notified
        // about changes in market data handles.
        CheckQuantoCorrection(false, true);
    }

    [Fact]
    public void TestErrorWhenBaseDateAfterFixingDate()
    {
        // Testing error when base date after fixing date...
        var vars = new CommonVars();

        var end = new Date(5, January, 2023);
        var start = new Date(5, April, 2023);

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow(vars.EquityIndex, start, end);

        CheckRaisedError(cf, vars.Context, "Fixing date cannot fall before base date.");
    }

    [Fact]
    public void TestErrorWhenQuantoCurveHandleIsEmpty()
    {
        // Testing error when quanto currency curve handle is empty...
        var vars = new CommonVars();

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow();
        cf = cf.SetPricer(new EquityQuantoCashFlowPricer(null, vars.EquityVol, vars.FxVol, vars.Correlation));
        CheckRaisedError(cf, vars.Context, "Quanto currency term structure handle cannot be empty.");
    }

    [Fact]
    public void TestErrorWhenEquityVolHandleIsEmpty()
    {
        // Testing error when equity vol handle is empty...
        var vars = new CommonVars();

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow();
        cf = cf.SetPricer(new EquityQuantoCashFlowPricer(vars.QuantoCcyInterest, null, vars.FxVol, vars.Correlation));
        CheckRaisedError(cf, vars.Context, "Equity volatility term structure handle cannot be empty.");
    }

    [Fact]
    public void TestErrorWhenFXVolHandleIsEmpty()
    {
        // Testing error when FX vol handle is empty...
        var vars = new CommonVars();

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow();
        cf = cf.SetPricer(new EquityQuantoCashFlowPricer(vars.QuantoCcyInterest, vars.EquityVol, null, vars.Correlation));
        CheckRaisedError(cf, vars.Context, "FX volatility term structure handle cannot be empty.");
    }

    [Fact]
    public void TestErrorWhenCorrelationHandleIsEmpty()
    {
        // Testing error when correlation handle is empty...
        var vars = new CommonVars();

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow();
        cf = cf.SetPricer(new EquityQuantoCashFlowPricer(vars.QuantoCcyInterest, vars.EquityVol, vars.FxVol, null));
        CheckRaisedError(cf, vars.Context, "Correlation handle cannot be empty.");
    }

    [Fact]
    public void TestErrorWhenInconsistentMarketDataReferenceDate()
    {
        // Testing error when market data reference dates are inconsistent...
        var vars = new CommonVars();

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow();
        cf = cf.SetPricer(new EquityQuantoCashFlowPricer(
            new FlatForward(new Date(26, January, 2023), 0.02, vars.DayCount), vars.EquityVol, vars.FxVol, vars.Correlation));

        CheckRaisedError(
            cf,
            vars.Context,
            "Quanto currency term structure, equity and FX volatility need to have the same reference date.");
    }

    private static void CheckQuantoCorrection(bool includeDividend, bool bumpData = false)
    {
        const Real tolerance = 1.0e-6;

        var vars = new CommonVars(bumpData);
        EquityIndex equityIndex = includeDividend
            ? vars.EquityIndex
            : vars.EquityIndex.Clone(vars.LocalCcyInterest, null, vars.Spot);

        EquityCashFlow cf = vars.CreateEquityQuantoCashFlow(equityIndex);

        Real strike = vars.EquityIndex.Fixing(cf.FixingDate, vars.Context);
        Real indexStart = vars.EquityIndex.Fixing(cf.BaseDate()!.Value, vars.Context);

        Real time = vars.LocalCcyInterest.TimeFromReference(cf.FixingDate);
        Real rf = vars.LocalCcyInterest.ZeroRate(time, Compounding.Continuous).Rate;
        Real q = includeDividend ? vars.Dividend.ZeroRate(time, Compounding.Continuous).Rate : 0.0;
        Real eqVol = vars.EquityVol.BlackVol(cf.FixingDate, strike);
        Real fxVol = vars.FxVol.BlackVol(cf.FixingDate, 1.0);
        Real rho = vars.Correlation;
        Real spot = vars.Spot;

        Real quantoForward = spot * Math.Exp((rf - q - (rho * eqVol * fxVol)) * time);
        Real expectedAmount = ((quantoForward / indexStart) - 1.0) * vars.Notional;

        Real actualAmount = cf.Amount(vars.Context);

        Assert.False(
            Math.Abs(actualAmount - expectedAmount) > tolerance,
            $"could not replicate equity quanto correction\n    actual amount:    {actualAmount}\n"
            + $"    expected amount:    {expectedAmount}\n    index start:    {indexStart}\n"
            + $"    index end:    {quantoForward}\n    local rate:    {rf}\n    equity volatility:    {eqVol}\n"
            + $"    FX volatility:    {fxVol}\n    correlation:    {rho}\n    spot:    {spot}\n");
    }

    // BOOST_CHECK_EXCEPTION(cf->amount(), Error, ExpectedErrorMessage(message)): QuantLib's Error is an
    // ArgumentException or a NanoQLException in nanoql, its message containing the expected text.
    private static void CheckRaisedError(EquityCashFlow cf, EvaluationContext context, string message)
    {
        Exception? e = Record.Exception(() => cf.Amount(context));
        if (e is not (ArgumentException or NanoQLException))
        {
            Assert.Fail($"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
        }

        Assert.True(
            e.Message.Contains(message, StringComparison.Ordinal),
            $"Error expected to contain: '{message}'. Actual error is: '{e.Message}'.");
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Real forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // The test suite's flatVol(vol, dc): a constant volatility from the evaluation date.
    private static BlackVolTermStructure FlatVol(Real vol, DayCounter dc, EvaluationContext context) =>
        BlackConstantVol.Create(0, new NullCalendar(), vol, dc, context);

    private sealed class CommonVars
    {
        // bumpMarketData's values when bumped
        public CommonVars(bool bumped = false)
        {
            Calendar = new TARGET();
            DayCount = new Actual365Fixed();
            Notional = 1.0e7;

            Today = Calendar.Adjust(new Date(27, January, 2023));
            var context = new EvaluationContext { EvaluationDate = Today };

            LocalCcyInterest = FlatRate(bumped ? 0.04 : 0.0375, DayCount, context);
            Dividend = FlatRate(bumped ? 0.01 : 0.005, DayCount, context);
            QuantoCcyInterest = FlatRate(bumped ? 0.03 : 0.001, DayCount, context);

            EquityVol = FlatVol(bumped ? 0.45 : 0.4, DayCount, context);
            FxVol = FlatVol(bumped ? 0.25 : 0.2, DayCount, context);

            Spot = bumped ? 8710.0 : 8700.0;
            Correlation = 0.4;

            EquityIndex = new EquityIndex("eqIndex", Calendar, new EURCurrency(), LocalCcyInterest, Dividend, Spot);
            IndexFixings fixings = context.Fixings
                .AddFixing(EquityIndex, new Date(5, January, 2023), 9010.0)
                .AddFixing(EquityIndex, Today, 8690.0);
            Context = context with { Fixings = fixings };
        }

        public Date Today { get; }

        public Calendar Calendar { get; }

        public DayCounter DayCount { get; }

        public Real Notional { get; }

        public EquityIndex EquityIndex { get; }

        public YieldTermStructure LocalCcyInterest { get; }

        public YieldTermStructure Dividend { get; }

        public YieldTermStructure QuantoCcyInterest { get; }

        public BlackVolTermStructure EquityVol { get; }

        public BlackVolTermStructure FxVol { get; }

        public Real Spot { get; }

        public Real Correlation { get; }

        public EvaluationContext Context { get; }

        public EquityCashFlow CreateEquityQuantoCashFlow(EquityIndex index, Date start, Date end, bool useQuantoPricer = true)
        {
            var cf = new EquityCashFlow(Notional, index, start, end, end);
            if (useQuantoPricer)
            {
                var pricer = new EquityQuantoCashFlowPricer(QuantoCcyInterest, EquityVol, FxVol, Correlation);
                cf = cf.SetPricer(pricer);
            }

            return cf;
        }

        public EquityCashFlow CreateEquityQuantoCashFlow(EquityIndex index, bool useQuantoPricer = true)
        {
            var start = new Date(5, January, 2023);
            var end = new Date(5, April, 2023);

            return CreateEquityQuantoCashFlow(index, start, end, useQuantoPricer);
        }

        public EquityCashFlow CreateEquityQuantoCashFlow(bool useQuantoPricer = true) =>
            CreateEquityQuantoCashFlow(EquityIndex, useQuantoPricer);
    }
}

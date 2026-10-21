// SPDX-FileCopyrightText: Copyright (C) 2018 Tom Anderson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/forwardrateagreement.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ForwardRateAgreementTests
{
    // Adapted: the quotes are given their values before the curve is built, where QuantLib sets them after building
    // the helpers and the FRAs on relinkable handles; the FRAs are built on an index forecasting on the bootstrapped
    // curve, which QuantLib's handle links. QL_USE_INDEXED_COUPON is undefined in QuantLib's default build, so the
    // helpers use the indexed coupon.
    [Fact]
    public void TestConstructionWithoutACurve()
    {
        // Testing forward rate agreement construction...

        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        // set up the index
        IborIndex index = new USDLibor(new Period(3, TimeUnit.Months));

        // determine the settlement date for a FRA
        Date settlementDate = index.FixingCalendar.Advance(today, index.FixingDays, TimeUnit.Days);

        // set up quotes
        double[] quotes = [0.01, 0.02, 0.03];

        bool useIndexedFra = true;

        // set up the curve (this bit is a very rough sketch - i'm actually using swaps !)
        BootstrapHelper<YieldTermStructure>[] helpers =
        [
            new FraRateHelper(
                quotes[0], new Period(1, TimeUnit.Years), index, context, PillarChoice.LastRelevantDate, null, useIndexedFra),
            new FraRateHelper(
                quotes[1], new Period(2, TimeUnit.Years), index, context, PillarChoice.LastRelevantDate, null, useIndexedFra),
            new FraRateHelper(
                quotes[2], new Period(3, TimeUnit.Years), index, context, PillarChoice.LastRelevantDate, null, useIndexedFra),
        ];
        var curve = new PiecewiseYieldCurve<ForwardRate, Cubic, IterativeBootstrap>(
            today, helpers, index.DayCounter, context);

        IborIndex linked = new USDLibor(new Period(3, TimeUnit.Months), curve);

        // set up the instrument to price
        // check the constructor without maturity date
        // inferring maturity date from the index
        var fra = new ForwardRateAgreement(
            linked, settlementDate + new Period(12, TimeUnit.Months), PositionType.Long, 0, 1, curve);

        double rate = fra.Calculate(context).ForwardRate!.Rate;
        Assert.True(Math.Abs(rate - 0.01) <= 1e-6, $"grid creation failed for FRA without maturityDate, got rate {rate} expected 0.01");

        // check the constructor with explicit maturity date
        var fra2 = new ForwardRateAgreement(
            linked,
            settlementDate + new Period(12, TimeUnit.Months),
            settlementDate + new Period(15, TimeUnit.Months),
            PositionType.Long,
            0,
            1,
            curve);

        double rate2 = fra2.Calculate(context).ForwardRate!.Rate;
        Assert.True(Math.Abs(rate2 - 0.01) <= 1e-6, $"grid creation failed for FRA with maturityDate, got rate {rate} expected 0.01");
    }
}

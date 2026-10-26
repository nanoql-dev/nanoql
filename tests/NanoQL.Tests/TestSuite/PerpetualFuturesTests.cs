// SPDX-FileCopyrightText: Copyright (C) 2025 Hiroto Ogawa
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/perpetualfutures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PerpetualFuturesTests
{
    [Fact]
    public void TestPerpetualFuturesValues()
    {
        // Testing perpetual futures value against analytic form for constant parameters...
        PerpetualFuturesData[] values =
        [
            // Discrete time
            new(PerpetualFuturesPayoffType.Linear, PerpetualFuturesFundingType.FundingWithPreviousSpot, new Period(3, TimeUnit.Months), 10000.0, 0.04, 0.02, 0.01, 0.005, 1.0e-6),
            new(PerpetualFuturesPayoffType.Linear, PerpetualFuturesFundingType.FundingWithCurrentSpot, new Period(3, TimeUnit.Months), 10000.0, 0.04, 0.02, 0.01, 0.005, 1.0e-6),
            new(PerpetualFuturesPayoffType.Inverse, PerpetualFuturesFundingType.FundingWithPreviousSpot, new Period(3, TimeUnit.Months), 10000.0, 0.04, 0.02, 0.01, 0.005, 1.0e-6),
            new(PerpetualFuturesPayoffType.Inverse, PerpetualFuturesFundingType.FundingWithCurrentSpot, new Period(3, TimeUnit.Months), 10000.0, 0.04, 0.02, 0.01, 0.005, 1.0e-6),
            new(PerpetualFuturesPayoffType.Linear, PerpetualFuturesFundingType.FundingWithPreviousSpot, new Period(3, TimeUnit.Months), 10000.0, 0.04, 0.02, 0.01, 0.005, 1.0e-6),

            // Continuous time
            new(PerpetualFuturesPayoffType.Linear, PerpetualFuturesFundingType.FundingWithPreviousSpot, new Period(0, TimeUnit.Months), 10000.0, 0.04, 0.02, 0.2, 0.005, 1.0e-6),
            new(PerpetualFuturesPayoffType.Inverse, PerpetualFuturesFundingType.FundingWithPreviousSpot, new Period(0, TimeUnit.Months), 10000.0, 0.04, 0.02, 0.2, 0.005, 1.0e-6),
        ];

        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        Calendar cal = new NullCalendar();

        // Adapted: QuantLib's Date::todaysDate(), fixed (D13: no today default)
        var today = new Date(15, October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (PerpetualFuturesData value in values)
        {
            var trade = new PerpetualFutures(value.PayoffType, value.FundingType, value.FundingFreq, cal, dc);
            YieldTermStructure domCurve = new FlatForward(today, value.R, dc);
            YieldTermStructure forCurve = new FlatForward(today, value.Q, dc);
            double[] fundingTimes = [0.0], fundingRates = [value.K], interestRateDiffs = [value.IDiff];
            Real calculated = DiscountingPerpetualFuturesEngine.Calculate(
                trade, context, domCurve, forCurve, value.S, fundingTimes, fundingRates, interestRateDiffs).NPV!.Value;

            // analytic
            // for details, refer to
            // Perpetual Futures Pricing, Damien Ackerer, Julien Hugonnier, Urban Jermann, 2024
            // https://finance.wharton.upenn.edu/~jermann/AHJ-main-10.pdf
            Real dt = value.FundingFreq.Units switch
            {
                TimeUnit.Years => value.FundingFreq.Length,
                TimeUnit.Months => value.FundingFreq.Length / 12.0,
                TimeUnit.Weeks => value.FundingFreq.Length * 7.0 / 365.0,
                TimeUnit.Days => value.FundingFreq.Length / 365.0,
                TimeUnit.Hours => value.FundingFreq.Length / 365.0 / 24.0,
                TimeUnit.Minutes => value.FundingFreq.Length / 365.0 / 24.0 / 60.0,
                TimeUnit.Seconds => value.FundingFreq.Length / 365.0 / 24.0 / 60.0 / 60.0,
                _ => throw new NanoQLException("Unknown fundingFrequency unit"),
            };
            Real expected = 0.0;

            // Discrete time
            if (value.FundingFreq.Length > 0)
            {
                if (value.PayoffType == PerpetualFuturesPayoffType.Linear)
                {
                    if (value.FundingType == PerpetualFuturesFundingType.FundingWithPreviousSpot)
                    {
                        // Equation (12) in the above paper
                        expected = value.S * (value.K - value.IDiff) * Math.Exp(value.Q * dt)
                            / (Math.Exp(value.Q * dt) - Math.Exp(value.R * dt) + (value.K * Math.Exp(value.Q * dt)));
                    }
                    else if (value.FundingType == PerpetualFuturesFundingType.FundingWithCurrentSpot)
                    {
                        // at the end of "3 Perpetual futures pricing" in the above paper
                        expected = value.S * (value.K - value.IDiff) * Math.Exp(value.R * dt)
                            / (Math.Exp(value.Q * dt) - Math.Exp(value.R * dt) + (value.K * Math.Exp(value.R * dt)));
                    }
                }
                else if (value.PayoffType == PerpetualFuturesPayoffType.Inverse)
                {
                    if (value.FundingType == PerpetualFuturesFundingType.FundingWithPreviousSpot)
                    {
                        // "Proposition 2" in the above paper
                        expected = value.S
                            * (Math.Exp(value.R * dt) - Math.Exp(value.Q * dt) + (value.K * Math.Exp(value.R * dt)))
                            / (value.K - value.IDiff) / Math.Exp(value.R * dt);
                    }
                    else if (value.FundingType == PerpetualFuturesFundingType.FundingWithCurrentSpot)
                    {
                        expected = value.S
                            * (Math.Exp(value.R * dt) - Math.Exp(value.Q * dt) + (value.K * Math.Exp(value.Q * dt)))
                            / (value.K - value.IDiff) / Math.Exp(value.Q * dt);
                    }
                }
            }
            else
            {
                // Continuous time
                if (value.PayoffType == PerpetualFuturesPayoffType.Linear)
                {
                    // "Proposition 3" in the above paper
                    expected = value.S * (value.K - value.IDiff) / (value.Q - value.R + value.K);
                }
                else if (value.PayoffType == PerpetualFuturesPayoffType.Inverse)
                {
                    // "Proposition 4" in the above paper
                    expected = value.S * (value.R - value.Q + value.K) / (value.K - value.IDiff);
                }
            }

            Real relError = Math.Abs((calculated / expected) - 1.0);
            Assert.False(
                relError > value.RelTol,
                $"{value.PayoffType} perpetual futures with {value.FundingType} funding type:\n"
                + $"    spot value:                      {value.S}\n    risk-free rate:                  {value.R}\n"
                + $"    asset yield:                     {value.Q}\n    funding rate:                    {value.K}\n"
                + $"    interest rate differential:      {value.IDiff}\n    funding frequency:               {value.FundingFreq}\n"
                + $"    reference date:                  {today}\n    expected   value: {expected}\n"
                + $"    calculated value: {calculated}\n    rel error: {relError}\n    tolerance: {value.RelTol}\n");
        }
    }

    private readonly record struct PerpetualFuturesData(
        PerpetualFuturesPayoffType PayoffType,
        PerpetualFuturesFundingType FundingType,
        Period FundingFreq,
        Real S,
        Rate R,
        Rate Q,
        Rate K,
        Rate IDiff,
        Real RelTol);
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The American window's bounds on the tree (upstream's fix after v1.44, #2878: <c>51c6263b7</c>, merged as
/// <c>eaeac5651</c>): <c>TimeGrid(T, N)</c> builds its last time as (T/N)·N, an ulp off T, the discretized assets snap
/// their exercise times to the grid but start at the unsnapped maturity, and QuantLib 1.44's American branch compares
/// the two with plain <c>&lt;=</c> and <c>&gt;=</c>; so where the last time rounds below T the exercise is never
/// applied at expiry, and where it rounds above, a window ending at maturity is rejected. Upstream compares both bounds
/// with <c>isOnTime</c>, as the European branch does, in <c>DiscretizedVanillaOption</c> and
/// <c>DiscretizedConvertible</c>. On the wheel, at the upstream cases' market (21 September 2026, 91 days to expiry on
/// Actual/365 (Fixed), spot 100, r = 5%, q = 0, σ = 15%), where the last time rounds below T at 51 steps and above it
/// at 101: the CRR call struck at 85 at 51 steps, American 16.05280871761558 against European 16.074532566935556;
/// exercisable only at maturity at 101 steps, American 0.0 against European 16.075288548193498; and the zero-coupon
/// convertible (conversion ratio 2, redemption 100, no credit spread) at 51 steps, American at maturity
/// 98.76131265936051 against European 200.00004055297964. nanoql takes upstream's fix; its cases are upstream's
/// (<c>europeanoption.cpp</c>'s <c>testBinomialAmericanExerciseAtMaturity</c> and
/// <c>testBinomialAmericanExerciseOnlyAtMaturity</c>, <c>convertiblebonds.cpp</c>'s
/// <c>testBinomialConversionAtMaturity</c>), until the pin moves to a release that carries the fix.
/// </summary>
public class AmericanBoundsOnTimeTests
{
    private static readonly Date Today = new(21, Month.September, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly Date Maturity = Today + 91;
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.0, Dc), new FlatForward(Today, 0.05, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.15, Dc));

    [Fact]
    public void TheAmericanExerciseAtMaturityIsApplied()
    {
        // Testing binomial American exercise at maturity...
        var americanExercise = new AmericanExercise(Today, Maturity);
        var europeanExercise = new EuropeanExercise(Maturity);

        // At 51 steps, the last grid time can round below the unsnapped maturity.
        int[] steps = [3, 7, 13, 25, 51, 75, 101, 151, 251, 299];
        double tolerance = 1.0e-10;
        foreach (int n in steps)
        {
            foreach ((string name, Func<VanillaOption, OneAssetOptionResults> engine) in new (string, Func<VanillaOption, OneAssetOptionResults>)[]
                     {
                         ("CRR", o => BinomialCRRVanillaEngine.Calculate(o, Context, Process, n)),
                         ("JR", o => BinomialJRVanillaEngine.Calculate(o, Context, Process, n)),
                         ("LR", o => BinomialLRVanillaEngine.Calculate(o, Context, Process, n)),
                     })
            {
                foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                {
                    var payoff = new PlainVanillaPayoff(type, 85.0);
                    double americanValue = engine(new VanillaOption(payoff, americanExercise)).NPV!.Value;
                    double europeanValue = engine(new VanillaOption(payoff, europeanExercise)).NPV!.Value;

                    // The American exercise set includes exercise at European expiry.
                    Assert.True(
                        americanValue >= europeanValue - tolerance,
                        $"{name} {type} with {n} steps: American value {americanValue} is below European value {europeanValue}");

                    // With no dividends and positive rates, early call exercise has no value.
                    if (name == "CRR" && n == 51 && type == OptionType.Call)
                    {
                        Assert.True(
                            Math.Abs(americanValue - europeanValue) <= tolerance,
                            $"Non-dividend-paying CRR call with 51 steps: American value {americanValue} differs from European value {europeanValue}");
                    }
                }
            }
        }
    }

    [Fact]
    public void AnExerciseOnlyAtMaturityIsTheEuropean()
    {
        // Testing binomial American exercise only at maturity...
        var americanExercise = new AmericanExercise(Maturity, Maturity);
        var europeanExercise = new EuropeanExercise(Maturity);

        // At 101 steps, the snapped lower bound rounds above the unsnapped maturity.
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            var payoff = new PlainVanillaPayoff(type, 85.0);
            double americanValue = BinomialCRRVanillaEngine.Calculate(new VanillaOption(payoff, americanExercise), Context, Process, 101).NPV!.Value;
            double europeanValue = BinomialCRRVanillaEngine.Calculate(new VanillaOption(payoff, europeanExercise), Context, Process, 101).NPV!.Value;
            Assert.True(
                Math.Abs(americanValue - europeanValue) <= 1.0e-10,
                $"{type} with 101 steps: terminal-only American value {americanValue} differs from European value {europeanValue}");
        }
    }

    [Fact]
    public void TheConvertibleConvertsAtMaturity()
    {
        // Testing binomial convertible-bond conversion at maturity...
        int timeSteps = 51;
        var europeanExercise = new EuropeanExercise(Maturity);
        var americanExercise = new AmericanExercise(Maturity, Maturity);
        Schedule schedule = new MakeSchedule().From(Today).To(Maturity).WithFrequency(Frequency.Once)
                                              .WithCalendar(new NullCalendar()).WithConvention(BusinessDayConvention.Unadjusted)
                                              .ToSchedule();
        double conversionRatio = 2.0;
        double redemption = 100.0;
        var european = new ConvertibleZeroCouponBond(europeanExercise, conversionRatio, [], Today, 0, Dc, schedule, redemption);
        var american = new ConvertibleZeroCouponBond(americanExercise, conversionRatio, [], Today, 0, Dc, schedule, redemption);
        double europeanValue = BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(european, Context, Process, timeSteps, 0.0).NPV!.Value;
        double americanValue = BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(american, Context, Process, timeSteps, 0.0).NPV!.Value;

        // Even the lowest terminal CRR stock node converts for more than redemption.
        // Discounting that minimum payoff with the TF lattice's simple rate gives a lower bound.
        double dt = Dc.YearFraction(Today, Maturity) / timeSteps;
        double minimumConversion = conversionRatio * 100.0 * Math.Exp(-(timeSteps * 0.15 * Math.Sqrt(dt)));
        double lowerBound = minimumConversion / Math.Pow(1.0 + (0.05 * dt), timeSteps);
        Assert.True(minimumConversion > redemption);
        Assert.True(
            americanValue >= lowerBound - 1.0e-10,
            $"Terminal-only American convertible value {americanValue} is below discounted minimum conversion value {lowerBound}");
        Assert.True(
            Math.Abs(americanValue - europeanValue) <= 1.0e-10,
            $"Terminal-only American convertible value {americanValue} differs from European value {europeanValue}");
    }
}

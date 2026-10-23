// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The convertible bonds' public members that no vector or QuantLib case reaches, each against QuantLib's definition: a
/// bond's callability schedule, and the discretized convertible's mandatory times, its exercise, callability and coupon
/// times from the bond's settlement date on the risk-free curve's day counter, in that order
/// (<c>discretizedconvertible.hpp:55-64</c>).
/// </summary>
public class ConvertibleMemberTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly TARGET Calendar = new();

    [Fact]
    public void ABondsCallabilityIsItsSchedule()
    {
        (ConvertibleFixedCouponBond bond, ImmutableArray<Callability> callability, _) = Bond();
        Assert.Equal(callability, bond.Callability);
    }

    [Fact]
    public void TheMandatoryTimesAreTheExerciseCallabilityAndCouponTimes()
    {
        (ConvertibleFixedCouponBond bond, ImmutableArray<Callability> callability, Exercise exercise) = Bond();
        var dc = new Actual360();
        var process = new BlackScholesMertonProcess(
            50.0, new FlatForward(Today, 0.02, dc), new FlatForward(Today, 0.05, dc), new BlackConstantVol(Today, null, 0.15, dc));
        var asset = new DiscretizedConvertible(bond, process, [], 0.005, Context);

        Date settlement = bond.SettlementDate(Context);
        var expected = new List<double>();
        expected.AddRange(exercise.Dates.Select(d => dc.YearFraction(settlement, d)));
        expected.AddRange(callability.Select(c => dc.YearFraction(settlement, c.Date)));
        expected.AddRange(bond.Cashflows.SkipLast(1).Where(cf => cf.Date > settlement).Select(cf => dc.YearFraction(settlement, cf.Date)));
        Assert.Equal(expected, asset.MandatoryTimes());
    }

    // A two-year semiannual 4% convertible from two days after today, European, callable softly at 105 at one and a half
    // years and hard at 102 at a year.
    private static (ConvertibleFixedCouponBond Bond, ImmutableArray<Callability> Callability, Exercise Exercise) Bond()
    {
        Date issue = Calendar.Advance(Today, 2, TimeUnit.Days);
        Date maturity = Calendar.Advance(issue, 2, TimeUnit.Years);
        Schedule schedule = new MakeSchedule().From(issue).To(maturity).WithTenor(new Period(6, TimeUnit.Months))
                                              .WithCalendar(Calendar).Backwards().ToSchedule();
        ImmutableArray<Callability> callability =
        [
            new Callability(
                new BondPrice(102.0, BondPriceType.Clean), CallabilityType.Call, Calendar.Advance(issue, 1, TimeUnit.Years)),
            new SoftCallability(
                new BondPrice(105.0, BondPriceType.Clean), Calendar.Advance(issue, 18, TimeUnit.Months), 1.2),
        ];
        var exercise = new EuropeanExercise(maturity);
        var bond = new ConvertibleFixedCouponBond(exercise, 1.5, callability, issue, 2, [0.04], new Actual360(), schedule, 100.0);
        return (bond, callability, exercise);
    }
}

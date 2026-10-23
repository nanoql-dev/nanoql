// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A callable bond's live callabilities after one on or before the settlement date, of the other type. QuantLib 1.44
/// keeps the dates and prices of the live callabilities only (<c>callablebond.cpp:448-452</c>), but its tree applies
/// the i-th of them by the type of the i-th entry of the whole schedule
/// (<c>discretizedcallablefixedratebond.cpp:179</c>), so after a settled put a live call is applied as a put, and
/// after a settled call a live put as a call. QuantLib fixed it after v1.44 (issue #2871, PR #2875, commit
/// <c>20c2d3ac6</c>), keeping each live callability's type with its date and price, and nanoql fixes it as QuantLib
/// does. This is upstream's <c>testCallabilityTypeAfterSettledCallability</c>: on testCached's
/// bond, the price with the settled callability ahead equals the price without it, where QuantLib 1.44 prices the
/// settled put ahead of the calls at 106.0716945553609 for 102.76445466543099, and the settled call ahead of the puts
/// at 102.90405256617298 for 114.89193072303775. The class retires, and the case joins
/// <c>CallableBondsTests</c>, when the pin moves to a release with the fix.
/// </summary>
public class CallabilityTypeAfterSettledCallabilityTests
{
    [Fact]
    public void ASettledCallabilityLeavesTheLiveOnesTypesAlone()
    {
        // Testing callability types after a settled callability...

        var today = new Date(3, Month.June, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        var calendar = new TARGET();
        Date settlement = calendar.Advance(today, 3, TimeUnit.Days);

        YieldTermStructure termStructure = new FlatForward(settlement, 0.032, new Actual365Fixed());
        var model = new HullWhite(termStructure);

        Date issueDate = calendar.Adjust(today - 100);
        Date maturityDate = calendar.Advance(issueDate, 10, TimeUnit.Years);
        Schedule schedule = new MakeSchedule()
            .From(issueDate)
            .To(maturityDate)
            .WithCalendar(calendar)
            .WithFrequency(Frequency.Semiannual)
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .WithRule(DateGenerationRule.Backward)
            .ToSchedule();

        int timeSteps = 240;

        double Price(IEnumerable<Callability> exercises)
        {
            var bond = new CallableFixedRateBond(
                3, 10000.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis),
                BusinessDayConvention.ModifiedFollowing, 100.0, issueDate, exercises);
            return bond.CleanPrice(
                TreeCallableFixedRateBondEngine.Calculate(bond, context, model, timeSteps, termStructure), context);
        }

        double tolerance = 1e-10;

        // QuantLib 1.44's prices with the settled callability ahead, and without it
        var quantLib144 = new Dictionary<CallabilityType, (double WithSettled, double Without)>
        {
            [CallabilityType.Call] = (106.0716945553609, 102.76445466543099),
            [CallabilityType.Put] = (102.90405256617298, 114.89193072303775),
        };

        // QuantLib's BOOST_ERROR, which reports each scenario
        var errors = new List<string>();
        foreach (CallabilityType type in new[] { CallabilityType.Call, CallabilityType.Put })
        {
            CallabilityType settledType = (type == CallabilityType.Call) ? CallabilityType.Put : CallabilityType.Call;

            ImmutableArray<Callability> live =
            [
                .. new[] { 4, 8 }.Select(i => new Callability(
                    new BondPrice(100.0, BondPriceType.Clean), type, schedule.Date(i))),
            ];

            // a callability on the settlement date has already occurred
            ImmutableArray<Callability> exercises =
            [
                new Callability(new BondPrice(100.0, BondPriceType.Clean), settledType, settlement),
                .. live,
            ];

            double expected = Price(live);
            double calculated = Price(exercises);
            if (Math.Abs(calculated - expected) > tolerance)
            {
                errors.Add(
                    $"settled {(settledType == CallabilityType.Call ? "call" : "put")} changed the price of a bond "
                    + $"with later {(type == CallabilityType.Call ? "calls" : "puts")}: calculated {calculated}, "
                    + $"expected {expected}");
            }

            // QuantLib 1.44's reading moves it; the price without it is QuantLib's within the tolerance, the tree's
            // Ornstein-Uhlenbeck variance in its cancellation-free form (the S-sr plan's SSR3 class)
            Assert.True(Tolerance.Within(quantLib144[type].Without, expected), $"{expected}");
            if (calculated == quantLib144[type].WithSettled)
            {
                errors.Add($"QuantLib 1.44's price, {calculated}, for later {(type == CallabilityType.Call ? "calls" : "puts")}");
            }
        }

        Assert.Empty(errors);
    }
}

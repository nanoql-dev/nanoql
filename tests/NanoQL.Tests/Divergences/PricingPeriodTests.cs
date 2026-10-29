// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CommodityMarkets;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A daily schedule's periods counting both their ends (the S-xrt plan's XRR5 (iv), fixed under R8). QuantLib's
/// <c>createPricingPeriods</c> gives a period <c>[start, start + 1M − 1]</c> the quantity <c>perDay × (end −
/// start)</c>, one day short: 10 barrels a day from January to March 2025 makes 300, 270 and 300 barrels where the
/// months have 31, 28 and 31 days (the S-xrt planner's <c>xp_c1.cpp</c>). nanoql's is the daily quantity times the
/// period's days, FpML's <c>PerCalendarDay</c> ("the Quantity or Notional Quantity is deemed to apply once per Calendar
/// Day"): 310, 280 and 310. Check: each period's quantity is the sum of its days' quantities.
/// </summary>
public class PricingPeriodTests
{
    [Fact]
    public void ADailyPeriodsQuantityIsItsDaysTimesTheDailyQuantity()
    {
        var periods = CommodityPricingHelperFunctions.CreatePricingPeriods(
            new Date(1, Month.January, 2025),
            new Date(31, Month.March, 2025),
            new Quantity(None, new BarrelUnitOfMeasure(), 10.0),
            EnergyCommodityDeliverySchedule.Daily,
            EnergyCommodityQuantityPeriodicity.PerDay,
            new PaymentTerm("Pricing end + 5 days", PaymentTermEventType.PricingDate, 5, new NullCalendar()));
        Assert.Equal([310.0, 280.0, 310.0], periods.Select(p => p.Quantity.Amount).ToArray());
        foreach (PricingPeriod p in periods)
        {
            double sum = 0.0;
            for (Date d = p.StartDate; d <= p.EndDate; d++)
            {
                sum += 10.0;
            }

            Assert.Equal(sum, p.Quantity.Amount);
        }
    }
}

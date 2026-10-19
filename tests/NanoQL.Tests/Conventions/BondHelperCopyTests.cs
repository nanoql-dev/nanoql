// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A bond helper prices a plain copy of its bond, as QuantLib's copies it as its base class
/// (<c>make_shared&lt;Bond&gt;(*bond)</c>, <c>bondhelpers.cpp:33</c>): <see cref="BondHelper.Bond"/> is a
/// <see cref="Bond"/>, sharing the bond's cash flows, and a derived bond's overrides, such as an accrued amount of its
/// own, don't reach the helper's implied quote.
/// </summary>
public class BondHelperCopyTests
{
    [Fact]
    public void TheHelperPricesAPlainCopyOfTheBond()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var schedule = new Schedule(
            new Date(15, Month.March, 2024), new Date(15, Month.March, 2030), new Period(6, TimeUnit.Months),
            new TARGET(), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward, false, context);
        var bond = new RoundedAccrualBond(schedule);

        var helper = new BondHelper(100.0, bond, context);

        // A plain Bond, sharing the bond's cash flows and terms.
        Assert.Equal(typeof(Bond), helper.Bond.GetType());
        Assert.True(helper.Bond.Cashflows == bond.Cashflows);
        Assert.Equal(bond.SettlementDate(context), helper.Bond.SettlementDate(context));

        // The derived bond's accrued amount doesn't reach the helper's quote, the plain bond's clean price.
        BondResults results = DiscountingBondEngine.Calculate(bond, context, curve);
        double plain = results.SettlementValue!.Value - BondFunctions.AccruedAmount(bond, context);
        double implied = helper.SetTermStructure(curve).ImpliedQuote(context);
        Assert.Equal(plain, implied, 12);
        Assert.NotEqual(bond.CleanPrice(results, context), implied);

        // A fixed-rate helper holds a plain Bond too.
        var fixedHelper = new FixedRateBondHelper(
            100.0, 2, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis), context);
        Assert.Equal(typeof(Bond), fixedHelper.Bond.GetType());
    }

    // A fixed-rate bond whose accrued amount is rounded to two decimals, as some markets quote it.
    private sealed class RoundedAccrualBond(Schedule schedule) : FixedRateBond(
        2, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis))
    {
        public override double AccruedAmount(EvaluationContext context, Date? d = null) =>
            Math.Round(base.AccruedAmount(context, d), 2);
    }
}

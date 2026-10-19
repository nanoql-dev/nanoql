// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The bond functions call the bond's own accrued amount and notional where QuantLib's call the bond's virtual
/// <c>accruedAmount</c> and <c>notional</c> (<c>bondfunctions.cpp</c>), so a derived bond that overrides them, as later
/// ports' Italian bonds round their accrual, prices through its overrides: on a curve, from a yield, and back to a yield.
/// </summary>
public class BondFunctionsVirtualsTests
{
    [Fact]
    public void ADerivedBondsAccruedAmountAndNotionalReachTheBondFunctions()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var schedule = new Schedule(
            new Date(15, Month.March, 2024), new Date(15, Month.March, 2030), new Period(6, TimeUnit.Months),
            new TARGET(), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward, false, context);
        var plain = new FixedRateBond(2, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis));
        var derived = new ShiftedBond(schedule);

        // the functions' own accrued amount is the leg's per the bond's notional, as QuantLib's; the bond's own adds
        // its shift to that
        double accrued = BondFunctions.AccruedAmount(plain, context);
        Assert.Equal(accrued / 2.0, BondFunctions.AccruedAmount(derived, context), 1e-14);
        double derivedAccrued = derived.AccruedAmount(context);
        Assert.Equal((accrued / 2.0) + 0.25, derivedAccrued, 1e-14);

        // the notional doubled halves the dirty price, and the bond's own accrued amount gives the clean one
        double dirty = BondFunctions.DirtyPrice(plain, curve, context);
        Assert.Equal((dirty / 2.0) - derivedAccrued, BondFunctions.CleanPrice(derived, curve, context), 1e-12);

        var yield = new InterestRate(0.04, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual);
        double dirtyFromYield = BondFunctions.DirtyPrice(plain, yield, context);
        Assert.Equal((dirtyFromYield / 2.0) - derivedAccrued, BondFunctions.CleanPrice(derived, yield, context), 1e-12);

        // a clean price of the derived bond is the plain bond's whose dirty value matches it
        const double clean = 101.0;
        double plainClean = (2.0 * (clean + derivedAccrued)) - accrued;
        Assert.Equal(
            BondFunctions.Yield(
                plain, new BondPrice(plainClean, BondPriceType.Clean), new Actual365Fixed(), Compounding.Compounded,
                Frequency.Semiannual, context),
            BondFunctions.Yield(
                derived, new BondPrice(clean, BondPriceType.Clean), new Actual365Fixed(), Compounding.Compounded,
                Frequency.Semiannual, context),
            1e-9);
    }

    // A bond of one's own: a quarter more accrued, twice the notional.
    private sealed class ShiftedBond(Schedule schedule) : FixedRateBond(
        2, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis))
    {
        public override double AccruedAmount(EvaluationContext context, Date? d = null) =>
            base.AccruedAmount(context, d) + 0.25;

        public override double Notional(EvaluationContext context, Date? d = null) => 2.0 * base.Notional(context, d);
    }
}

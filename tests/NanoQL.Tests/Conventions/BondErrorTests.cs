// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The bonds' refusals by type, message and parameter: those of QuantLib's undefined paths (playbook §2), a value
/// already stored refused with <see cref="NanoQLException"/> and an argument with <see cref="ArgumentException"/>; a
/// null argument a member doesn't take with <see cref="ArgumentNullException"/>; and the bond functions' tradability
/// refusals, whose texts are QuantLib's, the curve-based dirty price's alone naming the settlement date; and a fitted
/// bond curve's method given fewer parameters than it fits, which QuantLib reads past.
/// </summary>
public class BondErrorTests
{
    private static readonly Date Today = new(16, Month.June, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheBondsRefusalsOfAnEmptyLegAndANullCashFlow()
    {
        NanoQLException notional = Assert.Throws<NanoQLException>(() => new Bond(2, new TARGET()).Notional(Context));
        Assert.Equal(typeof(NanoQLException), notional.GetType());
        Assert.Equal("no notional schedule: the bond has no cash flows", notional.Message);

        ImmutableArray<CashFlow> leg = [null!];
        ArgumentException coupons = Assert.Throws<ArgumentException>(() => new Bond(2, new TARGET(), null, leg));
        Assert.Equal(typeof(ArgumentException), coupons.GetType());
        Assert.Equal("coupons", coupons.ParamName);
        Assert.StartsWith("null cash flow in leg", coupons.Message, StringComparison.Ordinal);
        ArgumentException cashflows = Assert.Throws<ArgumentException>(() => new Bond(2, new TARGET(), 100.0, null, null, leg));
        Assert.Equal("cashflows", cashflows.ParamName);
        Assert.StartsWith("null cash flow in leg", cashflows.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFittingMethodsDiscountRefusesFewerParametersThanItFits()
    {
        // The discount function reads the method's parameters, past a shorter span's end in QuantLib.
        var method = new NelsonSiegelFitting();
        ArgumentException e = Assert.Throws<ArgumentException>(() => method.Discount([0.1, 0.2, 0.3], 1.0));
        Assert.Equal("x", e.ParamName);
        Assert.StartsWith("3 parameters given for a method of 4", e.Message, StringComparison.Ordinal);
        Assert.Equal(method.Discount([0.03, -0.01, 0.0, 1.0], 1.0), method.Discount([0.03, -0.01, 0.0, 1.0, 9.0], 1.0));
    }

    [Fact]
    public void TheBondsIssueDateRedemptionAndSettlementValueRefusals()
    {
        // the issue date against the first payment, in both public constructors (the vectors' "issue on the first
        // payment" cases)
        var date = new Date(18, Month.June, 2026);
        ImmutableArray<CashFlow> leg = [new SimpleCashFlow(100.0, date)];
        string text = $"issue date ({date}) must be earlier than first payment date ({date})";
        ArgumentException first = Assert.Throws<ArgumentException>(() => new Bond(2, new TARGET(), date, leg));
        Assert.Equal(typeof(ArgumentException), first.GetType());
        Assert.Equal("issueDate", first.ParamName);
        Assert.StartsWith(text, first.Message, StringComparison.Ordinal);
        ArgumentException second = Assert.Throws<ArgumentException>(() => new Bond(2, new TARGET(), 100.0, date, date, leg));
        Assert.Equal("issueDate", second.ParamName);
        Assert.StartsWith(text, second.Message, StringComparison.Ordinal);

        // a NaN face amount gives a redemption per notional step (the vectors' "NaN face amount" case)
        var schedule = new Schedule(
            new Date(18, Month.June, 2025), new Date(18, Month.June, 2027), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Backward, false, Context);
        NanoQLException fixedRate = Assert.Throws<NanoQLException>(
            () => new FixedRateBond(2, double.NaN, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis)));
        Assert.Equal(typeof(NanoQLException), fixedRate.GetType());
        Assert.Equal("multiple redemptions created", fixedRate.Message);
        var index = new Euribor6M(new FlatForward(Today, 0.03, new Actual365Fixed()));
        NanoQLException floating = Assert.Throws<NanoQLException>(
            () => new FloatingRateBond(2, double.NaN, schedule, index, new Actual360(), Context));
        Assert.Equal("multiple redemptions created", floating.Message);

        // the engine-reading prices of results with no settlement value
        var bond = new FixedRateBond(2, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis));
        NanoQLException settlement = Assert.Throws<NanoQLException>(() => bond.CleanPrice(new BondResults(), Context));
        Assert.Equal(typeof(NanoQLException), settlement.GetType());
        Assert.Equal("settlement value not provided", settlement.Message);
        Assert.Equal(
            "settlement value not provided", Assert.Throws<NanoQLException>(() => bond.DirtyPrice(new BondResults(), Context)).Message);
    }

    [Fact]
    public void TheBondHelpersRefusalsOfNoCashFlowsAndANullBond()
    {
        ArgumentException empty = Assert.Throws<ArgumentException>(
            () => new BondHelper(100.0, new Bond(2, new TARGET()), Context));
        Assert.Equal(typeof(ArgumentException), empty.GetType());
        Assert.Equal("bond", empty.ParamName);
        Assert.StartsWith("bond with no cashflows!", empty.Message, StringComparison.Ordinal);

        ArgumentNullException none = Assert.Throws<ArgumentNullException>(() => new BondHelper(100.0, null!, Context));
        Assert.Equal("bond", none.ParamName);
    }

    [Fact]
    public void TheSinkingNotionalsRefusals()
    {
        foreach (Period length in new[] { new Period(0, TimeUnit.Years), new Period(-1, TimeUnit.Years) })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => AmortizingFixedRateBondFunctions.SinkingNotionals(length, Frequency.Annual, 0.05, 100.0));
            Assert.Equal(typeof(ArgumentException), e.GetType());
            Assert.Equal("bondLength", e.ParamName);
            Assert.StartsWith("bond length must be positive", e.Message, StringComparison.Ordinal);
        }

        ArgumentException once = Assert.Throws<ArgumentException>(
            () => AmortizingFixedRateBondFunctions.SinkingNotionals(
                new Period(5, TimeUnit.Years), Frequency.Once, 0.05, 100.0));
        Assert.Equal(typeof(ArgumentException), once.GetType());
        Assert.Equal("frequency", once.ParamName);
        Assert.StartsWith("Bond frequency is incompatible with the maturity tenor", once.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFloatingRateBondRefusesANullIndexUnderItsOwnName()
    {
        var schedule = new Schedule(
            new Date(18, Month.June, 2025), new Date(18, Month.June, 2027), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Backward, false, Context);

        ArgumentNullException e = Assert.Throws<ArgumentNullException>(
            () => new FloatingRateBond(2, 100.0, schedule, null!, new Actual360(), Context));
        Assert.Equal("iborIndex", e.ParamName);
        Assert.StartsWith("no index provided", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTradabilityRefusalsKeepQuantLibsTexts()
    {
        var bond = new ZeroCouponBond(0, new TARGET(), 100.0, new Date(16, Month.June, 2026));
        var curve = new FlatForward(Today, 0.03, new Actual365Fixed());
        var late = new Date(1, Month.July, 2026);
        var yield = new InterestRate(0.03, new Actual365Fixed(), Compounding.Compounded, Frequency.Annual);

        // The curve-based dirty price, and the clean price through it, name the settlement date.
        foreach (Func<object> f in new Func<object>[]
        {
            () => BondFunctions.DirtyPrice(bond, curve, Context, late),
            () => BondFunctions.CleanPrice(bond, curve, Context, late),
        })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(f);
            Assert.Equal(typeof(ArgumentException), e.GetType());
            Assert.Equal("settlementDate", e.ParamName);
            Assert.StartsWith(
                "non tradable at July 1st, 2026 settlement date (maturity being June 16th, 2026)",
                e.Message,
                StringComparison.Ordinal);
        }

        // The others don't.
        foreach (Func<object> f in new Func<object>[]
        {
            () => BondFunctions.DirtyPrice(bond, yield, Context, late),
            () => BondFunctions.Bps(bond, curve, Context, late),
            () => BondFunctions.AccrualStartDate(bond, Context, late)!,
            () => BondFunctions.Yield(bond, new BondPrice(98.0, BondPriceType.Clean), new Actual365Fixed(),
                Compounding.Compounded, Frequency.Annual, Context, late),
            () => BondFunctions.ZSpread(bond, new BondPrice(98.0, BondPriceType.Clean), curve, Compounding.Continuous,
                Frequency.Annual, Context, late),
        })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(f);
            Assert.Equal(typeof(ArgumentException), e.GetType());
            Assert.Equal("settlementDate", e.ParamName);
            Assert.StartsWith(
                "non tradable at July 1st, 2026 (maturity being June 16th, 2026)", e.Message, StringComparison.Ordinal);
        }
    }
}

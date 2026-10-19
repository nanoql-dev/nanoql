// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The strippers', the stripped optionlet's and the adapter's refusals: their exception types and QuantLib's texts
/// (playbook §3.6), each read by the wheel (the cap survey's probe p40). An argument a constructor refuses is an
/// <see cref="ArgumentException"/>, a type outside its enum an <see cref="ArgumentOutOfRangeException"/>, an index past
/// the dates an <see cref="ArgumentOutOfRangeException"/> with QuantLib's text, and a strip that fails a
/// <see cref="NanoQLException"/>.
/// </summary>
public class StripperErrorTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, Month.June, 2025) };

    [Fact]
    public void TheStrippersRefusals()
    {
        CapFloorTermVolSurface surface = StripperFixture.Surface(Context);
        IborIndex index = StripperFixture.Index(Context);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new OptionletStripper1(surface, index, Context, type: VolatilityType.Normal, displacement: 0.01));
        Assert.Equal("displacement", e.ParamName);
        Assert.StartsWith("non-null displacement is not allowed with Normal model", e.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentOutOfRangeException>(() => new OptionletStripper1(surface, index, Context, type: (VolatilityType)7));

        CapFloorTermVolSurface shortSurface = CapFloorTermVolSurface.Create(
            0, new TARGET(), BusinessDayConvention.Following, [new Period(3, TimeUnit.Months), new Period(6, TimeUnit.Months)],
            StripperFixture.Strikes, new Numerics.Matrix(new[,] { { 0.30, 0.25, 0.22, 0.21 }, { 0.28, 0.24, 0.21, 0.20 } }),
            Context, new Actual365Fixed());
        e = Assert.Throws<ArgumentException>(() => new OptionletStripper1(shortSurface, index, Context));
        Assert.Equal("termVolSurface", e.ParamName);
        Assert.StartsWith("too short (6M) capfloor term vol termVolSurface", e.Message, StringComparison.Ordinal);

        // A 6M index on a 3M optionlet frequency: the first length, 6M, holds one coupon, the already fixed one.
        NanoQLException n = Assert.Throws<NanoQLException>(
            () => new OptionletStripper1(surface, index, Context, optionletFrequency: new Period(3, TimeUnit.Months)));
        Assert.Equal("cap/floor length 6M does not contain any optionlets", n.Message);

        // An index without a curve: its forecast fails first, with a discount curve given.
        n = Assert.Throws<NanoQLException>(
            () => new OptionletStripper1(surface, new Euribor6M(), Context, discount: index.ForwardingTermStructure));
        Assert.Equal("null term structure set to this instance of Euribor6M Actual/360", n.Message);

        // A failed implied volatility, without dontThrow: QuantLib's text, with the formula's, its streamed numbers in C#'s
        // formatting (QuantLib's prints the price -0.000110787 and the annuity 0.478599).
        double[,] bad =
        {
            { 0.60, 0.60, 0.60, 0.60 }, { 0.10, 0.10, 0.10, 0.10 }, { 0.10, 0.10, 0.10, 0.10 }, { 0.10, 0.10, 0.10, 0.10 },
        };
        n = Assert.Throws<NanoQLException>(() => StripperFixture.Strip(Context, bad));
        Assert.Equal(
            "could not bootstrap optionlet:\n type:    Put\n strike:  2.000000 %\n atm:     3.985031 %\n"
            + " price:   -0.00011078675654328422\n annuity: 0.478599163549783\n expiry:  June 16th, 2026\n"
            + " error:   option price (-0.00011078675654328422) must be non-negative",
            n.Message);

        OptionletStripper1 stripper = StripperFixture.Strip(Context);
        ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(() => stripper.OptionletVolatilities(9));
        Assert.StartsWith("index (9) must be less than optionletVolatilities size (9)", r.Message, StringComparison.Ordinal);
        r = Assert.Throws<ArgumentOutOfRangeException>(() => stripper.OptionletStrikes(9));
        Assert.StartsWith("index (9) must be less than optionletStrikes size (9)", r.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentNullException>(() => new OptionletStripper1(null!, index, Context));
        Assert.Throws<ArgumentNullException>(() => new OptionletStripper1(surface, null!, Context));
        Assert.Throws<ArgumentNullException>(() => new OptionletStripper1(surface, index, null!));
    }

    // The term-volatility curve's checks, QuantLib's checkInputs in its order (capfloortermvolcurve.cpp), as the
    // surface's are asserted (CapFloorErrorTests); the wheel's refused curves record their throws.
    [Fact]
    public void TheTermVolatilityCurvesRefusals()
    {
        Date today = Context.EvaluationDate;
        Period[] tenors = [new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        CapFloorTermVolCurve Curve(Period[] t, double[] v) =>
            new(today, new TARGET(), BusinessDayConvention.Following, t, v, new Actual365Fixed());
        AssertArgument("optionTenors", "empty option tenor vector", () => Curve([], []));
        AssertArgument("vols", "mismatch between number of option tenors (2) and number of volatilities (1)", () => Curve(tenors, [0.2]));
        AssertArgument("optionTenors", "negative first option tenor: 0D", () => Curve([new(0, TimeUnit.Days), new(1, TimeUnit.Years)], [0.2, 0.2]));
        AssertArgument("optionTenors", "non increasing option tenor: 1st is 1Y, 2nd is 1Y", () => Curve([new(1, TimeUnit.Years), new(1, TimeUnit.Years)], [0.2, 0.2]));
    }

    // OptionletStripper2's own refusals, which the wheel doesn't bind: their texts read from optionletstripper2.cpp,
    // and QuantLib's throws on these inputs in the harness's stripper 2 file. Not reached by any input: "empty cap/floor
    // leg", since the stripper's leg builder refuses an empty Ibor leg first and an overnight leg always holds a coupon.
    [Fact]
    public void TheSecondStrippersRefusals()
    {
        OptionletStripper1 stripper1 = StripperFixture.Strip(Context);
        CapFloorTermVolCurve AtmCurve(double vol, DayCounter dc) => CapFloorTermVolCurve.Create(
            0, new TARGET(), BusinessDayConvention.Following, StripperFixture.Tenors,
            Enumerable.Repeat(vol, StripperFixture.Tenors.Length), Context, dc);

        // A curve on another day counter than the surface's.
        AssertArgument(
            "atmCapFloorTermVolCurve",
            "different day counters provided",
            () => new OptionletStripper2(stripper1, AtmCurve(0.2, new Actual360()), Context));

        // An at-the-money volatility no spread up to 0.1 reaches.
        NanoQLException n = Assert.Throws<NanoQLException>(
            () => new OptionletStripper2(stripper1, AtmCurve(0.9, new Actual365Fixed()), Context));
        Assert.Equal("ATM cap/floor price for option expiry 1Y requires a volatility spread greater than 0.1", n.Message);

        // An at-the-money price below what the base volatilities less themselves give.
        n = Assert.Throws<NanoQLException>(
            () => new OptionletStripper2(stripper1, AtmCurve(1e-4, new Actual365Fixed()), Context));
        Assert.Equal("ATM cap/floor price for option expiry 2Y is below the minimum attainable price", n.Message);

        // An overnight at-the-money cap longer than the surface it strips.
        var sofr = new Sofr(new FlatForward(Context.EvaluationDate, 0.04, new Actual365Fixed()));
        var calendar = new UnitedStates(UnitedStatesMarket.FederalReserve);
        Period[] tenors = [new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        CapFloorTermVolSurface surface = CapFloorTermVolSurface.Create(
            2, calendar, BusinessDayConvention.ModifiedFollowing, tenors, [0.03, 0.04, 0.05], new Numerics.Matrix(2, 3, 0.003),
            Context, new Actual360());
        var sofrStripper = new OptionletStripper1(
            surface, sofr, Context, null, 1e-6, 100, sofr.ForwardingTermStructure, VolatilityType.Normal, 0.0, false,
            new Period(3, TimeUnit.Months), 0);
        CapFloorTermVolCurve longer = CapFloorTermVolCurve.Create(
            2, calendar, BusinessDayConvention.ModifiedFollowing, [new(1, TimeUnit.Years), new(3, TimeUnit.Years)], [0.003, 0.003],
            Context, new Actual360());
        AssertArgument(
            "atmCapFloorTermVolCurve",
            "ATM cap/floor for expiry 3Y extends beyond the source optionlet surface",
            () => new OptionletStripper2(sofrStripper, longer, Context));

        // On 28 October 2013, Euribor 6M on a flat 4% curve, TARGET, Following and Actual/365 (Fixed).
        var october = new EvaluationContext { EvaluationDate = new Date(28, Month.October, 2013) };
        var euribor = new Euribor6M(new FlatForward(october.EvaluationDate, 0.04, new Actual365Fixed()));
        CapFloorTermVolSurface OctoberSurface(Period[] t, double[] k, double[] row) => CapFloorTermVolSurface.Create(
            0, new TARGET(), BusinessDayConvention.Following, t, k,
            new Numerics.Matrix(t.Length, k.Length, [.. Enumerable.Range(0, t.Length).SelectMany(_ => row)]), october,
            new Actual365Fixed());
        CapFloorTermVolCurve OctoberCurve(Period[] t, double vol) => CapFloorTermVolCurve.Create(
            0, new TARGET(), BusinessDayConvention.Following, t, Enumerable.Repeat(vol, t.Length), october,
            new Actual365Fixed());

        // A front stub, a 9M cap on Euribor 6M, over a stripper with one optionlet: the stub's first coupon fixes before
        // the first optionlet date, which one node can't extrapolate to.
        var oneOptionlet = new OptionletStripper1(
            OctoberSurface([new(6, TimeUnit.Months), new(1, TimeUnit.Years)], [0.02, 0.03, 0.04, 0.05], [0.20, 0.20, 0.20, 0.20]),
            euribor,
            october);
        n = Assert.Throws<NanoQLException>(() => new OptionletStripper2(
            oneOptionlet, OctoberCurve([new(9, TimeUnit.Months), new(1, TimeUnit.Years)], 0.19), october));
        Assert.Equal(
            "cannot extrapolate an ATM cap/floor fixing before the first optionlet date from fewer than two optionlet nodes",
            n.Message);

        // A smile steep enough below the at-the-money rate that the adapter, extrapolating past the last strike, gives a
        // negative base volatility there.
        var steep = new OptionletStripper1(
            OctoberSurface([new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years)], [0.02, 0.03, 0.035], [0.40, 0.30, 0.10]),
            euribor,
            october);
        n = Assert.Throws<NanoQLException>(() => new OptionletStripper2(
            steep, OctoberCurve([new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years)], 0.20), october));
        Assert.Equal("negative optionlet volatility at ATM strike 0.039850314101546484", n.Message);
    }

    [Fact]
    public void TheAdaptersAndStrippedOptionletsRefusals()
    {
        // A stripper on a surface built on a fixed date has no settlement days, which QuantLib's adapter reads.
        var fixedSurface = new CapFloorTermVolSurface(
            Context.EvaluationDate, new TARGET(), BusinessDayConvention.Following, StripperFixture.Tenors,
            StripperFixture.Strikes, new Numerics.Matrix(StripperFixture.Vols), new Actual365Fixed());
        var stripper = new OptionletStripper1(fixedSurface, StripperFixture.Index(Context), Context);
        Assert.Null(stripper.SettlementDays);
        ArgumentException e = Assert.Throws<ArgumentException>(() => StrippedOptionletAdapter.Create(stripper, Context));
        Assert.Equal("stripper", e.ParamName);
        Assert.StartsWith("settlement days not provided for this instance", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => StrippedOptionletAdapter.Create(null!, Context));

        Date d1 = new(16, Month.December, 2025), d2 = new(16, Month.June, 2026);
        StrippedOptionlet Optionlet(Date[] dates, double[] strikes, double[][] vols) => new(
            2, new TARGET(), BusinessDayConvention.Following, StripperFixture.Index(Context), dates, strikes, vols,
            new Actual365Fixed(), Context);
        (Func<StrippedOptionlet> Build, string Param, string Text)[] refused =
        [
            (() => Optionlet([], [0.02, 0.03], []), "optionletDates", "empty optionlet tenor vector"),
            (() => Optionlet([d1, d2], [0.02, 0.03], [[0.2, 0.2]]), "v",
             "mismatch between number of option tenors (2) and number of volatility rows (1)"),
            (() => Optionlet([Context.EvaluationDate - 1, d2], [0.02, 0.03], [[0.2, 0.2], [0.2, 0.2]]), "optionletDates",
             "first option date (June 15th, 2025) is in the past"),
            (() => Optionlet([Context.EvaluationDate, d2], [0.02, 0.03], [[0.2, 0.2], [0.2, 0.2]]), "optionletDates",
             "first option date (June 16th, 2025) is in the past"),
            (() => Optionlet([d2, d1], [0.02, 0.03], [[0.2, 0.2], [0.2, 0.2]]), "optionletDates",
             "non increasing option dates: 1st is June 16th, 2026, 2nd is December 16th, 2025"),
            (() => Optionlet([d1, d2], [0.02, 0.03], [[0.2, 0.2], [0.2, 0.2, 0.2]]), "v",
             "mismatch between number of option tenors (2) and number of vol columns at date 1 (3"),
            (() => Optionlet([d1, d2], [0.03, 0.02], [[0.2, 0.2], [0.2, 0.2]]), "strikes",
             "non increasing strikes at date 0: 1st is 3.000000 %, 2nd is 2.000000 %"),
        ];
        foreach ((Func<StrippedOptionlet> build, string param, string text) in refused)
        {
            e = Assert.Throws<ArgumentException>(build);
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
        }

        // One strike, or one date, is refused by the adapter's linear interpolations, with QuantLib's text at its first
        // volatility.
        e = Assert.Throws<ArgumentException>(
            () => StrippedOptionletAdapter.Create(Optionlet([d1, d2], [0.03], [[0.2], [0.21]]), Context));
        Assert.Equal("stripper", e.ParamName);
        Assert.StartsWith("not enough points to interpolate: at least 2 required, 1 provided", e.Message, StringComparison.Ordinal);
    }

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}

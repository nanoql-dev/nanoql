// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An adapter on a stripped optionlet with no strikes. No check of QuantLib's refuses empty strikes
/// (<c>strippedoptionlet.cpp:84-115</c>), and its adapter, which builds its strike interpolations at its first
/// calculation, builds. That calculation refuses them: its linear interpolation over the first date's strikes
/// (<c>strippedoptionletadapter.cpp:109</c>) throws "not enough points to interpolate: at least 2 required, 0 provided"
/// (<c>interpolation.hpp:83-86</c>), the wheel's text at its first extrapolated volatility. But its
/// <c>minStrike()</c> and <c>maxStrike()</c> read the front and back of the empty strikes before any calculation
/// (<c>strippedoptionletadapter.cpp:128, 132</c>), which a volatility without extrapolation reaches through its strike
/// check: the wheel's process ends with an access violation, 0xC0000005, in 3 of 3 runs. nanoql's adapter does
/// QuantLib's first calculation when it is built (S7R4), so it refuses the empty strikes there, with QuantLib's text, as
/// an <see cref="ArgumentException"/> naming <c>stripper</c>, before the reads; and a base with no fixing dates, which
/// builds no interpolation, is refused by <c>MinStrike</c> and <c>MaxStrike</c> with <see cref="NanoQLException"/>,
/// "no strikes given" (S7D16).
/// </summary>
public class StrippedOptionletAdapterEmptyStrikesTests
{
    [Fact]
    public void EmptyStrikesAreRefused()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };
        var optionlet = new StrippedOptionlet(
            2, new TARGET(), BusinessDayConvention.Following, StripperFixture.Index(context),
            [new Date(16, Month.December, 2025), new Date(16, Month.June, 2026)], Array.Empty<double>(), [[], []],
            new Actual365Fixed(), context);

        ArgumentException e = Assert.Throws<ArgumentException>(() => StrippedOptionletAdapter.Create(optionlet, context));
        Assert.Equal("stripper", e.ParamName);
        Assert.StartsWith("not enough points to interpolate: at least 2 required, 0 provided", e.Message, StringComparison.Ordinal);

        StrippedOptionletAdapter adapter = StrippedOptionletAdapter.Create(new NoMaturities(), context);
        Assert.Equal("no strikes given", Assert.Throws<NanoQLException>(() => adapter.MinStrike()).Message);
        Assert.Equal("no strikes given", Assert.Throws<NanoQLException>(() => adapter.MaxStrike()).Message);
    }

    // A base with no fixing dates, whose first date's strikes are empty.
    private sealed class NoMaturities : StrippedOptionletBase
    {
        public override int OptionletMaturities => 0;

        public override ImmutableArray<Date> OptionletFixingDates => [];

        public override ImmutableArray<double> OptionletFixingTimes => [];

        public override DayCounter DayCounter => new Actual365Fixed();

        public override Calendar? Calendar => new TARGET();

        public override int? SettlementDays => 0;

        public override BusinessDayConvention BusinessDayConvention => BusinessDayConvention.Following;

        public override VolatilityType VolatilityType => VolatilityType.ShiftedLognormal;

        public override double Displacement => 0.0;

        public override ImmutableArray<double> OptionletStrikes(int i) => [];

        public override ImmutableArray<double> OptionletVolatilities(int i) => [];

        public override ImmutableArray<double> AtmOptionletRates(EvaluationContext context) => [];
    }
}

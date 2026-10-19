// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="SmileSection"/>'s protected parameterless constructor, for a section that overrides every inspector, as
/// one forwarding them to another section does: its fields are defined, with no exercise date, reference date or day
/// counter, an exercise time of 0, <see cref="VolatilityType.ShiftedLognormal"/> and a shift of 0; and the base's
/// prices read the inspectors a subclass overrides, so a forwarding section prices as the section it forwards to.
/// </summary>
public class SmileSectionConstructorTests
{
    [Fact]
    public void AParameterlessSectionHasDefinedFields()
    {
        var bare = new BareSection();
        Assert.Null(bare.ExerciseDate);
        Assert.Null(bare.DayCounter);
        Assert.Equal(0.0, bare.ExerciseTime);
        Assert.Equal(VolatilityType.ShiftedLognormal, bare.VolatilityType);
        Assert.Equal(0.0, bare.Shift);
        Assert.Equal(
            "referenceDate not available for this instance",
            Assert.Throws<NanoQLException>(() => bare.ReferenceDate()).Message);

        // A section forwarding every inspector to another: the base's members read the forwarded values.
        var reference = new Date(15, Month.January, 2025);
        var underlying = new FlatSmileSection(
            reference + 730, 0.25, new Actual365Fixed(), reference, 0.03, VolatilityType.ShiftedLognormal, 0.01);
        var forwarding = new ForwardingSection(underlying);
        Assert.Equal(underlying.ExerciseDate, forwarding.ExerciseDate);
        Assert.Equal(underlying.ReferenceDate(), forwarding.ReferenceDate());
        Assert.Equal(underlying.DayCounter, forwarding.DayCounter);
        foreach (double k in new[] { -0.01, 0.0, 0.02, 0.03, 0.05 })
        {
            Assert.Equal(underlying.Variance(k), forwarding.Variance(k));
            Assert.Equal(underlying.OptionPrice(k), forwarding.OptionPrice(k));
            Assert.Equal(underlying.OptionPrice(k, OptionType.Put, 0.97), forwarding.OptionPrice(k, OptionType.Put, 0.97));
            Assert.Equal(underlying.DigitalOptionPrice(k), forwarding.DigitalOptionPrice(k));
            Assert.Equal(underlying.Vega(k), forwarding.Vega(k));
            Assert.Equal(underlying.Density(k), forwarding.Density(k));
        }
    }

    // A section that overrides only what SmileSection leaves abstract, built by the parameterless constructor.
    private sealed class BareSection : SmileSection
    {
        public override double MinStrike() => 0.0;

        public override double MaxStrike() => 1.0;

        public override double? AtmLevel() => null;

        protected override double VolatilityImpl(double strike) => 0.2;
    }

    // A section forwarding every inspector to another, as QuantLib's spreaded and ATM-adjusted sections do.
    private sealed class ForwardingSection(SmileSection underlying) : SmileSection
    {
        public override Date? ExerciseDate => underlying.ExerciseDate;

        public override VolatilityType VolatilityType => underlying.VolatilityType;

        public override double Shift => underlying.Shift;

        public override double ExerciseTime => underlying.ExerciseTime;

        public override DayCounter? DayCounter => underlying.DayCounter;

        public override Date ReferenceDate() => underlying.ReferenceDate();

        public override double MinStrike() => underlying.MinStrike();

        public override double MaxStrike() => underlying.MaxStrike();

        public override double? AtmLevel() => underlying.AtmLevel();

        protected override double VolatilityImpl(double strike) => underlying.Volatility(strike);
    }
}

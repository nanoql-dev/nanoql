// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A smile section built by the parameterless constructor. QuantLib's <c>SmileSection() = default</c>
/// (<c>smilesection.hpp:50</c>) leaves the exercise time, the volatility type and the shift uninitialized
/// (<c>smilesection.hpp:86-92</c>), and the base's inspectors, its variance and its converted volatility read them
/// (<c>smilesection.hpp:60-67, 112-115</c>; <c>smilesection.cpp:133</c>), indeterminate unless the section overrides
/// them all, as QuantLib's own users of the constructor do. nanoql's parameterless constructor defines them: an exercise
/// time of 0, <see cref="VolatilityType.ShiftedLognormal"/> and a shift of 0, so a section that keeps the base's members
/// reads those values.
/// </summary>
public class SmileSectionFieldsTests
{
    [Fact]
    public void AParameterlessSectionReadsDefinedFields()
    {
        var s = new UninitializedSection();

        // The variance reads the exercise time: 0.
        Assert.Equal(0.0, s.Variance(0.03));

        // The converted volatility compares the type the section was built with, ShiftedLognormal, and the shift, 0: the
        // section's own volatility, with no conversion.
        Assert.Equal(0.25, s.Volatility(0.03, VolatilityType.ShiftedLognormal, 0.0));
        Assert.Equal(0.25, s.Volatility(0.03, VolatilityType.ShiftedLognormal, 1e-30));

        // Any other type or shift converts, through the ATM level, which this section doesn't have.
        Assert.Equal(
            "smile section must provide atm level to compute converted volatilties",
            Assert.Throws<NanoQLException>(() => s.Volatility(0.03, VolatilityType.Normal)).Message);
        Assert.Equal(
            "smile section must provide atm level to compute converted volatilties",
            Assert.Throws<NanoQLException>(() => s.Volatility(0.03, VolatilityType.ShiftedLognormal, 0.01)).Message);
    }

    // A section that overrides only what SmileSection leaves abstract, built by the parameterless constructor.
    private sealed class UninitializedSection : SmileSection
    {
        public override double MinStrike() => 0.0;

        public override double MaxStrike() => 1.0;

        public override double? AtmLevel() => null;

        protected override double VolatilityImpl(double strike) => 0.25;
    }
}

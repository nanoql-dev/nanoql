// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A spreaded swaption volatility's extrapolation flag is its base's, read once, when it is built, as QuantLib's
/// constructor reads it (<c>spreadedswaptionvol.cpp:30</c>); a structure is immutable, so a base's copy with another
/// flag is another base, and enabling the spreaded structure's own extrapolation leaves its base's flag as it was. Its
/// settlement days and maximum swap tenor, which the wheel doesn't bind, are its base's.
/// </summary>
public class SpreadedSwaptionVolatilityExtrapolationTests
{
    [Fact]
    public void TheFlagIsTheBasesWhenBuilt()
    {
        SwaptionVolatilityMatrix matrix = CubeFixture.AtmMatrix();
        Assert.False(new SpreadedSwaptionVolatility(matrix, 0.001).AllowsExtrapolation);

        SwaptionVolatilityMatrix extrapolating = matrix.EnableExtrapolation();
        var spreaded = new SpreadedSwaptionVolatility(extrapolating, 0.001);
        Assert.True(spreaded.AllowsExtrapolation);
        Assert.False(new SpreadedSwaptionVolatility(extrapolating.DisableExtrapolation(), 0.001).AllowsExtrapolation);
        Assert.True(spreaded.AllowsExtrapolation);

        SpreadedSwaptionVolatility own = new SpreadedSwaptionVolatility(matrix, 0.001).EnableExtrapolation();
        Assert.True(own.AllowsExtrapolation);
        Assert.False(matrix.AllowsExtrapolation);

        // A query past the base's range reads the base with extrapolation, as QuantLib's does: the flag decides only the
        // spreaded structure's own range check.
        Date past = own.OptionDateFromTenor(new Period(40, TimeUnit.Years));
        Assert.Equal(
            matrix.Volatility(past, new Period(40, TimeUnit.Years), 0.03, true) + 0.001,
            own.Volatility(past, new Period(40, TimeUnit.Years), 0.03));

        Assert.Equal(matrix.SettlementDays, spreaded.SettlementDays);
        Assert.Equal(matrix.MaxSwapTenor, spreaded.MaxSwapTenor);
        SwaptionVolatilityMatrix fixedDate = CubeFixture.AtmMatrix(new Date(18, Month.June, 2025));
        Assert.Null(new SpreadedSwaptionVolatility(fixedDate, 0.001).SettlementDays);
    }
}

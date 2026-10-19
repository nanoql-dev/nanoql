// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Le Floc'h and Kennedy's SABR guess on a non-finite argument. QuantLib computes on it (<c>sabr.cpp:289-385</c>): a
/// NaN or infinite volatility makes the smile's moments NaN, ν NaN, and ρ the sign of a NaN through
/// <c>boost::math::sign</c> (<c>sabr.cpp:332, 365</c>), which reads the NaN's sign bit, so it depends on which NaN an
/// operation keeps, and the compiler's choice and .NET's differ on some inputs. On strikes 3%, 3% and 4% around a
/// forward of 3.5%, with a NaN first volatility, the wheel gives α NaN, β 0.5, ν NaN and ρ 1; with an infinite third
/// volatility, α NaN, β 0.5, ν NaN and ρ −1. None of them is a guess a caller can use, so nanoql refuses a non-finite
/// argument with <see cref="ArgumentException"/> naming it.
/// </summary>
public class SabrGuessNonFiniteTests
{
    [Fact]
    public void ANonFiniteArgumentIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => SabrFunctions.SabrGuess(
            0.03, double.NaN, 0.03, 0.2, 0.04, 0.21, 0.035, 1.0, 0.5, 0.0, VolatilityType.ShiftedLognormal));
        Assert.Equal("vol_m", e.ParamName);
        Assert.StartsWith("vol_m must be finite: NaN not allowed", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => SabrFunctions.SabrGuess(
            0.03, 0.2, 0.035, 0.19, 0.04, double.PositiveInfinity, 0.035, 1.0, 0.5, 0.0, VolatilityType.ShiftedLognormal));
        Assert.Equal("vol_p", e.ParamName);
        Assert.StartsWith("vol_p must be finite: Infinity not allowed", e.Message, StringComparison.Ordinal);
    }
}

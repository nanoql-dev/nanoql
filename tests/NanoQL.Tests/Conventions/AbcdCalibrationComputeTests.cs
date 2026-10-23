// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using static NanoQL.Tests.Divergences.AbcdTermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The abcd calibration is immutable (the S-g1d plan's G1D9): <see cref="AbcdCalibration.Compute"/> returns the fitted
/// calibration, where QuantLib's <c>compute()</c> writes the fit into the object; the receiver keeps its guesses, its
/// errors theirs, and no end criteria.
/// </summary>
public class AbcdCalibrationComputeTests
{
    [Fact]
    public void ComputeReturnsTheFittedCopy()
    {
        var guess = new AbcdCalibration(T, Sets[0]);
        double error = guess.Error();

        AbcdCalibration fit = guess.Compute();

        Assert.Equal(new[] { -0.06, 0.17, 0.54, 0.17 }, new[] { guess.A, guess.B, guess.C, guess.D });
        Assert.Equal(EndCriteriaType.None, guess.EndCriteria);
        Assert.Equal(error, guess.Error());
        Assert.NotSame(guess, fit);
        Assert.Equal(EndCriteriaType.StationaryFunctionValue, fit.EndCriteria);
        Assert.True(fit.Error() < error);
    }
}

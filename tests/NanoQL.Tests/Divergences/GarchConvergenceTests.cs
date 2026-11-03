// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// GARCH's minimizer convergence (QuantLib's master, #2844, taken). QuantLib 1.44 reads the minimizer's point whatever
/// its end criterion (<c>garch.cpp:534-536</c>, "TODO: check return value from minimize()"), so a calibration stopped
/// by its iterations returns a point short of the optimum as calibrated. nanoql refuses it, "GARCH(1,1) calibration did
/// not converge: optimizer stopped with end criterion …", and the mode-based calibration catches the refusal and falls
/// back to its initial guess, as master does; QuantLib's own cases, whose dummy optimizer returns <c>None</c>, expect
/// that guess. Check: five Simplex iterations on QuantLib's calibration series, refused from a given guess, and from
/// the moment-matching guess its values, 0.265749, 0.156956 and 0.230964 (QuantLib's <c>testCalibration</c>).
/// </summary>
public class GarchConvergenceTests
{
    [Fact]
    public void AMinimizerStoppedByItsIterationsIsRefusedOrFallsBackToTheGuess()
    {
        (ImmutableArray<double> r2, _) = Garch11.ToR2(GarchSeries.Values);
        var fewIterations = new EndCriteria(5, 3, 1e-8, 1e-8, 1e-8);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => Garch11.CalibrateR2(r2.AsSpan(), new Simplex(0.001), fewIterations, [0.2, 0.2, 0.2]));
        Assert.Equal("GARCH(1,1) calibration did not converge: optimizer stopped with end criterion MaxIterations", e.Message);

        Garch11 guess = new Garch11(GarchSeries.Returns, Garch11Mode.MomentMatchingGuess).Calibrate(GarchSeries.Returns, new Simplex(0.001), fewIterations);
        Assert.Equal(0.265749, guess.Alpha, 1e-6);
        Assert.Equal(0.156956, guess.Beta, 1e-6);
        Assert.Equal(0.230964, guess.Omega, 1e-6);
    }
}

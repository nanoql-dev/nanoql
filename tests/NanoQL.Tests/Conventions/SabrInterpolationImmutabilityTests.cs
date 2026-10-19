// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A SABR interpolation is immutable once built: its fit writes its parameters only while it is built, so reading it
/// many times, at strikes inside and outside its range, leaves its parameters, errors and weights as they were; its
/// weights are an immutable array; and it copied its inputs, so changing the caller's arrays afterwards changes nothing.
/// </summary>
public class SabrInterpolationImmutabilityTests
{
    [Fact]
    public void NothingWritesTheFitAfterConstruction()
    {
        double[] strikes = [0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06];
        double[] vols = [0.36, 0.31, 0.27, 0.25, 0.24, 0.245, 0.26];
        var s = new SABRInterpolation(
            strikes, vols, 2.0, 0.035, null, 0.5, null, null, false, true, false, false, true, null, new Simplex(0.01));
        (double, double, double, double, double, double) before = (s.Alpha, s.Beta, s.Nu, s.Rho, s.RmsError, s.MaxError);
        double[] weightsBefore = [.. s.InterpolationWeights()];
        double[] valuesBefore = [.. strikes.Select(k => s.Evaluate(k))];

        for (int i = 0; i < 2000; i++)
        {
            _ = s.Evaluate(0.01 + (0.0001 * i), true);
        }

        strikes[3] = 0.1;
        vols[3] = 0.9;

        Assert.Equal(before, (s.Alpha, s.Beta, s.Nu, s.Rho, s.RmsError, s.MaxError));
        Assert.Equal(weightsBefore, s.InterpolationWeights());
        double[] valuesAfter = [.. new double[] { 0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06 }.Select(k => s.Evaluate(k))];
        Assert.Equal(valuesBefore, valuesAfter);
        Assert.Equal(0.035, s.XValues()[3]);
        Assert.Equal(0.25, s.YValues()[3]);
    }
}

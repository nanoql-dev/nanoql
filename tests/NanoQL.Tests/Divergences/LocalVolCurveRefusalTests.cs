// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="LocalVolCurve"/> on a Black variance that falls in time. QuantLib returns the square root of the negative
/// derivative, NaN (<c>localvolcurve.hpp:88-95</c>): a curve of 30% at six months and 10% at a year, built without the
/// forced monotone variance, gives a process diffusion of NaN at t = 0.8. nanoql refuses with
/// <see cref="NanoQLException"/>, "negative local vol^2 at time …; the black variance curve is not smooth enough", as
/// QuantLib's <c>LocalVolSurface</c> refuses the same condition (<c>localvolsurface.cpp:148-151</c>).
/// </summary>
public class LocalVolCurveRefusalTests
{
    [Fact]
    public void AFallingVarianceIsRefused()
    {
        var today = new Date(15, Month.October, 2026);
        var curve = new BlackVarianceCurve(today, [today + 182, today + 365], [0.30, 0.10], new Actual365Fixed(), false);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, new Actual365Fixed()), new FlatForward(today, 0.04, new Actual365Fixed()), curve);

        Assert.IsType<LocalVolCurve>(process.LocalVolatility);
        NanoQLException e = Assert.Throws<NanoQLException>(() => process.Diffusion(0.8, 100.0));
        Assert.StartsWith("negative local vol^2 at time 0.8; the black variance curve is not smooth enough", e.Message, StringComparison.Ordinal);
    }
}

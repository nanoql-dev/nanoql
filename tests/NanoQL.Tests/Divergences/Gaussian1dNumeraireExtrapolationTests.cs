// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The numeraire's read of a given curve (the S-g1d plan's G1R6 (i)): GSR's numeraire at t = 0 reads a curve it is
/// given at the numeraire time without extrapolation (<c>gsr.cpp:216-220</c>), where it reads its own with it, so on a
/// 30-year curve QuantLib's <c>numeraire(0, 0, yts)</c> throws "time (60) is past max curve time (30.0192)", and so
/// does every Gaussian1d engine given that discount curve. nanoql reads it with extrapolation. QuantLib's check: its
/// own route, the model built on that curve, whose numeraire at 0 is 0.1319979111949979, and at (1.5, 0.3)
/// 0.1358278427215987, the value QuantLib's given-curve read gives there too.
/// </summary>
public class Gaussian1dNumeraireExtrapolationTests
{
    [Fact]
    public void AGivenCurveIsReadWithExtrapolation()
    {
        var today = new Date(15, Month.January, 2025);
        YieldTermStructure flat = new FlatForward(today, 0.03, new Actual365Fixed());
        Date[] dates = [today, today + new Period(1, TimeUnit.Years), today + new Period(5, TimeUnit.Years), today + new Period(10, TimeUnit.Years), today + new Period(30, TimeUnit.Years)];
        YieldTermStructure curve = new InterpolatedZeroCurve<Numerics.Linear>(dates, [0.03, 0.03, 0.031, 0.032, 0.033], new Actual365Fixed());
        Gsr model = Gaussian1dInputs.Constant(flat, 0.01, 0.01);

        Assert.True(Math.Abs((model.Numeraire(0.0, 0.0, curve) / 0.1319979111949979) - 1.0) <= 1e-15, $"{model.Numeraire(0.0, 0.0, curve)}");
        Assert.True(Math.Abs((model.Numeraire(1.5, 0.3, curve) / 0.1358278427215987) - 1.0) <= 1e-14, $"{model.Numeraire(1.5, 0.3, curve)}");

        // QuantLib's own route: the model on the curve.
        Gsr onCurve = Gaussian1dInputs.Constant(curve, 0.01, 0.01);
        Assert.Equal(onCurve.Numeraire(0.0, 0.0), model.Numeraire(0.0, 0.0, curve));
    }
}

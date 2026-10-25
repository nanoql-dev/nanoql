// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The no-except local volatility surface answers its overwrite where Dupire's surface refuses its reads with a
/// <see cref="NanoQLException"/>, QuantLib's <c>Error</c> (the S-van plan's VD21), and is Dupire's value elsewhere; the
/// structure's own range check, before the read, isn't caught.
/// </summary>
public class NoExceptLocalVolOverwriteTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void WhereDupireRefusesItIsTheOverwrite()
    {
        // a variance falling in time: Dupire's surface refuses with "decreasing variance at strike"
        var falling = new BlackVarianceCurve(Today, [Today + 182, Today + 365], [0.40, 0.20], Dc, false);
        var r = new FlatForward(Today, 0.03, Dc);
        var q = new FlatForward(Today, 0.01, Dc);
        var dupire = new LocalVolSurface(falling, r, q, 100.0);
        var noExcept = new NoExceptLocalVolSurface(falling, r, q, 100.0, 0.33);

        Assert.Throws<NanoQLException>(() => dupire.LocalVol(0.75, 100.0, true));
        Assert.Equal(0.33, noExcept.LocalVol(0.75, 100.0, true));

        // where Dupire's surface answers, the same bits
        Assert.Equal(dupire.LocalVol(0.3, 100.0, true), noExcept.LocalVol(0.3, 100.0, true));
    }

    [Fact]
    public void TheRangeCheckIsntCaught()
    {
        var surface = new BlackVarianceSurface(
            Today, null, [Today + 182, Today + 365], [80.0, 100.0, 120.0], new Matrix(3, 2, 0.2), Dc);
        var noExcept = new NoExceptLocalVolSurface(surface, new FlatForward(Today, 0.03, Dc), new FlatForward(Today, 0.01, Dc), 100.0, 0.33);

        // past the maximum date, without extrapolation
        Assert.Throws<NanoQLException>(() => noExcept.LocalVol(2.0, 100.0));
    }
}

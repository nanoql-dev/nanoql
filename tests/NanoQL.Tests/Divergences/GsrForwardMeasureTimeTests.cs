// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// GSR's forward measure time (QuantLib's #2842, taken ahead of the pin): QuantLib 1.44's
/// <c>GsrProcess::setForwardMeasureTime</c> moves the process's time and leaves its core's
/// (<c>gsrprocess.hpp:85-88</c>), so a process on step times 1 and 2, volatilities 0.01, 0.012 and 0.009 and reversion
/// 0.03, built at T = 10 and moved to 20, still gives the expectation E(x(1.5) | x(0) = 0) of T = 10,
/// −0.001238869348433307, where a process built at 20 gives −0.002344324730472735, and its drift at (1, 0) mixes the
/// two times, −0.0010387193164901856 against −0.001988418783549303; and it accepts a time below the last step time
/// (1.5). QuantLib's master after the pin moves the core with the process and refuses such a time with its text,
/// "forward measure time (1.5) must be greater than or equal to the last vol/reversion pillar time (2)", as nanoql's
/// <c>WithForwardMeasureTime</c> and <c>Gsr.WithNumeraireTime</c> do. QuantLib's check: its process built at the new
/// time.
/// </summary>
public class GsrForwardMeasureTimeTests
{
    private static readonly GsrProcess Process = new([1.0, 2.0], [0.01, 0.012, 0.009], [0.03], 10.0);

    [Fact]
    public void TheCopyMovesTheCoresTime()
    {
        GsrProcess moved = Process.WithForwardMeasureTime(20.0);
        var built = new GsrProcess([1.0, 2.0], [0.01, 0.012, 0.009], [0.03], 20.0);
        Assert.Equal(built.Expectation(0.0, 0.0, 1.5), moved.Expectation(0.0, 0.0, 1.5));
        Assert.Equal(built.Drift(1.0, 0.0), moved.Drift(1.0, 0.0));
        Assert.True(Math.Abs((moved.Expectation(0.0, 0.0, 1.5) / -0.002344324730472735) - 1.0) <= 1e-14);

        // QuantLib 1.44's moved process, at its old core's time.
        Assert.True(Math.Abs(moved.Expectation(0.0, 0.0, 1.5) - -0.001238869348433307) > 1e-3);
    }

    [Fact]
    public void ATimeBelowTheLastStepTimeIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => Process.WithForwardMeasureTime(1.5));
        Assert.StartsWith("forward measure time (1.5) must be greater than or equal to the last vol/reversion pillar time (2)", e.Message, StringComparison.Ordinal);
        Assert.Equal("forwardMeasureTime", e.ParamName);

        var today = new Date(15, Month.January, 2025);
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var model = new Gsr(curve, [today + new Period(2, TimeUnit.Years)], [0.01, 0.01], 0.03);
        var m = Assert.Throws<ArgumentException>(() => model.WithNumeraireTime(1.0));
        Assert.StartsWith("forward measure time (1) must be greater than or equal to the last vol/reversion pillar time (2", m.Message, StringComparison.Ordinal);
        Assert.Equal("T", m.ParamName);
    }
}

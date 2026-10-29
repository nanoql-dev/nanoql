// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Tests.Divergences;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The CLV models' conventions (the S-xrt plan's XRR5 (xxiii), (xli) and XRR14): the square-root mapping refuses a time
/// past its last maturity with QuantLib's text, before the read QuantLib makes of its map's end; the normal mapping
/// refuses a time that isn't positive, where QuantLib's standardized value is 0/0 at x₀ and infinite elsewhere; and the
/// square-root rescale is QuantLib's on a top-only clamp, as at every date of QuantLib's two cases, the points scaled
/// toward zero by a = (q(pMax) − x.front())/(x.back() − x.front()).
/// </summary>
public class CLVModelTests
{
    [Fact]
    public void TheSquareRootMappingRefusesPastItsMaturities()
    {
        var today = new Date(16, Month.October, 2016);
        DayCounter dc = new Actual365Fixed();
        Date[] dates = [today + new Period(3, TimeUnit.Months), today + new Period(6, TimeUnit.Months)];
        var model = new SquareRootCLVModel(
            SquareRootCLVRescaleTests.Bs(today, dc), new SquareRootProcess(0.09, 1.0, 0.2, 0.09), dates, 6);
        Func<double, double, double> g = model.G();
        double t0 = dc.YearFraction(today, dates[0]);
        double t1 = dc.YearFraction(today, dates[1]);
        Assert.True(double.IsFinite(g(t0, 0.1)));
        Assert.True(double.IsFinite(g(0.5 * (t0 + t1), 0.1)));
        Assert.True(double.IsFinite(g(t1, 0.1)));
        foreach (double t in new[] { t1 + 0.01, 0.5 * t0 })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => g(t, 0.1));
            Assert.Equal("extrapolation to large or small t is not allowed", e.Message);
        }
    }

    [Fact]
    public void TheNormalMappingRefusesATimeThatIsntPositive()
    {
        var today = new Date(22, Month.June, 2016);
        DayCounter dc = new Actual365Fixed();
        var bs = new GeneralizedBlackScholesProcess(
            100.0, new FlatForward(today, 0.04, dc), new FlatForward(today, 0.1, dc), new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
        var ou = new OrnsteinUhlenbeckProcess(2.3, 0.35, 100.0, 100.0);
        var model = new NormalCLVModel(bs, ou, [today + new Period(6, TimeUnit.Months), today + new Period(1, TimeUnit.Years)], 8);
        Func<double, double, double> g = model.G();
        foreach (double t in new[] { 0.0, -0.1 })
        {
            Assert.Throws<NanoQLException>(() => g(t, 100.0));
        }

        // a path's first step, QuantLib's own case's first read
        Assert.True(double.IsFinite(g(1.0 / 365.0, 100.0)));
    }

    [Fact]
    public void ATopOnlyClampScalesThePointsTowardZero()
    {
        // QuantLib's vanilla case: pMax 1 − 1e-14 clamps the top, pMin 1e-14 doesn't clamp the bottom, so b is 0
        (SquareRootCLVModel model, Date maturity, double df, double ncp) = SquareRootCLVRescaleTests.Vanilla(1 - 1e-14, 1e-14);
        Vector x = model.CollocationPointsX(maturity);
        double[] nodes = SquareRootCLVRescaleTests.SortedNodes(df, ncp);
        double a = x[x.Size - 1] / nodes[^1];
        Assert.True(a < 1.0);
        for (int i = 0; i < nodes.Length; i++)
        {
            Assert.True(Math.Abs(x[i] - (a * nodes[i])) <= 1e-14 * x[i], $"point {i}: {x[i]} for {a * nodes[i]}");
        }
    }
}

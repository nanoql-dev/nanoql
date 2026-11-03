// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The square-root CLV model's collocation points raised to the pMin quantile where QuantLib's rescale puts them below
/// it. QuantLib maps the Gauss nodes by a·x + (xMin − x.front()), which keeps them inside [q(pMin), q(pMax)] at every
/// date of QuantLib's two cases but breaks the lower bound the method states wherever pMin's quantile exceeds the map's
/// lowest point: on the vanilla case's nodes at pMin 1e-3 and pMax 0.999 the lowest point falls at probability 6.5e-5,
/// at pMin 1e-2 at 1.4e-3, and on the mapping case's year date at pMin 1e-3 at 3.5e-4 with no low clamp. nanoql's map
/// is QuantLib's raised by the shortfall: its lowest point at the pMin quantile, the affine map onto [q(pMin), q(pMax)]
/// where the low bound clamps, continuous in pMin.
/// </summary>
public class SquareRootCLVRescaleTests
{
    [Theory]
    [InlineData(1e-3)]
    [InlineData(1e-2)]
    public void ALowClampMapsOntoTheQuantileBounds(double pMin)
    {
        (SquareRootCLVModel model, Date maturity, double df, double ncp) = Vanilla(0.999, pMin);
        Vector x = model.CollocationPointsX(maturity);
        var cdf = new NonCentralCumulativeChiSquareDistribution(df, ncp);
        Assert.True(Math.Abs(cdf.Evaluate(x[0]) - pMin) <= 1e-7 * pMin, $"{cdf.Evaluate(x[0])}");
        Assert.True(Math.Abs(cdf.Evaluate(x[x.Size - 1]) - 0.999) <= 1e-9, $"{cdf.Evaluate(x[x.Size - 1])}");

        // the affine map: the points' spacing a constant multiple of the nodes'
        double[] nodes = SortedNodes(df, ncp);
        double a = (x[x.Size - 1] - x[0]) / (nodes[^1] - nodes[0]);
        for (int i = 0; i < nodes.Length; i++)
        {
            Assert.True(Math.Abs(x[i] - (x[0] + (a * (nodes[i] - nodes[0])))) <= 1e-12 * x[x.Size - 1]);
        }
    }

    [Fact]
    public void TheBandRaisesTheLowestPointToTheQuantile()
    {
        // the mapping case's year date at pMin 1e-3: the top clamps, the low bound doesn't, QuantLib's lowest point at
        // probability 3.5e-4
        var today = new Date(16, Month.October, 2016);
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + new Period(365, TimeUnit.Days);
        var model = new SquareRootCLVModel(Bs(today, dc), new SquareRootProcess(0.09, 1.0, 0.2, 0.09), [maturity], 14, 1 - 1e-10, 1e-3);
        double t = dc.YearFraction(today, maturity);
        (double df, double ncp) = Params(0.09, 1.0, 0.2, 0.09, t);
        Vector x = model.CollocationPointsX(maturity);
        var cdf = new NonCentralCumulativeChiSquareDistribution(df, ncp);
        Assert.True(Math.Abs(cdf.Evaluate(x[0]) - 1e-3) <= 1e-10, $"{cdf.Evaluate(x[0])}");
        Assert.True(cdf.Evaluate(x[x.Size - 1]) < 1 - 1e-10);
    }

    [Fact]
    public void TheMapIsContinuousInPMin()
    {
        // across the band's threshold, where the raise starts, and the low clamp's, where the lowest node is passed
        double previous = double.NaN;
        foreach (double pMin in new[] { 1e-6, 3e-6, 1e-5, 3e-5, 1e-4, 3e-4, 1e-3 })
        {
            (SquareRootCLVModel model, Date maturity, double _, double _) = Vanilla(1 - 1e-14, pMin);
            double lowest = model.CollocationPointsX(maturity)[0];
            Assert.True(double.IsNaN(previous) || lowest >= previous - 1e-12);
            previous = lowest;
        }
    }

    // QuantLib's vanilla case's model and maturity at the given bounds, its kernel's parameters there.
    internal static (SquareRootCLVModel Model, Date Maturity, double Df, double Ncp) Vanilla(double pMax, double pMin)
    {
        var today = new Date(5, Month.October, 2016);
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        Date maturity = today + new Period(3, TimeUnit.Months);
        var model = new SquareRootCLVModel(Bs(today, dc), new SquareRootProcess(0.06, 1.0, 0.2, 0.09), [maturity], 14, pMax, pMin);
        (double df, double ncp) = Params(0.06, 1.0, 0.2, 0.09, dc.YearFraction(today, maturity));
        return (model, maturity, df, ncp);
    }

    internal static GeneralizedBlackScholesProcess Bs(Date today, DayCounter dc) => new(
        100.0, new FlatForward(today, 0.03, dc), new FlatForward(today, 0.08, dc), new BlackConstantVol(today, new NullCalendar(), 0.3, dc));

    internal static (double Df, double Ncp) Params(double theta, double kappa, double sigma, double x0, double t) =>
        (4 * theta * kappa / (sigma * sigma), 4 * kappa * Math.Exp(-kappa * t) / (sigma * sigma * (1 - Math.Exp(-kappa * t))) * x0);

    internal static double[] SortedNodes(double df, double ncp)
    {
        double[] x = [.. new GaussianQuadrature(14, new GaussNonCentralChiSquaredPolynomial(df, ncp)).X()];
        Array.Sort(x);
        return x;
    }
}

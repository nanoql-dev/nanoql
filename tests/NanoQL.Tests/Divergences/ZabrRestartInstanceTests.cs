// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The model a ZABR interpolation prices with after its restarts. QuantLib's model is a smile section built from a copy
/// of the parameters (<c>zabrinterpolation.hpp:110-115</c>), rebuilt at each evaluation of the cost function
/// (<c>xabrinterpolation.hpp:292-307</c>); after the restarts the fit writes the best restart's parameters back and
/// doesn't rebuild it (<c>:230-234</c>). So where the restarts run out without an accepted fit, or Simplex's last trial
/// isn't the vertex it returns, the interpolation's values and its <c>rmsError()</c> and <c>maxError()</c> are its
/// last trial's, while <c>alpha()</c>… are the best restart's: the C++ harness's fits below. nanoql rebuilds the
/// section from the best parameters, so its values and errors are those of QuantLib's section built at the parameters
/// it reports. A cube whose fits' restarts run out records its last trial's errors beside its best parameters, and
/// refuses them against its tolerance where nanoql's cube records its best parameters' errors and builds.
/// <para>
/// Each fit's claim is asserted in the same run, bit for bit: its values and errors are its own section's at the
/// parameters it reports. QuantLib's numbers are asserted within bounds above what one-ulp moves of the five
/// volatilities, together and each alone, change: the maximum-error fit's by up to 1.5e-11, within the vectors'
/// tolerance; the Simplex fit's parameters by up to 1.6e-9 and its values by 2.9e-10, within 1e-8, both in the
/// stability measure at scale 1.
/// </para>
/// </summary>
public class ZabrRestartInstanceTests
{
    private static readonly double[] Strikes = [0.02, 0.025, 0.03, 0.035, 0.04];
    private static readonly double[] Volatilities = [0.25, 0.22, 0.2, 0.19, 0.195];

    // The Simplex fit's bound in the stability measure, above its measured one-ulp moves (1.6e-9).
    private const double SimplexBound = 1e-8;

    [Fact]
    public void AFitWhoseRestartsRunOutPricesWithItsBestParameters()
    {
        // Scored by the maximum error, no restart reaches errorAccept, so all 50 run.
        var fit = new ZabrInterpolation<ZabrShortMaturityLognormal>(
            Strikes, Volatilities, 1.0, 0.03, null, 0.5, null, null, 1.0, false, true, false, false, true, useMaxError: true);

        // its values and errors are its section's at the parameters it reports
        AssertOwnSection(fit);

        // the best restart's parameters, as QuantLib reports them, and QuantLib's section at them, with its errors
        AssertWithin(0.03455095003835179, fit.Alpha, Tolerance.Relative);
        AssertWithin(0.466821650535032, fit.Nu, Tolerance.Relative);
        AssertWithin(-0.1196508298165791, fit.Rho, Tolerance.Relative);
        double[] values = [0.25206185682468246, 0.2189237026987793, 0.1994800030539971, 0.19206074917423763, 0.19279740677253487];
        for (int i = 0; i < Strikes.Length; i++)
        {
            AssertWithin(values[i], fit.Evaluate(Strikes[i]), Tolerance.Relative);
        }

        AssertWithin(0.00171547913238195, fit.RmsError, Tolerance.Relative);
        AssertWithin(0.0022025932274651328, fit.MaxError, Tolerance.Relative);

        // QuantLib's interpolation's, its last trial's: values [0.2266966997609207, 0.21469345101993662,
        // 0.2052017639335689, 0.19739835808066433, 0.19080287191092665], rmsError 0.009378955675378877, maxError
        // 0.02330330023907931
        Assert.NotEqual(0.02330330023907931, fit.MaxError);
    }

    [Fact]
    public void ASimplexFitPricesWithTheVertexItReturns()
    {
        // accepted at the first minimization, whose last trial isn't the vertex Simplex returns
        var fit = new ZabrInterpolation<ZabrShortMaturityLognormal>(
            Strikes, Volatilities, 1.0, 0.03, null, 0.5, null, null, 1.0, false, true, false, false, true, optMethod: new Simplex(0.01));

        Assert.Equal(EndCriteriaType.StationaryPoint, fit.EndCriteria);

        // its values and errors are its section's at the parameters it reports
        AssertOwnSection(fit);

        // QuantLib's parameters, and its section at them, with its errors
        AssertWithin(0.03455095066552853, fit.Alpha, SimplexBound);
        AssertWithin(0.4668214901278758, fit.Nu, SimplexBound);
        AssertWithin(-0.11965093697259457, fit.Rho, SimplexBound);
        double[] values = [0.25206185265416897, 0.2189237054761371, 0.1994800066750037, 0.19206074721197, 0.19279739645520494];
        for (int i = 0; i < Strikes.Length; i++)
        {
            AssertWithin(values[i], fit.Evaluate(Strikes[i]), SimplexBound);
        }

        AssertWithin(0.001715479132373924, fit.RmsError, SimplexBound);
        AssertWithin(0.0022026035447950676, fit.MaxError, SimplexBound);

        // QuantLib's interpolation's, its last trial's: values [0.2520618526720326, 0.2189237054404919,
        // 0.19948000664367976, 0.19206074724307642, 0.19279739657074688], rmsError 0.001715479132374018, maxError
        // 0.0022026034292531305
        Assert.NotEqual(0.0022026034292531305, fit.MaxError);
    }

    [Fact]
    public void ACubeWhoseRestartsRunOutRecordsItsBestParametersErrors()
    {
        // The fixture's cube, β and γ fixed, not at-the-money calibrated, its restarts forced to run out. On the wheel
        // QuantLib's refuses it: "global swaptions calibration failed: error tolerance exceeded: using rmsError tolerance
        // 0.01, option maturity = June 16th, 2026, swap tenor = 10Y, rms error = 1.814232 %, max error = 2.156702 %,
        // alpha = 0.0332691 beta = 0.5 nu = 0.535233 rho = -0.0971677 gamma = 1.000000", the last trial's errors beside
        // the best restart's parameters.
        var cube = new ZabrSwaptionVolatilityCube(
            CubeFixture.AtmMatrix(),
            CubeFixture.Options,
            CubeFixture.Swaps,
            CubeFixture.StrikeSpreads,
            CubeFixture.VolSpreads,
            CubeFixture.Index(2),
            CubeFixture.Index(1),
            false,
            Enumerable.Range(0, 9).Select(_ => new[] { 0.2, 0.5, 0.4, 0.0, 1.0 }),
            [false, true, false, false, true],
            false,
            CubeFixture.Context,
            errorAccept: 1e-12,
            maxGuesses: 5);

        // nanoql's: the 1Y x 10Y node's parameters, QuantLib's printed ones, and its section's errors at them.
        Matrix sparse = cube.SparseSabrParameters(), market = cube.MarketVolCube();
        int row = Enumerable.Range(0, sparse.Rows).Single(i => sparse[i, 0] == 10.0 && sparse[i, 1] < 2.0);
        Assert.True(Math.Abs(sparse[row, 2] - 0.0332691) < 5e-8, $"alpha {sparse[row, 2]}");
        Assert.True(Math.Abs(sparse[row, 4] - 0.535233) < 5e-7, $"nu {sparse[row, 4]}");
        Assert.True(Math.Abs(sparse[row, 5] - -0.0971677) < 5e-8, $"rho {sparse[row, 5]}");
        double forward = sparse[row, 7];
        var section = new ZabrShortMaturityLognormalSmileSection(
            sparse[row, 1], forward, [sparse[row, 2], sparse[row, 3], sparse[row, 4], sparse[row, 5], sparse[row, 6]]);
        int n = CubeFixture.StrikeSpreads.Length;
        double total = 0.0, worst = 0.0;
        for (int k = 0; k < n; k++)
        {
            double e = section.Volatility(forward + CubeFixture.StrikeSpreads[k]) - market[row, 2 + k];
            total += e * e * (1.0 / n);
            worst = Math.Max(worst, Math.Abs(e));
        }

        Assert.True(Tolerance.Within(Math.Sqrt(n * total / (n - 1)), sparse[row, 8]), $"rmsError {sparse[row, 8]}");
        Assert.True(Tolerance.Within(worst, sparse[row, 9]), $"maxError {sparse[row, 9]}");
        Assert.True(sparse[row, 8] < 0.01, $"rmsError {sparse[row, 8]}");
    }

    // The fit's values, and its errors by QuantLib's formulas, are those of a section built at its reported parameters,
    // bit for bit. The errors are weighted as the fit weights them: by the strikes' Black vegas at their volatilities,
    // normalized, ZabrInterpolation's default (xabrinterpolation.hpp's update, zabrinterpolation.hpp's weight).
    private static void AssertOwnSection(ZabrInterpolation<ZabrShortMaturityLognormal> fit)
    {
        var section = new ZabrShortMaturityLognormalSmileSection(1.0, 0.03, [fit.Alpha, fit.Beta, fit.Nu, fit.Rho, fit.Gamma]);
        int n = Strikes.Length;
        var weights = new double[n];
        double weightsSum = 0.0;
        for (int i = 0; i < n; i++)
        {
            weights[i] = BlackFormulaFunctions.BlackFormulaStdDevDerivative(
                Strikes[i], 0.03, Math.Sqrt(Volatilities[i] * Volatilities[i] * 1.0), 1.0);
            weightsSum += weights[i];
        }

        double total = 0.0, worst = -double.MaxValue; // QL_MIN_REAL
        for (int i = 0; i < n; i++)
        {
            double value = section.Volatility(Strikes[i]);
            Assert.Equal(value, fit.Evaluate(Strikes[i]));
            double error = value - Volatilities[i];
            total += error * error * (weights[i] / weightsSum);
            worst = worst < Math.Abs(error) ? Math.Abs(error) : worst;
        }

        Assert.Equal(Math.Sqrt(n * total / (n - 1)), fit.RmsError);
        Assert.Equal(worst, fit.MaxError);
    }

    // Within relative of the expected value, in the stability measure at scale 1.
    private static void AssertWithin(double expected, double actual, double relative) =>
        Assert.True(Tolerance.Within(expected, actual, 1.0, relative), $"expected {expected} within {relative}, got {actual}");
}

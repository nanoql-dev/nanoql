// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CMS market calibration's reversion transform: QuantLib fits the reversion through y = m² unconstrained and reads
/// it back as √y (<c>cmsmarketcalibration.hpp:84-89</c>), so a fit that ends at y below 0 returns a NaN reversion
/// beside finite betas and error: on the wheel, on the vectors' market, Simplex(0.1) fits with the reversion free on
/// the Standard model, whose prices don't read it, return a NaN reversion on spreads from every guess of 0.05 to 1e-4
/// (a beta of 0.2425733030330998, an error of 0.011716914297575421), and finite ones on prices and forward prices.
/// nanoql reads √|y|, QuantLib's map wherever y ≥ 0. QuantLib's check: its values on every fit, betas within 1e-7
/// relative, errors within 1e-12 relative, and the reversion within 1e-5 where QuantLib's is finite, an ill-conditioned
/// fit's bounds: the C runtime and processor a fit runs on move its last bits, its beta 9.7e-12 relative on GitHub's
/// windows-2022 runner, 5.4e-11 on macOS and 1.6e-8 on Linux, and its error up to 4.8e-15 (CI run 41146085965). On
/// forward prices the two Simplex paths agree to the bit for 114 evaluations; at the 115th, at the same point, one SABR
/// section fit of the recalibrated cube, the 10-year expiry on the 2-year tenor, lands on the other of two solutions
/// QuantLib's own fit flips between when its beta moves one ulp (an ill-conditioned section fit, whose outputs the
/// vectors' stability filter drops), the costs 3.5e-12 apart, and from there the fit lands 2.8e-8 away in beta.
/// </summary>
public class CmsCalibrationReversionTransformTests
{
    private static readonly double[] Guesses = [0.05, 0.01, 0.001, 0.0001];

    [Fact]
    public void TheLevenbergMarquardtFitsKeepQuantLibsValues()
    {
        // QuantLib's: a beta of 1e-06, the reversion its guess, and the error per type
        foreach ((CmsMarketCalibrationCalibrationType type, double error) in new[]
                 {
                     (CmsMarketCalibrationCalibrationType.OnSpread, 0.011717151338411348),
                     (CmsMarketCalibrationCalibrationType.OnPrice, 0.10551894800621647),
                     (CmsMarketCalibrationCalibrationType.OnForwardCmsPrice, 0.05229195322117656),
                 })
        {
            foreach (double guess in Guesses)
            {
                CmsMarketCalibrationResults<Vector> r = Fit(type, guess, new LevenbergMarquardt(1e-8, 1e-8, 1e-8));
                Assert.Equal((1e-06, guess, error), (r.Result[0], r.Result[1], r.Error));
            }
        }
    }

    [Fact]
    public void TheSimplexFitsHaveAFiniteReversion()
    {
        // QuantLib's beta and error per type, and its reversion per guess, NaN on spreads
        foreach ((CmsMarketCalibrationCalibrationType type, double beta, double error, double[] reversions) in new[]
                 {
                     (CmsMarketCalibrationCalibrationType.OnSpread, 0.2425733030330998, 0.011716914297575421,
                         new[] { double.NaN, double.NaN, double.NaN, double.NaN }),
                     (CmsMarketCalibrationCalibrationType.OnPrice, 0.21722996661640323, 0.10551659551641257,
                         new[] { 0.33738640964959177, 0.3338107089597967, 0.3336623883751988, 0.33366090483639543 }),
                     (CmsMarketCalibrationCalibrationType.OnForwardCmsPrice, 0.17114658399913094, 0.05229104440080088,
                         new[] { 0.4087204438992126, 0.4057738301827379, 0.40565182270164807, 0.4056506024415218 }),
                 })
        {
            for (int i = 0; i < Guesses.Length; i++)
            {
                CmsMarketCalibrationResults<Vector> r = Fit(type, Guesses[i], new Simplex(0.1));
                Assert.True(double.IsFinite(r.Result[1]) && r.Result[1] >= 0.0, $"{type} {Guesses[i]}: {r.Result[1]}");
                Assert.True(Math.Abs(r.Result[0] - beta) <= 1e-7 * beta, $"{type} {Guesses[i]}: {r.Result[0]}");
                Assert.True(Math.Abs(r.Error - error) <= 1e-12 * error, $"{type} {Guesses[i]}: {r.Error}");
                if (!double.IsNaN(reversions[i]))
                {
                    Assert.True(Math.Abs(r.Result[1] - reversions[i]) <= 1e-5, $"{type} {Guesses[i]}: {r.Result[1]}");
                }
            }
        }
    }

    // A free-reversion fit from a beta of 0.5 under EndCriteria(200, 50, 1e-8, 1e-8, 1e-8).
    private static CmsMarketCalibrationResults<Vector> Fit(CmsMarketCalibrationCalibrationType type, double guess, OptimizationMethod method)
    {
        VectorCase c = VectorFile.Load("termstructures/volatility/swaption/cmsmarketcalibration").Cases[0];
        CmsMarketCalibration calibration = CmsMarketCalibrationVectorTests.CalibrationOf(c, out EvaluationContext context, calibrationType: type);
        return calibration.Compute(new EndCriteria(200, 50, 1e-8, 1e-8, 1e-8), method, new Vector([0.5, guess]), false, context);
    }
}

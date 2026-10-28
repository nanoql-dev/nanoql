// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The affine hazard-rate curve's initial intensity (the S-xcr plan's XR8 (xiii)): the model's dynamics' short rate at
/// the process's x0, where QuantLib squares x0. On nodes today and one, three and five years out (Actual/365 Fixed),
/// with the Cox-Ingersoll-Ross model r0 0.02, θ 0.03, k 0.5, σ 0.05, QuantLib's curve of zero hazard rates reads
/// S(5) 0.90893263808262059, P(0, 5, r0²), where its own base structure on the same model reads 0.87690828065332527,
/// P(0, 5, r0); on hazard rates 0.01 to 0.04 its S(5) is 0.79418153056948726, and its conditional survival from one to
/// three years at an intensity of 0.025 is 0.90156675873632208, which reads no x0. On Vasicek r0 0.02, a 0.1, b 0.03,
/// σ 0.01, whose x0 is r0 − b, its curve reads 0.96957583433..., its base 0.89655379153... (the S-xcr run's
/// <c>c4_facts.cpp</c>).
/// </summary>
public class AffineHazardRateTests
{
    private static readonly Date Today = new(31, August, 2006);

    private static Date[] Nodes => [Today, Today + new Period(1, TimeUnit.Years), Today + new Period(3, TimeUnit.Years), Today + new Period(5, TimeUnit.Years)];

    [Fact]
    public void TheCurveOfZeroHazardRatesIsTheBaseStructure()
    {
        var cir = new CoxIngersollRoss(0.02, 0.03, 0.5, 0.05);
        var curve = new InterpolatedAffineHazardRateCurve<Linear>(Nodes, [0.0, 0.0, 0.0, 0.0], new Actual365Fixed(), cir);
        var structure = new OneFactorAffineSurvivalStructure(cir, Today, new Actual365Fixed());

        // QuantLib: 0.90893263808262059 on its curve, 0.87690828065332527 on its base structure
        Assert.Equal(0.87690828065332527, structure.SurvivalProbability(5.0), 1e-15);
        Assert.Equal(0.87690828065332527, curve.SurvivalProbability(5.0), 1e-15);

        var vasicek = new Vasicek(0.02, 0.1, 0.03, 0.01);
        var curveV = new InterpolatedAffineHazardRateCurve<Linear>(Nodes, [0.0, 0.0, 0.0, 0.0], new Actual365Fixed(), vasicek);
        var structureV = new OneFactorAffineSurvivalStructure(vasicek, Today, new Actual365Fixed());

        // QuantLib: 0.969575834335 on its curve, 0.896553791531 on its base structure
        Assert.Equal(0.896553791531, structureV.SurvivalProbability(5.0), 1e-12);
        Assert.Equal(structureV.SurvivalProbability(5.0), curveV.SurvivalProbability(5.0), 1e-15);
    }

    [Fact]
    public void TheCurvesSurvivalIsQuantLibsOnTheModelsOwnIntensity()
    {
        var cir = new CoxIngersollRoss(0.02, 0.03, 0.5, 0.05);
        var curve = new InterpolatedAffineHazardRateCurve<Linear>(Nodes, [0.01, 0.02, 0.03, 0.04], new Actual365Fixed(), cir);

        // QuantLib's 0.79418153056948726 is the deterministic part's survival times P(0, 5, r0²), 0.90893263808262059:
        // here times P(0, 5, r0), 0.87690828065332527
        Assert.Equal(0.79418153056948726 / 0.90893263808262059 * 0.87690828065332527, curve.SurvivalProbability(5.0), 1e-15);

        // the conditional survival reads the realized intensity, not x0: QuantLib's
        Assert.Equal(0.90156675873632208, curve.ConditionalSurvivalProbability(1.0, 3.0, 0.025), 1e-15);
    }
}

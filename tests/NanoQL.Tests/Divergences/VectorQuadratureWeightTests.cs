// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gauss-Hermite vector integration weights every node (the S-xcr plan's XR8 (v)): QuantLib's applies its first
/// node's weight inside a <c>std::for_each</c> that discards the result (<c>multidimquadrature.hpp:61-64</c>), so node
/// n − 1 enters unweighted. QuantLib's values are the S-xcr run's <c>c1_facts.cpp</c> and the planner's
/// <c>xp_latent.cpp</c> (D18); the check is QuantLib's scalar rule per component, bit for bit.
/// </summary>
public class VectorQuadratureWeightTests
{
    [Fact]
    public void TheVectorRuleIsTheScalarRulePerComponent()
    {
        // QuantLib, order 25 in one dimension: the vector rule on [1, m, m^2] reads 13.380985451636585,
        // -0.84819164770044253, 197.61020770632012
        var g = new GaussianQuadMultidimIntegrator(1, 25);

        double[] v = g.IntegrateV(m => [1.0, m[0], m[0] * m[0]]);

        Assert.Equal(g.Integrate(_ => 1.0), v[0]);
        Assert.Equal(g.Integrate(m => m[0]), v[1]);
        Assert.Equal(g.Integrate(m => m[0] * m[0]), v[2]);
        Assert.NotEqual(13.380985451636585, v[0], 1e-3);
    }

    [Fact]
    public void ALatentModelsVectorExpectationIsItsScalarOnes()
    {
        // QuantLib, E[M^2] on the latent model's density: vector 1.0000000116420298, scalar 0.9999999999451582
        var lm = new GaussianDefProbLM(0.3, 2, LatentModelIntegrationType.GaussianQuadrature);

        double[] v = lm.IntegratedExpectedValueV(m => [m[0] * m[0], 1.0]);

        Assert.Equal(lm.IntegratedExpectedValue(m => m[0] * m[0]), v[0]);
        Assert.Equal(0.9999999999451582, v[0], 1e-15);
        Assert.Equal(lm.IntegratedExpectedValue(_ => 1.0), v[1]);
    }
}

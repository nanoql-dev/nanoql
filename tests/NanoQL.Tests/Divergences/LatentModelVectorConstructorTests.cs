// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The latent model on one factor's weights builds its Gaussian policy from them: QuantLib's constructor
/// default-constructs the policy first (<c>latentmodel.hpp:667-680</c>), whose default reads the first row of an empty
/// matrix (<c>gaussiancopulapolicy.hpp:40-43</c>): QuantLib crashes. The check is QuantLib's matrix constructor on the
/// same weights, bit for bit.
/// </summary>
public class LatentModelVectorConstructorTests
{
    [Fact]
    public void TheVectorConstructorIsTheMatrixOne()
    {
        double[] w = [0.3, 0.7, 0.0];
        var byVector = new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(w);
        var byMatrix = new LatentModel<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>([[0.3], [0.7], [0.0]]);

        Assert.Equal(byMatrix.NumFactors, byVector.NumFactors);
        Assert.Equal(byMatrix.Copula.NumFactors, byVector.Copula.NumFactors);
        Assert.Equal(byMatrix.IdiosyncFctrs, byVector.IdiosyncFctrs);
        Assert.Equal(byMatrix.LatentVarValue([0.5, -1.0, 0.2, 1.5], 1), byVector.LatentVarValue([0.5, -1.0, 0.2, 1.5], 1));
        Assert.Equal(byMatrix.InverseCumulativeY(0.03, 0), byVector.InverseCumulativeY(0.03, 0));
    }
}

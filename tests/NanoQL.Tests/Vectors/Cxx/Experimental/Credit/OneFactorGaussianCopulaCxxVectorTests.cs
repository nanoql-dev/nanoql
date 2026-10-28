// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's one-factor Gaussian copula vectors, <c>tests/vectors/cxx/experimental/credit/onefactorgaussiancopula.json</c>: the densities, the
/// distributions of Z and Y and Y's inverse, the conditional probabilities and the integrals of a probability, and the test integral of Y's distribution.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OneFactorGaussianCopulaCxxVectorTests
{
    [Fact]
    public void OneFactorGaussianCopulaCxxVector() => OneFactorCopulaCxxVectorTests.CheckMembers("cxx/experimental/credit/onefactorgaussiancopula");
}

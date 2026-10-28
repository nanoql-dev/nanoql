// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's one-factor Student t copulas' vectors, <c>tests/vectors/cxx/experimental/credit/onefactorstudentcopula.json</c>: the densities, the
/// distributions of Z and Y and Y's inverse, the conditional probabilities and the integrals of a probability, Y's from each copula's table.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OneFactorStudentCopulaCxxVectorTests
{
    [Fact]
    public void OneFactorStudentCopulaCxxVector() => OneFactorCopulaCxxVectorTests.CheckMembers("cxx/experimental/credit/onefactorstudentcopula");
}

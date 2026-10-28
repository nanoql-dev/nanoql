// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's inhomogeneous pool loss model vectors, <c>tests/vectors/cxx/experimental/credit/inhomogeneouspooldef.json</c>: the model's statistics
/// through the baskets it is bound to, and those it refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InhomogeneousPoolLossModelCxxVectorTests
{
    [Fact]
    public void InhomogeneousPoolLossModelCxxVector() => LossModelStatistics.Check("cxx/experimental/credit/inhomogeneouspooldef");
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's homogeneous pool loss model vectors, <c>tests/vectors/cxx/experimental/credit/homogeneouspooldef.json</c>: the model's statistics
/// through the baskets it is bound to, and those it refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HomogeneousPoolLossModelCxxVectorTests
{
    [Fact]
    public void HomogeneousPoolLossModelCxxVector() => LossModelStatistics.Check("cxx/experimental/credit/homogeneouspooldef");
}

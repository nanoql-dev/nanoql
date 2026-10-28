// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's base-correlation loss model vectors, <c>tests/vectors/cxx/experimental/credit/basecorrelationlossmodel.json</c>: the model's statistics
/// through the baskets it is bound to, and those it refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BaseCorrelationLossModelCxxVectorTests
{
    [Fact]
    public void BaseCorrelationLossModelCxxVector() => LossModelStatistics.Check("cxx/experimental/credit/basecorrelationlossmodel");
}

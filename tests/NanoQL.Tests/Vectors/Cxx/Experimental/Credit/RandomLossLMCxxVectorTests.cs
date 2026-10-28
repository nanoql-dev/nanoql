// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's random loss latent model vectors, <c>tests/vectors/cxx/experimental/credit/randomlosslatentmodel.json</c>: the model's statistics
/// through the baskets it is bound to, and those it refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RandomLossLMCxxVectorTests
{
    [Fact]
    public void RandomLossLMCxxVector() => LossModelStatistics.Check("cxx/experimental/credit/randomlosslatentmodel");
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's recursive loss model vectors, <c>tests/vectors/cxx/experimental/credit/recursivelossmodel.json</c>:
/// the model's statistics through the baskets it is bound to, and those it refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RecursiveLossModelCxxVectorTests
{
    [Fact]
    public void RecursiveLossModelCxxVector() => LossModelStatistics.Check("cxx/experimental/credit/recursivelossmodel");
}

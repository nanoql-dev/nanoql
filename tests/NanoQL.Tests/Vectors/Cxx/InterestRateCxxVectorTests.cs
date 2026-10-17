// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interest-rate vectors' twin from the C++ harness, <c>tests/vectors/cxx/interestrate.json</c>: the wheel
/// generator's cases, every output computed by QuantLib built from its sources, checked against nanoql by
/// <see cref="InterestRateVectorTests"/>' comparison.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterestRateCxxVectorTests
{
    [Fact]
    public void InterestRateCxxVector() => InterestRateVectorTests.Check("cxx/interestrate");
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The encoding self-vector's twin from the C++ harness, <c>tests/vectors/cxx/harness/encoding.json</c>: each sentinel,
/// literal, scaled amount and throw produced in C++, read back by <see cref="EncodingVectorTests"/>' comparison.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EncodingCxxVectorTests
{
    [Fact]
    public void EncodingCxx() => EncodingVectorTests.Check("cxx/harness/encoding");
}

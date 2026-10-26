// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's double barrier type vectors, <c>tests/vectors/cxx/instruments/doublebarriertype.json</c>: the text
/// of each <see cref="DoubleBarrierType"/>, <see cref="DoubleBarrierTypeFunctions.ToString(DoubleBarrierType)"/>, and the
/// refusal of a value outside the type.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DoubleBarrierTypeCxxVectorTests
{
    [Fact]
    public void DoubleBarrierTypeCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/doublebarriertype");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var type = (DoubleBarrierType)c.Int("type");
            check.Output(c, "text", Lazy(() => DoubleBarrierTypeFunctions.ToString(type)));
        }

        check.Complete();
    }
}

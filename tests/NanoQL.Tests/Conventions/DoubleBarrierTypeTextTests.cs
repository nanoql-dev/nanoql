// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's stream operator on <c>DoubleBarrier::Type</c> writes "KI lo+KO up" and the rest, not the members' names,
/// so it is <see cref="DoubleBarrierTypeFunctions.ToString(DoubleBarrierType)"/> (<c>doublebarriertype.cpp:27-41</c>),
/// its refusal of a value outside the type QuantLib's text. The members' integers are QuantLib's.
/// </summary>
public class DoubleBarrierTypeTextTests
{
    [Fact]
    public void TheDoubleBarrierTypesGiveQuantLibsTexts()
    {
        Assert.Equal("KnockIn", DoubleBarrierTypeFunctions.ToString(DoubleBarrierType.KnockIn));
        Assert.Equal("KnockOut", DoubleBarrierTypeFunctions.ToString(DoubleBarrierType.KnockOut));
        Assert.Equal("KI lo+KO up", DoubleBarrierTypeFunctions.ToString(DoubleBarrierType.KIKO));
        Assert.Equal("KO lo+KI up", DoubleBarrierTypeFunctions.ToString(DoubleBarrierType.KOKI));

        Assert.Equal(
            [("KnockIn", 0), ("KnockOut", 1), ("KIKO", 2), ("KOKI", 3)],
            Enum.GetValues<DoubleBarrierType>().Select(t => (t.ToString(), (int)t)));

        ArgumentOutOfRangeException e =
            Assert.Throws<ArgumentOutOfRangeException>(() => DoubleBarrierTypeFunctions.ToString((DoubleBarrierType)7));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("unknown DoubleBarrier::Type (7)", e.Message, StringComparison.Ordinal);
    }
}

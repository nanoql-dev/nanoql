// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's stream operator on <c>Barrier::Type</c> writes "Down&amp;In" and the rest, not the members' names, so it
/// is <see cref="BarrierTypeFunctions.ToString(BarrierType)"/> (the S-rx plan's SRXD11; <c>barriertype.cpp:28-42</c>),
/// its refusal of a value outside the type QuantLib's text. The members' integers are QuantLib's.
/// </summary>
public class BarrierTypeTextTests
{
    [Fact]
    public void TheBarrierTypesGiveQuantLibsTexts()
    {
        Assert.Equal("Down&In", BarrierTypeFunctions.ToString(BarrierType.DownIn));
        Assert.Equal("Up&In", BarrierTypeFunctions.ToString(BarrierType.UpIn));
        Assert.Equal("Down&Out", BarrierTypeFunctions.ToString(BarrierType.DownOut));
        Assert.Equal("Up&Out", BarrierTypeFunctions.ToString(BarrierType.UpOut));

        Assert.Equal(
            [("DownIn", 0), ("UpIn", 1), ("DownOut", 2), ("UpOut", 3)],
            Enum.GetValues<BarrierType>().Select(t => (t.ToString(), (int)t)));

        ArgumentOutOfRangeException e =
            Assert.Throws<ArgumentOutOfRangeException>(() => BarrierTypeFunctions.ToString((BarrierType)7));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("unknown Barrier::Type (7)", e.Message, StringComparison.Ordinal);
    }
}

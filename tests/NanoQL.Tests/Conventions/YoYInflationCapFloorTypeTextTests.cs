// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="YoYInflationCapFloorType"/>'s stream text, QuantLib's <c>operator&lt;&lt;</c>
/// (<c>inflationcapfloor.cpp:28-40</c>), which the C++ harness's builder vectors also read: "YoYInflationCap",
/// "YoYInflationFloor" and "YoYInflationCollar", QuantLib's integers 0, 1 and 2; any other value is an
/// <see cref="ArgumentOutOfRangeException"/> naming it, with QuantLib's "unknown YoYInflationCapFloor::Type (3)".
/// </summary>
public class YoYInflationCapFloorTypeTextTests
{
    [Fact]
    public void EachTypeHasQuantLibsText()
    {
        Assert.Equal(0, (int)YoYInflationCapFloorType.Cap);
        Assert.Equal(1, (int)YoYInflationCapFloorType.Floor);
        Assert.Equal(2, (int)YoYInflationCapFloorType.Collar);
        Assert.Equal("YoYInflationCap", InflationCapFloorFunctions.ToString(YoYInflationCapFloorType.Cap));
        Assert.Equal("YoYInflationFloor", InflationCapFloorFunctions.ToString(YoYInflationCapFloorType.Floor));
        Assert.Equal("YoYInflationCollar", InflationCapFloorFunctions.ToString(YoYInflationCapFloorType.Collar));

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => InflationCapFloorFunctions.ToString((YoYInflationCapFloorType)3));
        Assert.Equal("t", e.ParamName);
        Assert.StartsWith("unknown YoYInflationCapFloor::Type (3)", e.Message, StringComparison.Ordinal);
    }
}

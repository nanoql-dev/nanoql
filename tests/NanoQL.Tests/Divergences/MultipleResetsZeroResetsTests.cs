// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Zero resets per coupon (the S-cms plan's CMR5 (xiv)): QuantLib's checks of the resets per coupon are themselves a
/// modulo by them (<c>multipleresetscoupon.cpp:159</c>, <c>multipleresetsswap.cpp:57</c>), and the swap divides by them
/// in its base constructor's arguments before (<c>multipleresetsswap.cpp:49</c>), so zero resets is an integer division
/// by zero, undefined behaviour, which no check refuses. nanoql's leg and swap refuse zero resets first, naming them.
/// </summary>
public class MultipleResetsZeroResetsTests
{
    [Fact]
    public void ZeroResetsAreRefusedFirst()
    {
        VectorCase c = VectorFile.Load("instruments/multipleresetsswap").Cases
            .Single(x => x.Name.StartsWith("swap 5Y, monthly resets compounded quarterly, payer /", StringComparison.Ordinal));
        EvaluationContext context = c.Context();
        JsonNode spec = JsonNode.Parse(c.Input("swap").GetRawText())!;
        spec["resetsPerCoupon"] = 0;
        JsonElement zero = JsonSerializer.SerializeToElement(spec);

        ArgumentOutOfRangeException swap = Assert.Throws<ArgumentOutOfRangeException>(
            () => MultipleResetsSwapVectorTests.SwapOf(zero, context));
        Assert.Equal("resetsPerCoupon", swap.ParamName);
        Assert.StartsWith("a coupon needs at least one reset, so resetsPerCoupon must be positive", swap.Message, StringComparison.Ordinal);

        ArgumentOutOfRangeException leg = Assert.Throws<ArgumentOutOfRangeException>(() => new MultipleResetsLeg(
            CashFlowInputs.ScheduleOf(c.Input("swap").GetProperty("fullResetSchedule"), context),
            IndexInputs.IborIndexOf(c.Input("swap").GetProperty("index")),
            0));
        Assert.Equal("resetsPerCoupon", leg.ParamName);
    }
}

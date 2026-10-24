// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="StrippedOptionletAdapter.OptionletStripper"/> is QuantLib's <c>optionletStripper()</c>, the adapter's
/// stripper cast to an <c>OptionletStripper</c> (<c>strippedoptionletadapter.cpp:92-94</c>): the stripper it was built on
/// when that is an <see cref="OptionletStripper"/>, and null for another <see cref="StrippedOptionletBase"/>, a
/// <see cref="StrippedOptionlet"/>.
/// </summary>
public class StrippedOptionletAdapterStripperTests
{
    [Fact]
    public void AnOptionletStrippersAdapterGivesItBack()
    {
        VectorCase c = VectorFile.Load("cxx/termstructures/volatility/optionlet/strippedoptionletadapter").Cases[0];
        EvaluationContext context = c.Context();
        OptionletStripper stripper = OptionletStripperCxxVectorTests.StripperOf(c.Input("stripper"), context);

        Assert.Same(stripper, StrippedOptionletAdapter.Create(stripper, context).OptionletStripper);
    }

    [Fact]
    public void AnotherStripperGivesNull()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };
        var optionlet = new StrippedOptionlet(
            2, new TARGET(), BusinessDayConvention.Following, StripperFixture.Index(context),
            [new Date(16, Month.December, 2025), new Date(16, Month.June, 2026)], [0.02, 0.03],
            [[0.20, 0.19], [0.22, 0.21]], new Actual365Fixed(), context);

        Assert.Null(StrippedOptionletAdapter.Create(optionlet, context).OptionletStripper);
    }
}

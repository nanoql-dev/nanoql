// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discrete swaption volatility vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/swaption/swaptionvoldiscrete.json</c>: on volatility matrices,
/// <see cref="SwaptionVolatilityDiscrete.OptionDateFromTime"/> at times that reach the nodes, extrapolate, and leave
/// <see cref="NanoQL.Time.Date"/>'s range or the integers (NaN and the infinities among them), and the grid the base holds;
/// on the matrix built from option dates, whose date interpolation QuantLib never fills, every time is refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwaptionVolDiscreteCxxVectorTests
{
    [Fact]
    public void SwaptionVolDiscreteCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/swaption/swaptionvoldiscrete");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SwaptionVolatilityDiscrete s = SwaptionInputs.MatrixOf(c.Input("matrix"), c.Context());
            double[] times = VolatilityInputs.Reals(c.Input("times"));
            check.Output(
                c, "optionDateFromTime", times.Select(t => (Func<object?>)(() => s.OptionDateFromTime(t))).ToArray());
            check.Output(c, "optionTenors", s.OptionTenors.Select(p => new object[] { p.Length, (int)p.Units }));
            check.Output(c, "optionDates", s.OptionDates);
            check.Output(c, "optionTimes", s.OptionTimes);
            check.Output(c, "swapTenors", s.SwapTenors.Select(p => new object[] { p.Length, (int)p.Units }));
            check.Output(c, "swapLengths", s.SwapLengths);
        }

        check.Complete();
    }
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's volatility term-structure vectors, <c>tests/vectors/cxx/termstructures/voltermstructure.json</c>:
/// <see cref="VolatilityTermStructure.OptionDateFromTenor"/>, which the wheel doesn't expose, at 20 tenors, and
/// <see cref="VolatilityTermStructure.BusinessDayConvention"/>, <see cref="VolatilityTermStructure.MinStrike"/> and
/// <see cref="VolatilityTermStructure.MaxStrike"/>, on fixed and moving constant optionlet volatilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VolTermStructureCxxVectorTests
{
    [Fact]
    public void VolTermStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/voltermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            VolatilityTermStructure surface = VolatilityInputs.ConstantOptionletVolatilityOf(c.Input("surface"), c.Context());
            Period[] tenors = c.Input("tenors").EnumerateArray().Select(VolatilityInputs.TenorOf).ToArray();
            check.Output(
                c,
                "optionDateFromTenor",
                tenors.Select(p => (Func<object?>)(() => surface.OptionDateFromTenor(p))).ToArray());
            check.Output(c, "businessDayConvention", surface.BusinessDayConvention);
            check.Output(c, "minStrike", surface.MinStrike());
            check.Output(c, "maxStrike", surface.MaxStrike());
        }

        check.Complete();
    }
}

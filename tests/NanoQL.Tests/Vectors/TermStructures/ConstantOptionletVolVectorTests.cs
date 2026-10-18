// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constant optionlet volatility vectors, <c>tests/vectors/termstructures/volatility/optionlet/constantoptionletvol.json</c>:
/// on surfaces with a fixed reference date and on surfaces built from settlement days and the evaluation context, the
/// reference and maximum dates, the strike range, the volatility type and the displacement, and
/// <see cref="OptionletVolatilityStructure.Volatility(Date, double, bool)"/> and
/// <see cref="OptionletVolatilityStructure.BlackVariance(Date, double, bool)"/> and their time overloads at 10 dates
/// or times and 3 strikes each.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstantOptionletVolVectorTests
{
    [Fact]
    public void ConstantOptionletVolVector()
    {
        var file = VectorFile.Load("termstructures/volatility/optionlet/constantoptionletvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ConstantOptionletVolatility surface = VolatilityInputs.ConstantOptionletVolatilityOf(c.Input("surface"), c.Context());
            Date[] dates = c.DateInputs("dates");
            double[] times = c.Doubles("times");
            double[] strikes = c.Doubles("strikes");

            check.Output(c, "referenceDate", surface.ReferenceDate);
            check.Output(c, "maxDate", surface.MaxDate);
            check.Output(c, "minStrike", surface.MinStrike());
            check.Output(c, "maxStrike", surface.MaxStrike());
            check.Output(c, "volatilityType", surface.VolatilityType);
            check.Output(c, "displacement", surface.Displacement);
            check.Output(c, "volatilityAtDates", VolatilityInputs.Grid(dates, strikes, (d, k) => surface.Volatility(d, k)));
            check.Output(c, "volatilityAtTimes", VolatilityInputs.Grid(times, strikes, (t, k) => surface.Volatility(t, k)));
            check.Output(
                c, "blackVarianceAtDates", VolatilityInputs.Grid(dates, strikes, (d, k) => surface.BlackVariance(d, k)));
            check.Output(
                c, "blackVarianceAtTimes", VolatilityInputs.Grid(times, strikes, (t, k) => surface.BlackVariance(t, k)));
        }

        check.Complete();
    }
}

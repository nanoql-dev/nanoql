// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's base-correlation surface vectors,
/// <c>tests/vectors/cxx/experimental/credit/basecorrelationstructure.json</c>: the reference and maximum dates, the size
/// and the convention, the correlation by date and by time on and off the grid, and the dates of tenors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BaseCorrelationTermStructureCxxVectorTests
{
    [Fact]
    public void BaseCorrelationTermStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/basecorrelationstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            BaseCorrelationTermStructure<Bilinear> s = PortfolioInputs.SurfaceOf(c.Input("surface"), c.Context());
            Date[] dates = [.. c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            double[] times = [.. c.Input("times").EnumerateArray().Select(VectorCase.Real)];
            double[] levels = [.. c.Input("levels").EnumerateArray().Select(VectorCase.Real)];
            check.Output(c, "reads", new object[]
            {
                s.ReferenceDate.SerialNumber,
                s.MaxDate.SerialNumber,
                s.CorrelationSize,
                (int)s.BusinessDayConvention,
                dates.SelectMany(d => levels.Select(l => s.Correlation(d, l))).ToArray(),
                times.SelectMany(t => levels.Select(l => s.Correlation(t, l))).ToArray(),
                c.Input("tenors").EnumerateArray().Select(p => s.DateFromTenor(PortfolioInputs.PeriodOf(p)).SerialNumber).ToArray(),
            });
        }

        check.Complete();
    }
}

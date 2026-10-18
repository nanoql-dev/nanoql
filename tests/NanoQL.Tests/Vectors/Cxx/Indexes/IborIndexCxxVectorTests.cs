// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Ibor index vectors, <c>tests/vectors/cxx/indexes/iborindex.json</c>:
/// <see cref="IborIndex.ForecastFixing(Date, EvaluationContext)"/> called directly on Ibor and overnight indexes,
/// which the wheel reaches only through <see cref="InterestRateIndex.Fixing"/> on later dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IborIndexCxxVectorTests
{
    [Fact]
    public void IborIndexCxxVector()
    {
        var file = VectorFile.Load("cxx/indexes/iborindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            check.Output(
                c,
                "forecastFixing",
                c.DateInputs("dates").Select(d => (Func<object?>)(() => index.ForecastFixing(d, context))).ToArray());
        }

        check.Complete();
    }
}

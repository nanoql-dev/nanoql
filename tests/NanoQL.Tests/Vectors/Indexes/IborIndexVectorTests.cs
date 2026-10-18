// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Ibor index vectors, <c>tests/vectors/indexes/iborindex.json</c>: Ibor and overnight indexes on flat curves,
/// their conventions, maturity dates and forecast fixings; <see cref="IborIndex.Clone"/>'s name, type and forecasts on
/// another curve; and the refusal of a forecast with no curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IborIndexVectorTests
{
    [Fact]
    public void IborIndexVector()
    {
        var file = VectorFile.Load("indexes/iborindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            Date[] dates = c.DateInputs("dates");
            IborIndex clone = index.Clone(IndexInputs.CurveOf(c.Input("cloneCurve")));
            IborIndex bare = index.Clone(null);
            Date nullCurveDate = c.DateInput("nullCurveDate");

            check.Output(c, "businessDayConvention", index.BusinessDayConvention);
            check.Output(c, "endOfMonth", index.EndOfMonth);
            check.Output(c, "maturityDate", Each(dates, index.MaturityDate));
            check.Output(c, "fixing", Each(dates, d => index.Fixing(d, context)));
            check.Output(c, "cloneName", clone.Name);
            check.Output(c, "cloneType", clone.GetType().Name);
            check.Output(c, "cloneFixing", Each(dates, d => clone.Fixing(d, context)));
            check.Output(c, "nullCurveFixing", () => bare.Fixing(nullCurveDate, context));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TOut>(Date[] dates, Func<Date, TOut> f) =>
        dates.Select(d => (Func<object?>)(() => f(d))).ToArray();
}

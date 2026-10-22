// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ECB vectors, <c>tests/vectors/time/ecb.json</c>, on QuantLib's known dates (<see cref="ECB.Default"/>): codes'
/// validity, each known date's checks, code and next date, each month's date, codes' dates and next codes, and the next
/// dates after a date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ECBVectorTests
{
    [Fact]
    public void ECBVectors()
    {
        var file = VectorFile.Load("time/ecb");
        var check = new VectorAssert(file);
        ECB ecb = ECB.Default;
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Name == "isECBcode")
            {
                string[] strings = [.. c.Input("strings").EnumerateArray().Select(e => e.GetString()!)];
                check.Output(c, "isECBcode", strings.Select(s => (object?)ECB.IsECBcode(s)).ToArray());
            }
            else if (c.Name == "knownDates")
            {
                Date[] ds = c.DateInputs("dates");
                check.Output(c, "isECBdate", Each(ds, d => ecb.IsECBdate(d)));
                check.Output(c, "isECBdateBefore", Each(ds, d => ecb.IsECBdate(d - 1)));
                check.Output(c, "code", Each(ds, d => ecb.Code(d)));
                check.Output(c, "nextDateBefore", Each(ds, d => ecb.NextDate(context, d - 1)));
                check.Output(c, "nextDate", Each(ds, d => ecb.NextDate(context, d)));
                check.Output(c, "nextCodeBefore", Each(ds, d => ecb.NextCode(context, d - 1)));
            }
            else if (c.Name.StartsWith("months ", StringComparison.Ordinal))
            {
                int y = c.Int("year");
                check.Output(
                    c,
                    "date",
                    Enumerable.Range(1, 12).Select(m => (Func<object?>)(() => ecb.Date((Month)m, y))).ToArray());
            }
            else if (c.Name.StartsWith("codes ", StringComparison.Ordinal))
            {
                string[] codes = [.. c.Input("codes").EnumerateArray().Select(e => e.GetString()!)];
                Date reference = c.DateInput("referenceDate");
                check.Output(c, "date", Each(codes, s => ecb.Date(s, context, reference)));
                check.Output(c, "nextDate", Each(codes, s => ecb.NextDate(s, context, reference)));
                check.Output(c, "nextDatesCount", Each(codes, s => ecb.NextDates(s, context, reference).Length));
                check.Output(c, "nextCode", Each(codes, ECB.NextCode));
            }
            else
            {
                Date[] ds = c.DateInputs("dates");
                for (int i = 0; i < ds.Length; ++i)
                {
                    Date d = ds[i];
                    check.Output(c, $"nextDates{i}", () => ecb.NextDates(context, d));
                }
            }
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TIn, TOut>(TIn[] inputs, Func<TIn, TOut> f) =>
        inputs.Select(x => (Func<object?>)(() => f(x))).ToArray();
}

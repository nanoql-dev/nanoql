// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CPI cap and floor's harness vectors, <c>tests/vectors/cxx/instruments/cpicapfloor.json</c>: caps and floors on
/// the UK RPI, their members, fixing and payment dates and expiry, and the arguments QuantLib's
/// <c>setupArguments</c> copies from them, read here from the members they copy (the instrument's arguments builder is
/// internal, the interpolating CPI cap and floor engine's contract); and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CPICapFloorCxxVectorTests
{
    [Fact]
    public void CPICapFloorCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/cpicapfloor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement[] a = [.. c.Input("arguments").EnumerateArray()];
            CPICapFloor Cap() => new(
                (OptionType)a[0].GetInt32(),
                a[1].GetDouble(),
                new Date(a[2].GetInt32()),
                a[3].GetDouble(),
                new Date(a[4].GetInt32()),
                CashFlowInputs.NullableCalendarOf(a[5])!,
                (BusinessDayConvention)a[6].GetInt32(),
                CashFlowInputs.NullableCalendarOf(a[7])!,
                (BusinessDayConvention)a[8].GetInt32(),
                a[9].GetDouble(),
                (ZeroInflationIndex)InflationInputs.IndexOf(a[10]),
                InflationInputs.LagOf(a[11]),
                (CPIInterpolationType)a[12].GetInt32());
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => Cap());
                continue;
            }

            CPICapFloor cap = Cap();
            check.Output(c, "type", (int)cap.Type);
            check.Output(c, "nominal", cap.Nominal);
            check.Output(c, "strike", cap.Strike);
            check.Output(c, "fixingDate", cap.FixingDate());
            check.Output(c, "payDate", cap.PayDate());
            check.Output(c, "indexName", cap.Index.Name);
            check.Output(c, "observationLag", Lag(cap.ObservationLag));
            check.Output(c, "isExpired", cap.IsExpired(context));

            // QuantLib's setupArguments copies the members, and the fixing and payment dates
            check.Output(c, "argumentsType", (int)cap.Type);
            check.Output(c, "argumentsNominal", cap.Nominal);
            check.Output(c, "argumentsStartDate", cap.StartDate);
            check.Output(c, "argumentsFixDate", cap.FixingDate());
            check.Output(c, "argumentsPayDate", cap.PayDate());
            check.Output(c, "argumentsBaseCPI", cap.BaseCPI);
            check.Output(c, "argumentsMaturity", cap.Maturity);
            check.Output(c, "argumentsFixConvention", (int)cap.FixConvention);
            check.Output(c, "argumentsPayConvention", (int)cap.PayConvention);
            check.Output(c, "argumentsStrike", cap.Strike);
            check.Output(c, "argumentsObservationLag", Lag(cap.ObservationLag));
            check.Output(c, "argumentsInterpolation", (int)cap.ObservationInterpolation);
        }

        check.Complete();
    }

    private static int[] Lag(Period p) => [p.Length, (int)p.Units];
}

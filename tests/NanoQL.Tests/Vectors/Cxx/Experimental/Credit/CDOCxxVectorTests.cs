// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's older CDO vectors, <c>tests/vectors/cxx/experimental/credit/cdo.json</c>: the NPV, the legs, the
/// fair premium, the count of falling losses, the nominal and the expiry.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CDOCxxVectorTests
{
    [Fact]
    public void CDOCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/cdo");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            var cdo = new CDO(
                VectorCase.Real(a[1]),
                VectorCase.Real(a[2]),
                [.. a[3].EnumerateArray().Select(VectorCase.Real)],
                [.. a[4].EnumerateArray().Select(s => CreditInputs.CurveOf(s, context))],
                PortfolioInputs.OneFactorCopulaOf(a[0]),
                a[5].GetBoolean(),
                CreditInputs.ScheduleOf(a[6], context),
                VectorCase.Real(a[7]),
                CreditInputs.DayCounterOf(a[8]),
                VectorCase.Real(a[9]),
                VectorCase.Real(a[10]),
                CreditInputs.DiscountOf(a[11])!,
                a[12].GetInt32(),
                PortfolioInputs.PeriodOf(a[13]));
            check.Output(c, "calculate", () =>
            {
                CDOResults r = cdo.Calculate(context);
                return new object?[] { r.NPV, r.PremiumValue, r.ProtectionValue, r.FairPremium, r.Error, cdo.Nominal, cdo.IsExpired(context) };
            });
        }

        check.Complete();
    }
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FRA vectors, <c>tests/vectors/instruments/forwardrateagreement.json</c>: both constructors, long and short, with
/// and without a discount curve, past and today's fixings, two expired FRAs and two refused constructions; each FRA's
/// fixing date, expiry, forward rate, amount and NPV from <see cref="ForwardRateAgreement.Calculate"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ForwardRateAgreementVectorTests
{
    [Fact]
    public void ForwardRateAgreementVector()
    {
        var file = VectorFile.Load("instruments/forwardrateagreement");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("fra");
            ForwardRateAgreement Build()
            {
                var index = SwapInputs.IndexOf(spec.GetProperty("index"));
                var valueDate = new Date(spec.GetProperty("valueDate").GetInt32());
                var position = (PositionType)spec.GetProperty("type").GetInt32();
                double strike = spec.GetProperty("strike").GetDouble();
                double notional = spec.GetProperty("notional").GetDouble();
                var discount = IndexInputs.CurveOf(spec.GetProperty("discountCurve"));
                JsonElement maturity = spec.GetProperty("maturityDate");
                return maturity.ValueKind == JsonValueKind.Null
                    ? new ForwardRateAgreement(index, valueDate, position, strike, notional, discount)
                    : new ForwardRateAgreement(
                        index, valueDate, new Date(maturity.GetInt32()), position, strike, notional, discount);
            }

            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => Build());
                continue;
            }

            ForwardRateAgreement fra = Build();
            check.Output(c, "fixingDate", fra.FixingDate());
            check.Output(c, "isExpired", fra.IsExpired(context));
            if (c.Expected.TryGetProperty("forwardRate", out _))
            {
                check.Output(c, "forwardRate", () => fra.Calculate(context).ForwardRate!.Rate);
                check.Output(c, "NPV", () => fra.Calculate(context).NPV);
            }

            if (c.Expected.TryGetProperty("amount", out _))
            {
                check.Output(c, "amount", () => fra.Calculate(context).Amount);
            }
        }

        check.Complete();
    }
}

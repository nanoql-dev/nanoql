// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="OptionletStripper"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/optionlet/optionletstripper.json</c>: the members the wheel doesn't bind
/// (the fixing tenors, payment dates, accrual periods, frequency and payment lag), and the strip of Ibor strippers with
/// a payment lag and no optionlet frequency, which the wheel can't build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OptionletStripperCxxVectorTests
{
    [Fact]
    public void OptionletStripperCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/optionlet/optionletstripper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OptionletStripper1 s = StripperOf(c.Input("stripper"), context);
            int n = s.OptionletMaturities;
            check.Output(c, "optionletFixingTenors", s.OptionletFixingTenors.Select(t => new[] { t.Length, (int)t.Units }).ToArray());
            check.Output(c, "optionletPaymentDates", s.OptionletPaymentDates);
            check.Output(c, "optionletAccrualPeriods", s.OptionletAccrualPeriods);
            check.Output(
                c,
                "optionletFrequency",
                s.OptionletFrequency is Time.Period f ? new[] { f.Length, (int)f.Units } : null);
            check.Output(c, "paymentLag", s.PaymentLag);
            check.Output(c, "optionletFixingDates", s.OptionletFixingDates);
            check.Output(c, "optionletFixingTimes", s.OptionletFixingTimes);
            check.Output(c, "atmOptionletRates", s.AtmOptionletRates(context));
            check.Output(c, "optionletStrikes", Enumerable.Range(0, n + 1).Select(i => (Func<object?>)(() => s.OptionletStrikes(i))).ToArray());
            check.Output(c, "optionletVolatilities", Enumerable.Range(0, n + 1).Select(i => (Func<object?>)(() => s.OptionletVolatilities(i))).ToArray());
            check.Output(c, "switchStrike", s.SwitchStrike);
        }

        check.Complete();
    }

    /// <summary>The stripper of a harness stripper spec: its index an Ibor or overnight index spec, its curve inside.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The stripper.</returns>
    internal static OptionletStripper1 StripperOf(JsonElement spec, EvaluationContext context)
    {
        IborIndex index = IndexInputs.IborIndexOf(spec.GetProperty("index"));
        JsonElement switchStrike = spec.GetProperty("switchStrike");
        JsonElement frequency = spec.GetProperty("optionletFrequency");
        return new OptionletStripper1(
            CapFloorTermVolSurfaceVectorTests.SurfaceOf(spec.GetProperty("surface"), context),
            index,
            context,
            switchStrike.ValueKind == JsonValueKind.Null ? null : InterpolationInputs.Number(switchStrike),
            InterpolationInputs.Number(spec.GetProperty("accuracy")),
            spec.GetProperty("maxIter").GetInt32(),
            IndexInputs.CurveOf(spec.GetProperty("discount")),
            (VolatilityType)spec.GetProperty("type").GetInt32(),
            InterpolationInputs.Number(spec.GetProperty("displacement")),
            spec.GetProperty("dontThrow").GetBoolean(),
            frequency.ValueKind == JsonValueKind.Null ? null : SwapInputs.PeriodOf(frequency),
            spec.GetProperty("paymentLag").GetInt32());
    }
}

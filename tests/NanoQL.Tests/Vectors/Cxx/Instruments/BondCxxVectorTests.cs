// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's bond vectors, <c>tests/vectors/cxx/instruments/bond.json</c>: bonds the wheel can't build (on
/// QuantLib's empty calendar or day counter, a fixed-rate bond's first-period day counter), with the bond vectors'
/// outputs (<see cref="BondInputs"/>) and a fixed-rate bond's inspectors; and the theoretical yield of
/// <see cref="Bond.Yield(BondResults, DayCounter, Compounding, Frequency, EvaluationContext, double, int, double, BondPriceType)"/>
/// with its guess and price type, on those bonds and on the bond vectors' own. A refused construction is the case's
/// one output, "construct".
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BondCxxVectorTests
{
    [Fact]
    public void BondCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/bond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => BondInputs.BondOf(c.Input("bond"), context));
                continue;
            }

            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            if (c.Inputs.TryGetProperty("dates", out _))
            {
                BondInputs.CheckStatic(check, c, bond, context);
                BondInputs.CheckDated(check, c, bond, context);
                BondInputs.CheckEngine(check, c, bond, context);
            }

            if (bond is FixedRateBond fixedRate)
            {
                check.Output(c, "frequency", (int)fixedRate.Frequency);
                check.Output(c, "dayCounter", fixedRate.DayCounter.Name);
                check.Output(c, "firstPeriodDayCounter", fixedRate.FirstPeriodDayCounter?.Name);
            }

            FlatForward curve = CashFlowInputs.CurveOf(c.Input("curve"));
            InterestRate rate = BondInputs.RateOf(c.Input("rate"));
            check.Output(c, "theoreticalYield", BondInputs.Each(c.Input("yields").EnumerateArray(), (JsonElement y) =>
                bond.Yield(
                    DiscountingBondEngine.Calculate(bond, context, curve),
                    rate.DayCounter,
                    rate.Compounding,
                    rate.Frequency,
                    context,
                    y.GetProperty("accuracy").GetDouble(),
                    y.GetProperty("maxEvaluations").GetInt32(),
                    y.GetProperty("guess").GetDouble(),
                    (BondPriceType)y.GetProperty("priceType").GetInt32())));
        }

        check.Complete();
    }
}

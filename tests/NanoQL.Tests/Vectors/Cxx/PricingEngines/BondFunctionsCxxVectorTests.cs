// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's bond functions vectors, <c>tests/vectors/cxx/pricingengines/bond/bondfunctions.json</c>: the
/// members of <see cref="BondFunctions"/> the wheel doesn't bind, on the bond functions vectors' bonds and two on
/// QuantLib's empty calendar, at 8 settlement dates: the reference period's start and end, the positions of the
/// previous and next cash flows, and the dirty price for a yield in both forms. A refused value is a throw.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BondFunctionsCxxVectorTests
{
    [Fact]
    public void BondFunctionsCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/bond/bondfunctions");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            InterestRate r = BondInputs.RateOf(c.Input("rate"));
            Date?[] dates = BondInputs.DatesOf(c, "dates");

            void Each<T>(string name, Func<Date?, T> f) => check.Output(c, name, BondInputs.Each(dates, f));

            Each("referencePeriodStart", d => BondFunctions.ReferencePeriodStart(bond, context, d));
            Each("referencePeriodEnd", d => BondFunctions.ReferencePeriodEnd(bond, context, d));
            Each("previousCashFlow", d => BondFunctions.PreviousCashFlow(bond, context, d));
            Each("nextCashFlow", d => BondFunctions.NextCashFlow(bond, context, d));
            Each("dirtyPriceRate", d => BondFunctions.DirtyPrice(bond, r, context, d));
            Each("dirtyPriceRateForm", d => BondFunctions.DirtyPrice(
                bond, r.Rate, r.DayCounter, r.Compounding, r.Frequency, context, d));
        }

        check.Complete();
    }
}

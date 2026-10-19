// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The bond helper vectors, <c>tests/vectors/termstructures/yield/bondhelpers.json</c>: five sets of bond helpers,
/// their dates and price types, and four curves bootstrapped on each, with each helper's quote error and implied quote
/// on each curve. The helpers are <see cref="BondInputs.HelperOf"/>'s specs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BondHelpersVectorTests
{
    [Fact]
    public void BondHelpersVector()
    {
        var file = VectorFile.Load("termstructures/yield/bondhelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BondHelper[] helpers =
                [.. c.Input("helpers").EnumerateArray().Select(s => BondInputs.HelperOf(s, context))];
            check.Output(c, "earliestDate", helpers.Select(h => h.EarliestDate));
            check.Output(c, "maturityDate", helpers.Select(h => h.MaturityDate));
            check.Output(c, "latestRelevantDate", helpers.Select(h => h.LatestRelevantDate));
            check.Output(c, "pillarDate", helpers.Select(h => h.PillarDate));
            check.Output(c, "latestDate", helpers.Select(h => h.LatestDate));
            check.Output(c, "priceType", helpers.Select(h => h.PriceType));

            BootstrapHelper<YieldTermStructure>[] bootstrapHelpers = [.. helpers];
            BootstrapInputs.CheckCurves(check, c, bootstrapHelpers, context);
            check.Output(c, "impliedQuotes", c.Input("curves").EnumerateArray().Select(spec =>
            {
                BootstrapInputs.Built built;
                try
                {
                    built = BootstrapInputs.CurveOf(spec, bootstrapHelpers, context);
                }
                catch (NanoQLException)
                {
                    return Array.Empty<Func<object?>>();
                }

                return BondInputs.Each(helpers, h => h.SetTermStructure(built.Curve).ImpliedQuote(context));
            }));
        }

        check.Complete();
    }
}

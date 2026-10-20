// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CDS helper vectors, <c>tests/vectors/termstructures/credit/defaultprobabilityhelpers.json</c>:
/// <see cref="SpreadCdsHelper"/>'s and <see cref="UpfrontCdsHelper"/>'s dates, and their implied quotes and quote
/// errors bound to the piecewise flat hazard-rate curve they bootstrap (null where the stability filter dropped them).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DefaultProbabilityHelpersVectorTests
{
    [Fact]
    public void DefaultProbabilityHelpersVector()
    {
        var file = VectorFile.Load("termstructures/credit/defaultprobabilityhelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CdsHelper[] helpers = [.. c.Input("helpers").EnumerateArray().Select(s => CreditInputs.HelperOf(s, context))];
            var curve = CreditInputs.PiecewiseOf(c.Input("curve"), helpers, context);
            CdsHelper[] bound = [.. helpers.Select(h => h.SetTermStructure(curve))];
            check.Output(c, "earliestDates", helpers.Select(h => (object?)h.EarliestDate));
            check.Output(c, "latestDates", helpers.Select(h => (object?)h.LatestDate));
            check.Output(c, "pillarDates", helpers.Select(h => (object?)h.PillarDate));
            check.Output(c, "maturityDates", helpers.Select(h => (object?)h.MaturityDate));
            check.Output(c, "latestRelevantDates", helpers.Select(h => (object?)h.LatestRelevantDate));
            check.Output(
                c, "impliedQuotes", CreditInputs.Kept(c, "impliedQuotes", bound.Select(h => (Func<object?>)(() => h.ImpliedQuote(context)))));
            check.Output(
                c, "quoteErrors", CreditInputs.Kept(c, "quoteErrors", bound.Select(h => (Func<object?>)(() => h.QuoteError(context)))));
        }

        check.Complete();
    }
}

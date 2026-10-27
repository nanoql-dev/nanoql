// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Quotes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's vectors of the quotes the wheel doesn't expose, <c>tests/vectors/cxx/quotes/*.json</c>:
/// <see cref="ForwardValueQuote"/>, <see cref="ImpliedStdDevQuote"/>, <see cref="EurodollarFuturesImpliedStdDevQuote"/>
/// and <see cref="ForwardSwapQuote"/>, each built under the case's context.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QuoteCxxVectorTests
{
    [Fact]
    public void ForwardValueQuoteCxxVector()
    {
        var file = VectorFile.Load("cxx/quotes/forwardvaluequote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            check.Output(c, "value", () => new ForwardValueQuote(index, c.DateInput("fixingDate"), context).Value);
        }

        check.Complete();
    }

    [Fact]
    public void ImpliedStdDevQuoteCxxVector()
    {
        var file = VectorFile.Load("cxx/quotes/impliedstddevquote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var q = new ImpliedStdDevQuote(
                (OptionType)c.Int("optionType"),
                c.Double("forward"),
                c.Double("price"),
                c.Double("strike"),
                c.Double("guess"),
                c.Double("accuracy"),
                c.Int("maxIterations"));
            check.Output(c, "value", q.Value);
        }

        check.Complete();
    }

    [Fact]
    public void EurodollarFuturesImpliedStdDevQuoteCxxVector()
    {
        var file = VectorFile.Load("cxx/quotes/eurodollarfuturesquote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            check.Output(c, "value", () => new EurodollarFuturesImpliedStdDevQuote(
                c.Double("forward"),
                c.Double("callPrice"),
                c.Double("putPrice"),
                c.Double("strike"),
                c.Double("guess"),
                c.Double("accuracy"),
                c.Int("maxIterations")).Value);
        }

        check.Complete();
    }

    [Fact]
    public void ForwardSwapQuoteCxxVector()
    {
        var file = VectorFile.Load("cxx/quotes/forwardswapquote");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwapIndex index = SwaptionInputs.SwapIndexOf(c.Input("swapIndex"));
            int[] start = c.Ints("fwdStart");
            var q = new ForwardSwapQuote(index, c.Double("spread"), new Period(start[0], (TimeUnit)start[1]), context);
            check.Output(c, "value", q.Value);
            check.Output(c, "valueDate", q.ValueDate);
            check.Output(c, "startDate", q.StartDate);
            check.Output(c, "fixingDate", q.FixingDate);
        }

        check.Complete();
    }
}

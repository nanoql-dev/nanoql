// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's bootstrap-helper vectors, <c>tests/vectors/cxx/termstructures/bootstraphelper.json</c>: the five
/// date inspectors of <see cref="BootstrapHelper{TTermStructure}"/> and their fallbacks, on a helper of the test's own
/// setting the dates each case gives, as the harness's helper sets them; the dates of a
/// <see cref="RelativeDateBootstrapHelper{TTermStructure}"/> of the test's own, computed from the evaluation date it
/// recorded or from a fixed date, as the harness's computes them; and
/// <see cref="BootstrapHelperFunctions.ToString(PillarChoice)"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BootstrapHelperCxxVectorTests
{
    [Fact]
    public void BootstrapHelperCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/bootstraphelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("choices", out JsonElement choices))
            {
                check.Output(c, "toString", choices.EnumerateArray()
                    .Select(x => (Func<object?>)(() => BootstrapHelperFunctions.ToString((PillarChoice)x.GetInt32())))
                    .ToArray());
                continue;
            }

            if (c.Inputs.TryGetProperty("offsets", out JsonElement offsets))
            {
                bool updateDates = offsets.GetProperty("updateDates").GetBoolean();
                RateHelperInputs.CheckDates(check, c, new OffsetDatesHelper(
                    updateDates ? c.Context() : null,
                    updateDates,
                    new Date(offsets.GetProperty("fixedDate").GetInt32()),
                    offsets.GetProperty("earliestOffset").GetInt32(),
                    offsets.GetProperty("pillarOffset").GetInt32()));
                continue;
            }

            JsonElement dates = c.Input("dates");
            var helper = new ChosenDatesHelper(
                DateOf(dates, "earliestDate"),
                DateOf(dates, "latestDate"),
                DateOf(dates, "maturityDate"),
                DateOf(dates, "latestRelevantDate"),
                DateOf(dates, "pillarDate"));
            RateHelperInputs.CheckDates(check, c, helper);
        }

        check.Complete();
    }

    private static Date? DateOf(JsonElement dates, string name) => CashFlowInputs.NullableDate(dates.GetProperty(name));

    /// <summary>
    /// A helper setting the five dates it is given, null for QuantLib's <c>Date()</c>, as the harness's own helper
    /// does (<c>tools/cxx/src/termstructures/bootstraphelper.cpp</c>); it implies no quote.
    /// </summary>
    private sealed class ChosenDatesHelper : BootstrapHelper<YieldTermStructure>
    {
        public ChosenDatesHelper(Date? earliest, Date? latest, Date? maturity, Date? latestRelevant, Date? pillar)
            : base(0.0)
        {
            earliestDate = earliest;
            latestDate = latest;
            maturityDate = maturity;
            latestRelevantDate = latestRelevant;
            pillarDate = pillar;
        }

        public override double ImpliedQuote(EvaluationContext context) =>
            throw new NanoQLException("the test's helper with chosen dates implies no quote");
    }

    /// <summary>
    /// A helper whose earliest and pillar dates are offsets in days from the evaluation date it recorded, when its dates
    /// follow the evaluation date, or from a fixed date, when they don't, as the harness's own relative helper computes
    /// them in <c>initializeDates()</c>; it implies no quote.
    /// </summary>
    private sealed class OffsetDatesHelper : RelativeDateBootstrapHelper<YieldTermStructure>
    {
        private readonly Date fixedDate;
        private readonly int earliestOffset;
        private readonly int pillarOffset;

        public OffsetDatesHelper(
            EvaluationContext? context, bool updateDates, Date fixedDate, int earliestOffset, int pillarOffset)
            : base(0.0, context, updateDates)
        {
            this.fixedDate = fixedDate;
            this.earliestOffset = earliestOffset;
            this.pillarOffset = pillarOffset;
            SetDates(UpdateDates ? EvaluationDate!.Value : fixedDate);
        }

        public override double ImpliedQuote(EvaluationContext context) =>
            throw new NanoQLException("the test's helper with offset dates implies no quote");

        protected override void InitializeDates(EvaluationContext context) =>
            SetDates(UpdateDates ? context.EvaluationDate : fixedDate);

        private void SetDates(Date from)
        {
            earliestDate = from + earliestOffset;
            pillarDate = from + pillarOffset;
        }
    }
}

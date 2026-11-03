// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// End to end: a file written by hand, with an override of <c>TARGET</c>, a joint calendar, an OIS convention and
/// another tool's annotation, set as <see cref="ConventionProvider.Default"/>, reaches a direct construction, a set's
/// lookup, an OIS rate helper, re-dated by a bootstrap after <c>Default</c> is set back, and a swap; inside a scope of
/// QuantLib's provider the direct constructions are QuantLib's; and the file written back reads the same records, the
/// annotation where it was. In the collection whose facts alone set <c>Default</c>.
/// </summary>
[Collection(DefaultProviderCollection.Name)]
public class ConventionsEndToEndTests
{
    private const string File = """
        <Conventions xmlns="https://nanoql.org/schemas/conventions" xmlns:desk="urn:desk">
          <Calendar name="TARGET" base="TARGET" desk:owner="rates">
            <AddedHoliday>2026-12-28</AddedHoliday>
          </Calendar>
          <JointCalendar name="TARGET+NYSE">
            <Rule>JoinHolidays</Rule>
            <Member>TARGET</Member>
            <Member>UnitedStates.NYSE</Member>
          </JointCalendar>
          <OisConvention name="EUR-ESTR">
            <Index>Estr</Index>
            <PaymentLag>1</PaymentLag>
            <PaymentCalendar>TARGET+NYSE</PaymentCalendar>
          </OisConvention>
        </Conventions>
        """;

    // A Monday, a business day on QuantLib's TARGET and New York's exchange, a holiday in the file's.
    private static readonly Date Holiday = new(28, Month.December, 2026);

    [Fact]
    public void AFileSetAsTheDefaultReachesEachConstructionAndWritesBackWithItsAnnotation()
    {
        ConventionSnapshot snapshot = ConventionsXml.Read(new StringReader(File), "desk.xml");
        ConventionSet set = ConventionSet.QuantLib.With(snapshot);
        var early = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        var late = new EvaluationContext { EvaluationDate = new Date(16, Month.October, 2026) };
        var curve = new FlatForward(early.EvaluationDate, 0.03, new Actual365Fixed());
        OISRateHelper helper;
        try
        {
            ConventionProvider.Default = set;
            var euribor = new Euribor(new Period(6, TimeUnit.Months));
            IborIndex fromSet = set.IborIndex("Euribor6M");
            helper = set.Ois("EUR-ESTR").RateHelper(0.03, new Period(2, TimeUnit.Years), early);
            VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve)).WithFixedRate(0.03).ToVanillaSwap(early);

            Assert.True(euribor.FixingCalendar.IsHoliday(Holiday));
            Assert.True(fromSet.FixingCalendar.IsHoliday(Holiday));
            Assert.True(swap.FixedSchedule.Calendar!.IsHoliday(Holiday));
            Assert.True(helper.Swap.PaymentCalendar!.IsHoliday(Holiday));
            Assert.True(helper.Swap.OvernightSchedule.Calendar!.IsHoliday(Holiday));
        }
        finally
        {
            ConventionProvider.Default = null;
        }

        // the bootstrap re-dates the helper a day later, Default QuantLib's again: its new swap keeps the file's
        var bound = new List<IBootstrapHelper>();
        _ = PiecewiseYieldCurve<Discount, LogLinear, Capturing>.Create(
            0, new TARGET(), [helper], new Actual365Fixed(), late, bootstrap: new Capturing(bound));
        var redated = (OISRateHelper)Assert.Single(bound);
        Assert.NotEqual(helper.Swap.FixedSchedule.Dates[0], redated.Swap.FixedSchedule.Dates[0]);
        Assert.True(redated.Swap.PaymentCalendar!.IsHoliday(Holiday));
        Assert.True(redated.Swap.OvernightSchedule.Calendar!.IsHoliday(Holiday));

        // inside a scope of QuantLib's provider the direct constructions are QuantLib's; a set's lookup stays the set's
        using (ConventionProvider.Use(ConventionProvider.QuantLib))
        {
            Assert.False(new Euribor(new Period(6, TimeUnit.Months)).FixingCalendar.IsHoliday(Holiday));
            Assert.False(new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(curve)).WithFixedRate(0.03).ToVanillaSwap(early)
                .FixedSchedule.Calendar!.IsHoliday(Holiday));
            Assert.True(set.IborIndex("Euribor6M").FixingCalendar.IsHoliday(Holiday));
        }

        // the file written back reads the same records, the annotation on its record
        var written = new StringWriter();
        ConventionsXml.Write(snapshot, written);
        ConventionSnapshot again = ConventionsXml.Read(new StringReader(written.ToString()));
        Assert.Equal(snapshot.Records, again.Records);
        ForeignAttribute owner = Assert.Single(again.Records[0].Foreign.Attributes);
        Assert.Equal(("urn:desk", "owner", "rates"), (owner.Namespace, owner.LocalName, owner.Value));
    }

    // QuantLib's iterative bootstrap, recording the helpers it binds: the copies re-dated to the curve's context.
    private readonly struct Capturing(List<IBootstrapHelper> bound) : IBootstrap
    {
        public void Calculate<TTermStructure, TTraits, TInterpolator>(
            BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context)
            where TTermStructure : TermStructure
            where TTraits : struct, IBootstrapTraits<TTermStructure>
            where TInterpolator : struct, IInterpolator
        {
            new IterativeBootstrap().Calculate<TTermStructure, TTraits, TInterpolator>(state, interpolator, context);
            bound.AddRange(state.Instruments);
        }
    }
}

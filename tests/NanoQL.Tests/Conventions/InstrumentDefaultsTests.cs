// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Conventions;

/// <summary>
/// An instrument of one's own (<see cref="Instrument"/> is an extension point) takes the base's defaults: its terms
/// pass <see cref="Instrument.Validate"/> unchecked, an expired one's results are QuantLib's <c>setupExpired</c>'s (a
/// zero value and error estimate, no valuation date), and an engine's results are its results as they are. Every
/// instrument ported so far overrides <see cref="Instrument.Validate"/>, so only an instrument of one's own reaches
/// the base's.
/// </summary>
public class InstrumentDefaultsTests
{
    [Fact]
    public void AnInstrumentOfOnesOwnTakesTheBasesDefaults()
    {
        var instrument = new OwnInstrument();

        instrument.Validate();

        InstrumentResults expired = instrument.SetupExpired();
        Assert.Equal(0.0, expired.NPV);
        Assert.Equal(0.0, expired.ErrorEstimate);
        Assert.Null(expired.ValuationDate);

        var results = new InstrumentResults { NPV = 1.5 };
        Assert.Same(results, instrument.FetchResults(results));
    }

    private sealed class OwnInstrument : Instrument
    {
        public override bool IsExpired(EvaluationContext context) => false;
    }
}

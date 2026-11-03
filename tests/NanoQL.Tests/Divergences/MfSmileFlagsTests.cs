// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Markov-functional model's smile pretreatments: QuantLib's settings refuse only all three of Kahale's, SABR's and
/// a custom smile, though the refusal's text says "Only one … can be specified" (<c>markovfunctional.hpp:162-167</c>),
/// so on the wheel Kahale's smile with SABR's validates, and the model then pretreats with Kahale's and ignores SABR's.
/// nanoql refuses any two, as QuantLib's master does after 1.44 (#2912,
/// <c>Divergences/MarkovFunctionalUpstreamFixesTests.cs</c>), with the master's text, "At most one of …". QuantLib's
/// check: the master's own cases.
/// </summary>
public class MfSmileFlagsTests
{
    [Fact]
    public void AnyTwoSmilePretreatmentsAreRefused()
    {
        foreach (MarkovFunctionalModelSettingsAdjustments smiles in new[] { KahaleSmile | SabrSmile, KahaleSmile | CustomSmile, SabrSmile | CustomSmile, KahaleSmile | SabrSmile | CustomSmile })
        {
            MarkovFunctionalModelSettings settings = new MarkovFunctionalModelSettings().WithAdjustments(smiles);
            NanoQLException e = Assert.Throws<NanoQLException>(() => settings.Validate());
            Assert.Equal("At most one of KahaleSmile, SabrSmile and CustomSmile can be specified at the same time", e.Message);
        }

        // One alone validates.
        Assert.Equal(SabrSmile, new MarkovFunctionalModelSettings().WithAdjustments(SabrSmile).Validate().Adjustments);
    }
}

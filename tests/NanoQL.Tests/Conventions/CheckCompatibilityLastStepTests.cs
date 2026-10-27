// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="EvolutionDescriptionFunctions.CheckCompatibility"/> keeps QuantLib's check at every step but the last
/// (the S-mm plan's MR4 (c), as the user re-ruled it): a last step's numeraire maturing inside the step is accepted, as
/// QuantLib accepts it (<c>evolutiondescription.cpp:121</c>; the planner's MP26), and documented as the bond rolled into
/// the next at its maturity, which prices correctly; an earlier step's numeraire maturing before its end is refused.
/// </summary>
public class CheckCompatibilityLastStepTests
{
    private static readonly EvolutionDescription Evolution = new([0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5], [0.5, 1.0, 1.5, 2.25]);

    [Fact]
    public void ALastStepsNumeraireMaturingInsideTheStepIsAccepted() =>
        EvolutionDescriptionFunctions.CheckCompatibility(Evolution, [1, 2, 3, 3]);

    [Fact]
    public void AnEarlierStepsExpiredNumeraireIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => EvolutionDescriptionFunctions.CheckCompatibility(Evolution, [1, 2, 1, 4]));
        Assert.StartsWith("3rd step, evolution time 1.5: the numeraire (1), corresponding to rate time 1, is expired", e.Message, StringComparison.Ordinal);
    }
}

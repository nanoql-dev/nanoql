// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// ISDA's conventional recovery refused for <see cref="Seniority.NoSeniority"/>: QuantLib's <c>conventionalRecovery</c>
/// indexes its five-entry table by the seniority (<c>recoveryratequote.hpp:36-37</c>), and <c>NoSeniority</c> is 5,
/// past its end: an undefined read. The check is the table's five entries.
/// </summary>
public class ConventionalRecoveryNoSeniorityTests
{
    [Fact]
    public void NoSeniorityHasNoConventionalRecovery()
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => RecoveryRateQuote.ConventionalRecovery(Seniority.NoSeniority));

        Assert.Equal("seniority", e.ParamName);
        Assert.StartsWith("no conventional recovery for this seniority", e.Message, StringComparison.Ordinal);
        Assert.Equal(5, RecoveryRateQuote.MakeIsdaConvMap().Count);
    }
}

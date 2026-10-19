// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The interpolated cube's sections read the context the cube was built with. A section's at-the-money strike is a swap
/// index's fixing, which reads the evaluation date, the fixings and the at-par switch, and the volatility queries take
/// no context, so the cube keeps the one it was built with: a section's at-the-money level is the build context's
/// <see cref="SwaptionVolatilityCube.AtmStrike(Date, Period, EvaluationContext)"/>, whatever context the caller is in.
/// A cube built under another context answers with that one's.
/// </summary>
public class CubeBuildContextTests
{
    [Fact]
    public void TheInterpolatedCubesSectionsReadTheBuildContext()
    {
        EvaluationContext atPar = CubeFixture.Context;
        EvaluationContext indexed = atPar with { UseAtParCoupons = false };
        var cube = CubeFixture.Interpolated(context: atPar);
        Date optionDate = cube.OptionDateFromTenor(new Period(5, TimeUnit.Years));
        var swapTenor = new Period(10, TimeUnit.Years);

        double atParStrike = cube.AtmStrike(optionDate, swapTenor, atPar);
        double indexedStrike = cube.AtmStrike(optionDate, swapTenor, indexed);
        Assert.NotEqual(atParStrike, indexedStrike);

        // The section reads the build context, whichever context the caller holds.
        Assert.Equal(atParStrike, cube.SmileSection(optionDate, swapTenor).AtmLevel());

        // A cube built under the other context answers with it.
        var indexedCube = CubeFixture.Interpolated(context: indexed);
        Assert.Equal(indexedStrike, indexedCube.SmileSection(optionDate, swapTenor).AtmLevel());
    }
}

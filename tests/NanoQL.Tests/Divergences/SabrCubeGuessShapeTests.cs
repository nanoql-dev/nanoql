// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A SABR cube's parameter guesses with fewer rows than option and swap tenor pairs, or a row with fewer guesses than
/// the model's parameters. QuantLib reads <c>parametersGuessQuotes_[j*nSwapTenors_+k][i]</c> unchecked
/// (<c>sabrswaptionvolatilitycube.hpp:364-367, 377-382</c>): on QuantLib's 3 × 3 cube, 8 rows, a row of 3 guesses or no
/// rows end the process with an access violation at construction. nanoql refuses them with
/// <see cref="System.ArgumentException"/>, in the words of the volatility spreads' checks, and ignores rows and guesses
/// past those it reads, as QuantLib does.
/// </summary>
public class SabrCubeGuessShapeTests
{
    [Fact]
    public void FewerGuessesThanTheCubeReadsAreRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => CubeFixture.Sabr(guesses: CubeFixture.Guesses(8)));
        Assert.Equal("parametersGuess", e.ParamName);
        Assert.StartsWith(
            "mismatch between number of option tenors * swap tenors (9) and number of rows (8)", e.Message, StringComparison.Ordinal);

        double[][] shortRow = CubeFixture.Guesses();
        shortRow[0] = [0.2, 0.5, 0.4];
        e = Assert.Throws<ArgumentException>(() => CubeFixture.Sabr(guesses: shortRow));
        Assert.Equal("parametersGuess", e.ParamName);
        Assert.StartsWith(
            "mismatch between number of parameters (4) and number of columns (3) in the 1st row", e.Message, StringComparison.Ordinal);

        // More rows, and longer ones, are read as QuantLib reads them: the first nine, the first four guesses of each.
        double[][] longer = [.. CubeFixture.Guesses(10).Select(r => r.Concat([7.0]).ToArray())];
        Assert.Equal(
            Flat(CubeFixture.Sabr(isAtmCalibrated: false).SparseSabrParameters()),
            Flat(CubeFixture.Sabr(guesses: longer, isAtmCalibrated: false).SparseSabrParameters()));
    }

    private static double[] Flat(Numerics.Matrix m) => [.. Enumerable.Range(0, m.Rows).SelectMany(i => m.Row(i).ToArray())];
}

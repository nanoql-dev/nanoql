// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A tree's state prices read past the rows its construction stored. QuantLib's <c>TreeLattice::statePrices</c> keeps
/// every row it computes in a <c>mutable</c> cache (<c>lattice.hpp:87, 112-117</c>), and <c>setSpread</c> changes only
/// the tree's spread (<c>onefactormodel.hpp:105-108</c>), so a row read before the spread is set keeps its unspread
/// values afterwards, and a row first read after it carries the spread. nanoql's lattices keep no row beyond those
/// their construction stored, and <c>SetSpread</c> returns a copy: the copy's later rows carry the spread whatever was
/// read before, so reading a row changes nothing.
/// </summary>
public class StatePriceCacheTests
{
    [Fact]
    public void ARowReadBeforeTheSpreadIsSetDoesntKeepItsUnspreadValues()
    {
        var curve = new FlatForward(new Date(15, Month.January, 2026), 0.04, new Actual365Fixed());
        var tree = (OneFactorModelShortRateTree)new HullWhite(curve).Tree(new TimeGrid(2.0, 8));
        int last = 8;

        OneFactorModelShortRateTree before = tree.SetSpread(0.01);

        // QuantLib's tree would cache this row, past the eight it stored as it fitted
        double[] unspread = tree.StatePrices(last).ToArray();
        OneFactorModelShortRateTree after = tree.SetSpread(0.01);

        // QuantLib's statePrices(last) after setSpread is `unspread`; nanoql's copy computes it with the spread
        Assert.Equal(before.StatePrices(last).ToArray(), after.StatePrices(last).ToArray());
        Assert.NotEqual(unspread, after.StatePrices(last).ToArray());
        Assert.Equal(unspread, tree.StatePrices(last).ToArray());

        // the rows the fit stored are shared, unspread, as QuantLib's cache holds them
        Assert.Equal(tree.StatePrices(last - 1).ToArray(), after.StatePrices(last - 1).ToArray());
    }
}

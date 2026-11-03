// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An event set's simulation from 29 February: its periods take 28 February in years without one, where QuantLib
/// throws ("day outside month (2) day-range [1,28]"). On events of 100 on 1 March 2001, 50 on 15 June 2002, 70 on 28
/// February 2003, 30 on 1 March 2004 and 20 on 10 January 2006, covering 2001 to 2010, the simulation from 28 February
/// 2012 to 28 February 2014 reads in QuantLib the paths (serial numbers) 40969 100, 41440 50, 41698 70; 41334 30;
/// 41284 20; and an empty one, then none.
/// </summary>
public class EventSetTests
{
    [Fact]
    public void ASimulationFrom29FebruaryIsQuantLibsFrom28February()
    {
        var events = new EventSet(
            [
                new(new Date(1, March, 2001), 100.0),
                new(new Date(15, June, 2002), 50.0),
                new(new Date(28, February, 2003), 70.0),
                new(new Date(1, March, 2004), 30.0),
                new(new Date(10, January, 2006), 20.0),
            ],
            new Date(1, January, 2001),
            new Date(31, December, 2010));
        CatSimulation simulation = events.NewSimulation(new Date(29, February, 2012), new Date(28, February, 2014));

        (int Serial, double Loss)[][] quantLibs =
        [
            [(40969, 100), (41440, 50), (41698, 70)],
            [(41334, 30)],
            [(41284, 20)],
            [],
        ];
        var path = new List<KeyValuePair<Date, double>>();
        foreach ((int Serial, double Loss)[] expected in quantLibs)
        {
            Assert.True(simulation.NextPath(path));
            Assert.Equal(expected, path.Select(e => (e.Key.SerialNumber, e.Value)).ToArray());
        }

        Assert.False(simulation.NextPath(path));
    }
}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's multi-strike mesher vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.json</c>: the mesher's
/// locations, dplus and dminus on two markets at two sizes, three maturities and four strike sets, other epsilons and
/// scale factors, and concentration points inside and outside the grid.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmBlackScholesMultiStrikeMesherCxxVectorTests
{
    [Fact]
    public void FdmBlackScholesMultiStrikeMesherCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date reference = c.DateInput("referenceDate");
            DayCounter dc = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            var process = new GeneralizedBlackScholesProcess(
                c.Double("spot"),
                new FlatForward(reference, c.Double("dividendYield"), dc),
                new FlatForward(reference, c.Double("riskFreeRate"), dc),
                new BlackConstantVol(reference, new NullCalendar(), c.Double("volatility"), dc));
            JsonElement cPoint = c.Input("cPoint");
            var mesher = new FdmBlackScholesMultiStrikeMesher(
                c.Int("size"),
                process,
                c.Double("maturity"),
                Doubles(c.Input("strikes")),
                c.Double("eps"),
                c.Double("scaleFactor"),
                cPoint.ValueKind == JsonValueKind.Null ? default : (cPoint[0].GetDouble(), cPoint[1].GetDouble()));
            (double[] locations, double?[] dplus, double?[] dminus) = Read(mesher);
            check.Output(c, "locations", locations);
            check.Output(c, "dplus", dplus);
            check.Output(c, "dminus", dminus);
        }

        check.Complete();
    }
}

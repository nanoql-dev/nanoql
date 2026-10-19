// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SABR function vectors, <c>tests/vectors/termstructures/volatility/sabr.json</c>: on random and edge parameter
/// sets, <see cref="SabrFunctions.ShiftedSabrVolatility"/>, <see cref="SabrFunctions.SabrVolatility"/> and
/// <see cref="SabrFunctions.SabrFlochKennedyVolatility"/> at their strikes; <see cref="SabrFunctions.SabrGuess"/> on
/// the random sets' smiles, QuantLib's <c>testSabrGuess</c> table and smiles given directly, through every branch of
/// Boost's root finders; and each function on lists of arguments, the refusals among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SabrVectorTests
{
    [Fact]
    public void SabrVector()
    {
        var file = VectorFile.Load("termstructures/volatility/sabr");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "grid":
                    CheckVolatilities(check, c);
                    CheckGuess(check, c, "smileStrikes");
                    break;
                case "edge":
                    CheckVolatilities(check, c);
                    break;
                case "guess":
                    CheckGuess(check, c, "strikes");
                    break;
                case "sabrArguments":
                    check.Output(c, "sabrVolatility", Arguments(c, a => SabrFunctions.SabrVolatility(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6], (VolatilityType)(int)a[7])));
                    break;
                case "shiftedArguments":
                    check.Output(c, "shiftedSabrVolatility", Arguments(c, a => SabrFunctions.ShiftedSabrVolatility(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], (VolatilityType)(int)a[8])));
                    break;
                case "flochKennedyArguments":
                    check.Output(c, "sabrFlochKennedyVolatility", Arguments(c, a => SabrFunctions.SabrFlochKennedyVolatility(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6])));
                    break;
                case "guessArguments":
                    check.Output(c, "sabrGuess", Arguments(c, a => SabrFunctions.SabrGuess(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9], (VolatilityType)(int)a[10])));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckVolatilities(VectorAssert check, VectorCase c)
    {
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        double f = c.Double("forward"), t = c.Double("expiryTime"), shift = c.Double("shift");
        double alpha = c.Double("alpha"), beta = c.Double("beta"), nu = c.Double("nu"), rho = c.Double("rho");
        var type = (VolatilityType)c.Int("volatilityType");
        check.Output(c, "shiftedSabrVolatility", AtStrikes(
            strikes, k => SabrFunctions.ShiftedSabrVolatility(k, f, t, alpha, beta, nu, rho, shift, type)));
        check.Output(c, "sabrVolatility", AtStrikes(
            strikes, k => SabrFunctions.SabrVolatility(k, f, t, alpha, beta, nu, rho, type)));
        if (type == VolatilityType.ShiftedLognormal)
        {
            check.Output(c, "sabrFlochKennedyVolatility", AtStrikes(
                strikes, k => SabrFunctions.SabrFlochKennedyVolatility(k, f, t, alpha, beta, nu, rho)));
        }
    }

    private static void CheckGuess(VectorAssert check, VectorCase c, string strikesInput)
    {
        double[] k = VolatilityInputs.Reals(c.Input(strikesInput));
        double[] smile = VolatilityInputs.Reals(c.Input("smile"));
        check.Output(c, "sabrGuess", () => SabrFunctions.SabrGuess(
            k[0],
            smile[0],
            k[1],
            smile[1],
            k[2],
            smile[2],
            c.Double("forward"),
            c.Double("expiryTime"),
            c.Double("beta"),
            c.Double("shift"),
            (VolatilityType)c.Int("volatilityType")));
    }

    // Each list of arguments, its value computed when compared, so that a throw is checked.
    private static Func<object?>[] Arguments(VectorCase c, Func<double[], object> f) =>
        [.. c.Input("arguments").EnumerateArray().Select(a => (Func<object?>)(() => f(VolatilityInputs.Reals(a))))];

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];
}

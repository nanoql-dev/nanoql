// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SABR function vectors, <c>tests/vectors/cxx/termstructures/volatility/sabr.json</c>: the four
/// expansions that check nothing, <see cref="SabrFunctions.UnsafeSabrLogNormalVolatility"/>,
/// <see cref="SabrFunctions.UnsafeSabrNormalVolatility"/>, <see cref="SabrFunctions.UnsafeShiftedSabrVolatility"/> and
/// <see cref="SabrFunctions.UnsafeSabrVolatility"/>, on random and edge parameter sets and on arguments the checking
/// functions refuse; and <see cref="SabrFunctions.ValidateSabrParameters"/>, null where it accepts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SabrCxxVectorTests
{
    [Fact]
    public void SabrCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/sabr");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "grid" or "edge":
                    CheckExpansions(check, c);
                    break;
                case "arguments":
                    check.Output(c, "unsafeSabrLogNormalVolatility", Arguments(c, a => SabrFunctions.UnsafeSabrLogNormalVolatility(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6])));
                    check.Output(c, "unsafeSabrNormalVolatility", Arguments(c, a => SabrFunctions.UnsafeSabrNormalVolatility(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6])));
                    check.Output(c, "unsafeShiftedSabrVolatility", Arguments(c, a => SabrFunctions.UnsafeShiftedSabrVolatility(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], (VolatilityType)(int)a[8])));
                    check.Output(c, "unsafeSabrVolatility", Arguments(c, a => SabrFunctions.UnsafeSabrVolatility(
                        a[0], a[1], a[2], a[3], a[4], a[5], a[6], (VolatilityType)(int)a[8])));
                    break;
                case "validate":
                    check.Output(c, "validateSabrParameters", Arguments(c, a =>
                    {
                        SabrFunctions.ValidateSabrParameters(a[0], a[1], a[2], a[3]);
                        return null;
                    }));
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckExpansions(VectorAssert check, VectorCase c)
    {
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        double f = c.Double("forward"), t = c.Double("expiryTime"), shift = c.Double("shift");
        double alpha = c.Double("alpha"), beta = c.Double("beta"), nu = c.Double("nu"), rho = c.Double("rho");
        var type = (VolatilityType)c.Int("volatilityType");
        check.Output(c, "unsafeSabrLogNormalVolatility", AtStrikes(
            strikes, k => SabrFunctions.UnsafeSabrLogNormalVolatility(k, f, t, alpha, beta, nu, rho)));
        check.Output(c, "unsafeSabrNormalVolatility", AtStrikes(
            strikes, k => SabrFunctions.UnsafeSabrNormalVolatility(k, f, t, alpha, beta, nu, rho)));
        check.Output(c, "unsafeShiftedSabrVolatility", AtStrikes(
            strikes, k => SabrFunctions.UnsafeShiftedSabrVolatility(k, f, t, alpha, beta, nu, rho, shift, type)));
        check.Output(c, "unsafeSabrVolatility", AtStrikes(
            strikes, k => SabrFunctions.UnsafeSabrVolatility(k, f, t, alpha, beta, nu, rho, type)));
    }

    // Each list of arguments, its value computed when compared, so that a throw is checked.
    private static Func<object?>[] Arguments(VectorCase c, Func<double[], object?> f) =>
        [.. c.Input("arguments").EnumerateArray().Select(a => (Func<object?>)(() => f(VolatilityInputs.Reals(a))))];

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];
}

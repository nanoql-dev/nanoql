// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Bates models derive from <see cref="HestonModel"/> as QuantLib's do (the S-hes plan's HR1): no constructor calls
/// the virtual <c>GenerateArguments</c>, which in C# would run a derived override before the derived constructor has set
/// its arguments (CA2214); a Bates model builds its own process from its own arguments; and
/// <see cref="CalibratedModel.WithParams"/> keeps the derived type and rebuilds its process.
/// </summary>
public class HestonModelDerivationTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, A365);

    [Fact]
    public void HestonModelsConstructorsCallNoVirtual()
    {
        var process = new HestonProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.05, 0.3, -0.5);

        var model = new CountingHestonModel(process);

        Assert.Equal(0, model.GenerateArgumentsCalls);
        Assert.Equal(0.05, model.Process.Theta);
        Assert.IsType<HestonProcess>(model.Process);

        // WithParams runs the override on the copy, its arguments the new ones
        var copy = (CountingHestonModel)model.WithParams(new Vector([0.06, 1.5, 0.3, -0.5, 0.04]));
        Assert.Equal(1, copy.GenerateArgumentsCalls);
        Assert.Equal(0.06, copy.Process.Theta);
        Assert.Equal(0, model.GenerateArgumentsCalls);
    }

    [Fact]
    public void ABatesModelBuildsItsProcessFromItsOwnArguments()
    {
        var process = new BatesProcess(
            RiskFree, Dividends, 100.0, 0.04, 1.5, 0.05, 0.3, -0.5, 1.1, -0.12, 0.17, HestonProcessDiscretization.QuadraticExponential);

        var model = new BatesModel(process);

        var built = Assert.IsType<BatesProcess>(model.Process);
        Assert.Equal(1.1, built.Lambda);
        Assert.Equal(-0.12, built.Nu);
        Assert.Equal(0.17, built.Delta);
        Assert.Equal(0.05, built.Theta);
        Assert.Equal(8, model.Arguments.Length);

        var detJump = new BatesDetJumpModel(process, 0.7, 0.2);
        Assert.IsType<BatesProcess>(detJump.Process);
        Assert.Equal(10, detJump.Arguments.Length);
        Assert.Equal(0.7, detJump.KappaLambda);
        Assert.Equal(0.2, detJump.ThetaLambda);

        // the double-exponential models keep the Heston model's process, as QuantLib's
        var doubleExp = new BatesDoubleExpModel(process, 0.3, 0.2, 0.1, 0.4);
        Assert.Equal(typeof(HestonProcess), doubleExp.Process.GetType());
        Assert.Equal(9, doubleExp.Arguments.Length);
        Assert.Equal(11, new BatesDoubleExpDetJumpModel(process).Arguments.Length);
    }

    [Fact]
    public void WithParamsKeepsTheBatesTypeAndRebuildsItsProcess()
    {
        var process = new BatesProcess(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.05, 0.3, -0.5, 1.1, -0.12, 0.17);
        var model = new BatesModel(process);

        var copy = Assert.IsType<BatesModel>(model.WithParams(new Vector([0.06, 2.0, 0.4, -0.6, 0.05, -0.1, 0.2, 0.9])));

        var rebuilt = Assert.IsType<BatesProcess>(copy.Process);
        Assert.Equal(0.06, rebuilt.Theta);
        Assert.Equal(2.0, rebuilt.Kappa);
        Assert.Equal(0.9, rebuilt.Lambda);
        Assert.Equal(-0.1, rebuilt.Nu);
        Assert.Equal(0.2, rebuilt.Delta);

        // the receiver doesn't change
        Assert.Equal(1.1, ((BatesProcess)model.Process).Lambda);

        var detJump = new BatesDetJumpModel(process, 0.7, 0.2);
        var detCopy = Assert.IsType<BatesDetJumpModel>(
            detJump.WithParams(new Vector([0.06, 2.0, 0.4, -0.6, 0.05, -0.1, 0.2, 0.9, 0.8, 0.3])));
        Assert.Equal(0.8, detCopy.KappaLambda);
        Assert.Equal(0.9, Assert.IsType<BatesProcess>(detCopy.Process).Lambda);
    }

    // A derived model counting its GenerateArguments calls: QuantLib's derived models override it.
    private sealed class CountingHestonModel : HestonModel
    {
        public CountingHestonModel(HestonProcess process)
            : base(process, 5)
        {
        }

        public int GenerateArgumentsCalls { get; private set; }

        protected override void GenerateArguments()
        {
            GenerateArgumentsCalls++;
            base.GenerateArguments();
        }
    }
}

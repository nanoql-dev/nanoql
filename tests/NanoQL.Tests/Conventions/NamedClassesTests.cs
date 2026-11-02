// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib-Python's <c>%template</c> names are sealed subclasses of the closed generic class they name (playbook §2.6):
/// the nine generators and the two path generators, each with a <c>Clone()</c> that returns its own type; and the
/// named curves, iterative and global, each piecewise one with a <c>Create</c> that returns its own type; and the ZABR
/// sections, each with a <c>Create</c> that returns its own type, and the Runge-Kutta integrator; and the named
/// engines, static classes of their generic's calculations closed.
/// </summary>
public class NamedClassesTests
{
    [Fact]
    public void EachNamedClassIsASubclassOfItsClosedGeneric()
    {
        (Type Named, Type Generic)[] classes =
        [
            (typeof(MersenneTwisterUniformRsg), typeof(RandomSequenceGenerator<MersenneTwisterUniformRng>)),
            (typeof(InvCumulativeMersenneTwisterGaussianRng),
                typeof(InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeMersenneTwisterGaussianRng),
                typeof(InverseCumulativeRng<MersenneTwisterUniformRng, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeMersenneTwisterGaussianRsg),
                typeof(InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeMersenneTwisterGaussianRsg),
                typeof(InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeHaltonGaussianRsg), typeof(InverseCumulativeRsg<HaltonRsg, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeHaltonGaussianRsg), typeof(InverseCumulativeRsg<HaltonRsg, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeSobolGaussianRsg), typeof(InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>)),
            (typeof(MoroInvCumulativeSobolGaussianRsg), typeof(InverseCumulativeRsg<SobolRsg, MoroInverseCumulativeNormal>)),
            (typeof(InvCumulativeMersenneTwisterPathGenerator),
                typeof(PathGenerator<InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>>)),
            (typeof(GaussianSobolPathGenerator), typeof(PathGenerator<InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>>)),
        ];
        foreach ((Type named, Type generic) in classes)
        {
            Assert.True(named.IsSealed, named.Name);
            Assert.Equal(generic, named.BaseType);
            MethodInfo clone = named.GetMethod(
                "Clone", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, Type.EmptyTypes)!;
            Assert.Equal(named, clone.ReturnType);
        }
    }

    [Fact]
    public void EachNamedCurveIsASubclassOfItsClosedGeneric()
    {
        // the iterative piecewise curves and the interpolated curves (SD5); each piecewise one hides the generic's
        // Create with its own, returning its own type
        (Type Named, Type Generic)[] piecewise =
        [
            (typeof(PiecewiseLinearSimpleZero), typeof(PiecewiseYieldCurve<SimpleZeroYield, Linear, IterativeBootstrap>)),
            (typeof(PiecewiseFlatForward), typeof(PiecewiseYieldCurve<ForwardRate, BackwardFlat, IterativeBootstrap>)),
            (typeof(PiecewiseLogLinearDiscount), typeof(PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>)),
            (typeof(PiecewiseLinearForward), typeof(PiecewiseYieldCurve<ForwardRate, Linear, IterativeBootstrap>)),
            (typeof(PiecewiseLinearZero), typeof(PiecewiseYieldCurve<ZeroYield, Linear, IterativeBootstrap>)),
            (typeof(PiecewiseCubicZero), typeof(PiecewiseYieldCurve<ZeroYield, Cubic, IterativeBootstrap>)),
            (typeof(PiecewiseLogCubicDiscount), typeof(PiecewiseYieldCurve<Discount, LogCubic, IterativeBootstrap>)),
            (typeof(PiecewiseKrugerLogDiscount), typeof(PiecewiseYieldCurve<Discount, KrugerLog, IterativeBootstrap>)),
            (typeof(PiecewiseLogMixedLinearCubicDiscount),
                typeof(PiecewiseYieldCurve<Discount, LogMixedLinearCubic, IterativeBootstrap>)),
        ];
        (Type Named, Type Generic)[] interpolated =
        [
            (typeof(DiscountCurve), typeof(InterpolatedDiscountCurve<LogLinear>)),
            (typeof(LogCubicDiscountCurve), typeof(InterpolatedDiscountCurve<LogCubic>)),
            (typeof(KrugerLogDiscountCurve), typeof(InterpolatedDiscountCurve<KrugerLog>)),
            (typeof(LogMixedLinearCubicDiscountCurve), typeof(InterpolatedDiscountCurve<LogMixedLinearCubic>)),
            (typeof(ZeroCurve), typeof(InterpolatedZeroCurve<Linear>)),
            (typeof(CubicZeroCurve), typeof(InterpolatedZeroCurve<Cubic>)),
            (typeof(SimpleZeroCurve), typeof(InterpolatedSimpleZeroCurve<Linear>)),
            (typeof(ForwardCurve), typeof(InterpolatedForwardCurve<BackwardFlat>)),
            (typeof(LinearForwardCurve), typeof(InterpolatedForwardCurve<Linear>)),
            (typeof(CubicForwardCurve), typeof(InterpolatedForwardCurve<Cubic>)),
        ];
        foreach ((Type named, Type generic) in piecewise.Concat(interpolated))
        {
            Assert.True(named.IsSealed, named.Name);
            Assert.Equal(generic, named.BaseType);
        }

        foreach ((Type named, _) in piecewise)
        {
            MethodInfo create = named.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.Equal(named, create.ReturnType);
        }
    }

    [Fact]
    public void EachGlobalNamedCurveIsASubclassOfItsClosedGeneric()
    {
        (Type Named, Type Generic)[] classes =
        [
            (typeof(GlobalLinearSimpleZeroCurve), typeof(PiecewiseYieldCurve<SimpleZeroYield, Linear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseFlatForward), typeof(PiecewiseYieldCurve<ForwardRate, BackwardFlat, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLogLinearDiscount), typeof(PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLinearForward), typeof(PiecewiseYieldCurve<ForwardRate, Linear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLinearZero), typeof(PiecewiseYieldCurve<ZeroYield, Linear, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseCubicZero), typeof(PiecewiseYieldCurve<ZeroYield, Cubic, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLogCubicDiscount), typeof(PiecewiseYieldCurve<Discount, LogCubic, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseSplineCubicDiscount), typeof(PiecewiseYieldCurve<Discount, Cubic, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseKrugerLogDiscount), typeof(PiecewiseYieldCurve<Discount, KrugerLog, GlobalBootstrap>)),
            (typeof(GlobalPiecewiseLogMixedLinearCubicDiscount),
                typeof(PiecewiseYieldCurve<Discount, LogMixedLinearCubic, GlobalBootstrap>)),
        ];
        foreach ((Type named, Type generic) in classes)
        {
            Assert.True(named.IsSealed, named.Name);
            Assert.Equal(generic, named.BaseType);
            MethodInfo create = named.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.Equal(named, create.ReturnType);
        }
    }

    [Fact]
    public void EachNamedZabrClassIsASubclassOfItsClosedGeneric()
    {
        // the ZABR sections, each with a Create that returns its own type, and the Runge-Kutta integrator
        (Type Named, Type Generic)[] sections =
        [
            (typeof(ZabrShortMaturityLognormalSmileSection), typeof(ZabrSmileSection<ZabrShortMaturityLognormal>)),
            (typeof(ZabrShortMaturityNormalSmileSection), typeof(ZabrSmileSection<ZabrShortMaturityNormal>)),
        ];
        foreach ((Type named, Type generic) in sections.Append((typeof(RungeKutta), typeof(AdaptiveRungeKutta<double>))))
        {
            Assert.True(named.IsSealed, named.Name);
            Assert.Equal(generic, named.BaseType);
        }

        foreach ((Type named, _) in sections)
        {
            MethodInfo create = named.GetMethod("Create", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.Equal(named, create.ReturnType);
        }
    }

    [Fact]
    public void EachNamedEngineIsItsGenericsCalculationsClosed()
    {
        // QuantLib-Python's engine names, and the six the wheel doesn't name, named as their siblings are: static
        // classes whose calculations are their generic's closed at the named arguments, with its parameters, names and
        // defaults, and experimental where their generic is
        (Type Named, Type Generic, Type[] Closing)[] engines =
        [
            (typeof(MCPRDoubleBarrierEngine), typeof(MCDoubleBarrierEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDDoubleBarrierEngine), typeof(MCDoubleBarrierEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPREverestEngine), typeof(MCEverestEngine), [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDEverestEngine), typeof(MCEverestEngine), [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRHimalayaEngine), typeof(MCHimalayaEngine), [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDHimalayaEngine), typeof(MCHimalayaEngine), [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRPagodaEngine), typeof(MCPagodaEngine), [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDPagodaEngine), typeof(MCPagodaEngine), [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRPathBasketEngine), typeof(MCPathBasketEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDPathBasketEngine), typeof(MCPathBasketEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRDiscreteArithmeticAPEngine), typeof(MCDiscreteArithmeticAPEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDDiscreteArithmeticAPEngine), typeof(MCDiscreteArithmeticAPEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRDiscreteArithmeticAPHestonEngine), typeof(MCDiscreteArithmeticAPHestonEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDDiscreteArithmeticAPHestonEngine), typeof(MCDiscreteArithmeticAPHestonEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRDiscreteArithmeticASEngine), typeof(MCDiscreteArithmeticASEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDDiscreteArithmeticASEngine), typeof(MCDiscreteArithmeticASEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRDiscreteGeometricAPEngine), typeof(MCDiscreteGeometricAPEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDDiscreteGeometricAPEngine), typeof(MCDiscreteGeometricAPEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRDiscreteGeometricAPHestonEngine), typeof(MCDiscreteGeometricAPHestonEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDDiscreteGeometricAPHestonEngine), typeof(MCDiscreteGeometricAPHestonEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRBarrierEngine), typeof(MCBarrierEngine), [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDBarrierEngine), typeof(MCBarrierEngine), [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRAmericanBasketEngine), typeof(MCAmericanBasketEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDAmericanBasketEngine), typeof(MCAmericanBasketEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPREuropeanBasketEngine), typeof(MCEuropeanBasketEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDEuropeanBasketEngine), typeof(MCEuropeanBasketEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRHullWhiteCapFloorEngine), typeof(MCHullWhiteCapFloorEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDHullWhiteCapFloorEngine), typeof(MCHullWhiteCapFloorEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRPerformanceEngine), typeof(MCPerformanceEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDPerformanceEngine), typeof(MCPerformanceEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRForwardEuropeanBSEngine), typeof(MCForwardEuropeanBSEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDForwardEuropeanBSEngine), typeof(MCForwardEuropeanBSEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRForwardEuropeanHestonEngine), typeof(MCForwardEuropeanHestonEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDForwardEuropeanHestonEngine), typeof(MCForwardEuropeanHestonEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRVarianceSwapEngine), typeof(MCVarianceSwapEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDVarianceSwapEngine), typeof(MCVarianceSwapEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRLookbackEngine), typeof(MCLookbackEngine), [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDLookbackEngine), typeof(MCLookbackEngine), [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRAmericanEngine), typeof(MCAmericanEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics), typeof(PseudoRandom)]),
            (typeof(MCLDAmericanEngine), typeof(MCAmericanEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics), typeof(LowDiscrepancy)]),
            (typeof(MCPRDigitalEngine), typeof(MCDigitalEngine), [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDDigitalEngine), typeof(MCDigitalEngine), [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPREuropeanEngine), typeof(MCEuropeanEngine), [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDEuropeanEngine), typeof(MCEuropeanEngine), [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPREuropeanGJRGARCHEngine), typeof(MCEuropeanGJRGARCHEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDEuropeanGJRGARCHEngine), typeof(MCEuropeanGJRGARCHEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPREuropeanHestonEngine), typeof(MCEuropeanHestonEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDEuropeanHestonEngine), typeof(MCEuropeanHestonEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(MCPRHestonHullWhiteEngine), typeof(MCHestonHullWhiteEngine),
                [typeof(PseudoRandom), typeof(GeneralStatistics)]),
            (typeof(MCLDHestonHullWhiteEngine), typeof(MCHestonHullWhiteEngine),
                [typeof(LowDiscrepancy), typeof(GeneralStatistics)]),
            (typeof(BinomialCRRBarrierEngine), typeof(BinomialBarrierEngine),
                [typeof(CoxRossRubinstein), typeof(DiscretizedDermanKaniBarrierOption)]),
            (typeof(BinomialJRBarrierEngine), typeof(BinomialBarrierEngine),
                [typeof(JarrowRudd), typeof(DiscretizedDermanKaniBarrierOption)]),
            (typeof(BinomialEQPBarrierEngine), typeof(BinomialBarrierEngine),
                [typeof(AdditiveEQPBinomialTree), typeof(DiscretizedDermanKaniBarrierOption)]),
            (typeof(BinomialTrigeorgisBarrierEngine), typeof(BinomialBarrierEngine),
                [typeof(Trigeorgis), typeof(DiscretizedDermanKaniBarrierOption)]),
            (typeof(BinomialTianBarrierEngine), typeof(BinomialBarrierEngine),
                [typeof(Tian), typeof(DiscretizedDermanKaniBarrierOption)]),
            (typeof(BinomialLRBarrierEngine), typeof(BinomialBarrierEngine),
                [typeof(LeisenReimer), typeof(DiscretizedDermanKaniBarrierOption)]),
            (typeof(BinomialJ4BarrierEngine), typeof(BinomialBarrierEngine),
                [typeof(Joshi4), typeof(DiscretizedDermanKaniBarrierOption)]),
            (typeof(BinomialCRRDoubleBarrierEngine), typeof(BinomialDoubleBarrierEngine),
                [typeof(CoxRossRubinstein), typeof(DiscretizedDermanKaniDoubleBarrierOption)]),
            (typeof(BinomialJRDoubleBarrierEngine), typeof(BinomialDoubleBarrierEngine),
                [typeof(JarrowRudd), typeof(DiscretizedDermanKaniDoubleBarrierOption)]),
            (typeof(BinomialEQPDoubleBarrierEngine), typeof(BinomialDoubleBarrierEngine),
                [typeof(AdditiveEQPBinomialTree), typeof(DiscretizedDermanKaniDoubleBarrierOption)]),
            (typeof(BinomialTrigeorgisDoubleBarrierEngine), typeof(BinomialDoubleBarrierEngine),
                [typeof(Trigeorgis), typeof(DiscretizedDermanKaniDoubleBarrierOption)]),
            (typeof(BinomialTianDoubleBarrierEngine), typeof(BinomialDoubleBarrierEngine),
                [typeof(Tian), typeof(DiscretizedDermanKaniDoubleBarrierOption)]),
            (typeof(BinomialLRDoubleBarrierEngine), typeof(BinomialDoubleBarrierEngine),
                [typeof(LeisenReimer), typeof(DiscretizedDermanKaniDoubleBarrierOption)]),
            (typeof(BinomialJ4DoubleBarrierEngine), typeof(BinomialDoubleBarrierEngine),
                [typeof(Joshi4), typeof(DiscretizedDermanKaniDoubleBarrierOption)]),
        ];
        foreach ((Type named, Type generic, Type[] closing) in engines)
        {
            Assert.True(named.IsAbstract && named.IsSealed, named.Name);
            Assert.Equal(generic.IsDefined(typeof(ExperimentalAttribute)), named.IsDefined(typeof(ExperimentalAttribute)));
            MethodInfo[] closed =
            [
                .. generic.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Calculate" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == closing.Length)
                    .Select(m => m.MakeGenericMethod(closing)),
            ];
            MethodInfo[] calculations = named.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.Equal(closed.Length, calculations.Length);
            foreach (MethodInfo m in calculations)
            {
                Assert.Equal("Calculate", m.Name);
                MethodInfo g = Assert.Single(closed, g => g.GetParameters().Select(q => q.ParameterType).SequenceEqual(m.GetParameters().Select(q => q.ParameterType)));
                Assert.Equal(g.ReturnType, m.ReturnType);
                Assert.Equal(g.GetParameters().Select(Signature), m.GetParameters().Select(Signature));
            }
        }
    }

    [Fact]
    public void TheEngineCalculationsNoOracleTestCallsGiveTheirClosingsResults()
    {
        // the named calculations no vector or ported test calls, where those reach their closings through the builders
        // and the divergence tests: the low-discrepancy partial lookbacks and the Heston engines on a stochastic local
        // volatility process, each its generic's closing bit for bit
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var r = new FlatForward(today, 0.05, dc);
        var q = new FlatForward(today, 0.02, dc);
        var process = new BlackScholesMertonProcess(100.0, q, r, new BlackConstantVol(today, new NullCalendar(), 0.20, dc));
        var exercise = new EuropeanExercise(today + 365);
        var partialFixed = new ContinuousPartialFixedLookbackOption(
            today + 182, new PlainVanillaPayoff(OptionType.Call, 100.0), exercise);
        var partialFloating = new ContinuousPartialFloatingLookbackOption(
            100.0, 1.0, today + 182, new FloatingTypePayoff(OptionType.Call), exercise);
        Same(
            MCLookbackEngine.Calculate<LowDiscrepancy, GeneralStatistics>(partialFixed, context, process, 8, null, false, false, 255, null, null, 42),
            MCLDLookbackEngine.Calculate(partialFixed, context, process, 8, null, false, false, 255, null, null, 42));
        Same(
            MCLookbackEngine.Calculate<LowDiscrepancy, GeneralStatistics>(partialFloating, context, process, 8, null, false, false, 255, null, null, 42),
            MCLDLookbackEngine.Calculate(partialFloating, context, process, 8, null, false, false, 255, null, null, 42));

        var slv = new HestonSLVProcess(new HestonProcess(r, q, 100.0, 0.04, 1.5, 0.04, 0.3, -0.7), new LocalConstantVol(today, 0.25, dc));
        var vanilla = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), exercise);
        Same(
            MCEuropeanHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(vanilla, context, slv, null, 12, true, 255, null, null, 1234),
            MCPREuropeanHestonEngine.Calculate(vanilla, context, slv, null, 12, true, 255, null, null, 1234));
        Same(
            MCEuropeanHestonEngine.Calculate<LowDiscrepancy, GeneralStatistics>(vanilla, context, slv, null, 12, false, 255, null, null, 1234),
            MCLDEuropeanHestonEngine.Calculate(vanilla, context, slv, null, 12, false, 255, null, null, 1234));

        static void Same(OneAssetOptionResults closing, OneAssetOptionResults named) =>
            Assert.Equal((closing.NPV, closing.ErrorEstimate), (named.NPV, named.ErrorEstimate));
    }

    private static (string? Name, bool HasDefault, object? Default) Signature(ParameterInfo p) =>
        (p.Name, p.HasDefaultValue, p.HasDefaultValue ? p.DefaultValue : null);
}

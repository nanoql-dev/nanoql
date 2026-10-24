// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="IXabrCubeModel.ExtractGamma"/>'s default is QuantLib's primary template's <c>extractGamma</c>, 0
/// (<c>sabrswaptionvolatilitycube.hpp:70</c>, <c>:103-106</c>): a four-parameter model, the SABR model, takes it, and the
/// cube reads it only for a model of five parameters (<c>:553</c>, <c>:692</c>), which overrides it, as the ZABR model
/// does. The default is read here through the SABR model on a fitted interpolation.
/// </summary>
public class XabrCubeModelGammaTests
{
    [Fact]
    public void AFourParameterModelsGammaIsZero()
    {
        IXabrInterpolation fitted = SwaptionVolCubeSabrModel.CreateInterpolation(
            [0.02, 0.03, 0.04],
            [0.21, 0.20, 0.205],
            1.0,
            0.03,
            [0.2, 0.5, 0.3, 0.0],
            [true, true, true, true],
            false,
            null,
            null,
            0.0020,
            false,
            0,
            0.0,
            VolatilityType.ShiftedLognormal);

        Assert.Equal(4, NParams<SwaptionVolCubeSabrModel>());
        Assert.Equal(0.0, Gamma<SwaptionVolCubeSabrModel>(fitted));
    }

    private static int NParams<TModel>()
        where TModel : IXabrCubeModel => TModel.NParams;

    private static double Gamma<TModel>(IXabrInterpolation interpolation)
        where TModel : IXabrCubeModel => TModel.ExtractGamma(interpolation);
}

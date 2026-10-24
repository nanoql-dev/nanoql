// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2003 Niels Elken Sønderby
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/kronrodintegral.hpp ql/math/integrals/kronrodintegral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The non-adaptive Gauss-Kronrod integration: QuantLib's 10-, 21-, 43- and 87-point rules in succession, each reusing
/// its predecessors' points, until an estimate is within the absolute or the relative accuracy; for fast integration of
/// smooth functions.
/// </summary>
/// <remarks>
/// <para>
/// The rules sample the integrand at 87 points at most, on QUADPACK's <c>qng</c> abscissae and weights (L. W.
/// Fullerton, Bell Labs, 1981). Each integration's result carries its error estimate,
/// <see cref="IntegrationResult.AbsoluteError"/>, and its number of evaluations, 21, 43 or 87, which QuantLib keeps
/// in the integrator's mutable members (<c>kronrodintegral.cpp:299-350</c>);
/// <see cref="Integrator.IntegrationSuccess"/> reads them.
/// </para>
/// <para>
/// The maximum number of evaluations isn't a bound here: the last rule's result is returned whatever its error, and
/// <see cref="Integrator.IntegrationSuccess"/> compares its count with the maximum, as QuantLib's does.
/// </para>
/// </remarks>
public sealed class GaussKronrodNonAdaptive : Integrator
{
    // x1, abscissae common to the 10-, 21-, 43- and 87-point rule
    private static readonly double[] X1 =
    [
        0.973906528517171720077964012084452,
        0.865063366688984510732096688423493,
        0.679409568299024406234327365114874,
        0.433395394129247190799265943165784,
        0.148874338981631210884826001129720,
    ];

    // w10, weights of the 10-point formula
    private static readonly double[] W10 =
    [
        0.066671344308688137593568809893332,
        0.149451349150580593145776339657697,
        0.219086362515982043995534934228163,
        0.269266719309996355091226921569469,
        0.295524224714752870173892994651338,
    ];

    // x2, abscissae common to the 21-, 43- and 87-point rule
    private static readonly double[] X2 =
    [
        0.995657163025808080735527280689003,
        0.930157491355708226001207180059508,
        0.780817726586416897063717578345042,
        0.562757134668604683339000099272694,
        0.294392862701460198131126603103866,
    ];

    // w21a, weights of the 21-point formula for abscissae x1
    private static readonly double[] W21a =
    [
        0.032558162307964727478818972459390,
        0.075039674810919952767043140916190,
        0.109387158802297641899210590325805,
        0.134709217311473325928054001771707,
        0.147739104901338491374841515972068,
    ];

    // w21b, weights of the 21-point formula for abscissae x2
    private static readonly double[] W21b =
    [
        0.011694638867371874278064396062192,
        0.054755896574351996031381300244580,
        0.093125454583697605535065465083366,
        0.123491976262065851077958109831074,
        0.142775938577060080797094273138717,
        0.149445554002916905664936468389821,
    ];

    // x3, abscissae common to the 43- and 87-point rule
    private static readonly double[] X3 =
    [
        0.999333360901932081394099323919911,
        0.987433402908088869795961478381209,
        0.954807934814266299257919200290473,
        0.900148695748328293625099494069092,
        0.825198314983114150847066732588520,
        0.732148388989304982612354848755461,
        0.622847970537725238641159120344323,
        0.499479574071056499952214885499755,
        0.364901661346580768043989548502644,
        0.222254919776601296498260928066212,
        0.074650617461383322043914435796506,
    ];

    // w43a, weights of the 43-point formula for abscissae x1, x3
    private static readonly double[] W43a =
    [
        0.016296734289666564924281974617663,
        0.037522876120869501461613795898115,
        0.054694902058255442147212685465005,
        0.067355414609478086075553166302174,
        0.073870199632393953432140695251367,
        0.005768556059769796184184327908655,
        0.027371890593248842081276069289151,
        0.046560826910428830743339154433824,
        0.061744995201442564496240336030883,
        0.071387267268693397768559114425516,
    ];

    // w43b, weights of the 43-point formula for abscissae x3
    private static readonly double[] W43b =
    [
        0.001844477640212414100389106552965,
        0.010798689585891651740465406741293,
        0.021895363867795428102523123075149,
        0.032597463975345689443882222526137,
        0.042163137935191811847627924327955,
        0.050741939600184577780189020092084,
        0.058379395542619248375475369330206,
        0.064746404951445885544689259517511,
        0.069566197912356484528633315038405,
        0.072824441471833208150939535192842,
        0.074507751014175118273571813842889,
        0.074722147517403005594425168280423,
    ];

    // x4, abscissae of the 87-point rule
    private static readonly double[] X4 =
    [
        0.999902977262729234490529830591582,
        0.997989895986678745427496322365960,
        0.992175497860687222808523352251425,
        0.981358163572712773571916941623894,
        0.965057623858384619128284110607926,
        0.943167613133670596816416634507426,
        0.915806414685507209591826430720050,
        0.883221657771316501372117548744163,
        0.845710748462415666605902011504855,
        0.803557658035230982788739474980964,
        0.757005730685495558328942793432020,
        0.706273209787321819824094274740840,
        0.651589466501177922534422205016736,
        0.593223374057961088875273770349144,
        0.531493605970831932285268948562671,
        0.466763623042022844871966781659270,
        0.399424847859218804732101665817923,
        0.329874877106188288265053371824597,
        0.258503559202161551802280975429025,
        0.185695396568346652015917141167606,
        0.111842213179907468172398359241362,
        0.037352123394619870814998165437704,
    ];

    // w87a, weights of the 87-point formula for abscissae x1, x2, x3
    private static readonly double[] W87a =
    [
        0.008148377384149172900002878448190,
        0.018761438201562822243935059003794,
        0.027347451050052286161582829741283,
        0.033677707311637930046581056957588,
        0.036935099820427907614589586742499,
        0.002884872430211530501334156248695,
        0.013685946022712701888950035273128,
        0.023280413502888311123409291030404,
        0.030872497611713358675466394126442,
        0.035693633639418770719351355457044,
        0.000915283345202241360843392549948,
        0.005399280219300471367738743391053,
        0.010947679601118931134327826856808,
        0.016298731696787335262665703223280,
        0.021081568889203835112433060188190,
        0.025370969769253827243467999831710,
        0.029189697756475752501446154084920,
        0.032373202467202789685788194889595,
        0.034783098950365142750781997949596,
        0.036412220731351787562801163687577,
        0.037253875503047708539592001191226,
    ];

    // w87b, weights of the 87-point formula for abscissae x4
    private static readonly double[] W87b =
    [
        0.000274145563762072350016527092881,
        0.001807124155057942948341311753254,
        0.004096869282759164864458070683480,
        0.006758290051847378699816577897424,
        0.009549957672201646536053581325377,
        0.012329447652244853694626639963780,
        0.015010447346388952376697286041943,
        0.017548967986243191099665352925900,
        0.019938037786440888202278192730714,
        0.022194935961012286796332102959499,
        0.024339147126000805470360647041454,
        0.026374505414839207241503786552615,
        0.028286910788771200659968002987960,
        0.030052581128092695322521110347341,
        0.031646751371439929404586051078883,
        0.033050413419978503290785944862689,
        0.034255099704226061787082821046821,
        0.035262412660156681033782717998428,
        0.036076989622888701185500318003895,
        0.036698604498456094498018047441094,
        0.037120549269832576114119958413599,
        0.037334228751935040321235449094698,
        0.037361073762679023410321241766599,
    ];

    private double relativeAccuracy;

    /// <summary>Initializes a new instance of the <see cref="GaussKronrodNonAdaptive"/> class.</summary>
    /// <param name="absoluteAccuracy">The absolute accuracy, above machine epsilon.</param>
    /// <param name="maxEvaluations">The maximum number of evaluations, which
    /// <see cref="Integrator.IntegrationSuccess"/> compares with a result's count.</param>
    /// <param name="relativeAccuracy">The relative accuracy.</param>
    /// <exception cref="ArgumentException"><paramref name="absoluteAccuracy"/> isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEvaluations"/> is negative.</exception>
    public GaussKronrodNonAdaptive(double absoluteAccuracy, int maxEvaluations, double relativeAccuracy)
        : base(absoluteAccuracy, maxEvaluations)
    {
        this.relativeAccuracy = relativeAccuracy;
    }

    /// <summary>Gets the relative accuracy.</summary>
    public double RelativeAccuracy => relativeAccuracy;

    /// <summary>A copy of the integrator with the relative accuracy set; QuantLib's setter checks nothing.</summary>
    /// <param name="relativeAccuracy">The relative accuracy.</param>
    /// <returns>The modified copy.</returns>
    public GaussKronrodNonAdaptive SetRelativeAccuracy(double relativeAccuracy)
    {
        var copy = (GaussKronrodNonAdaptive)MemberwiseClone();
        copy.relativeAccuracy = relativeAccuracy;
        return copy;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The limits aren't ordered (a NaN limit).</exception>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        double result;

        Span<double> fv1 = stackalloc double[5], fv2 = stackalloc double[5], fv3 = stackalloc double[5],
            fv4 = stackalloc double[5];
        Span<double> savfun = stackalloc double[21]; // array of function values which have been computed
        double res10, res21, res43, res87; // 10, 21, 43 and 87 point results
        double err;
        double resAbs; // approximation to the integral of abs(f)
        double resasc; // approximation to the integral of abs(f-i/(b-a))
        int k;

        if (!(a < b))
        {
            throw new ArgumentException("b must be greater than a)", nameof(b));
        }

        double halfLength = 0.5 * (b - a);
        double center = 0.5 * (b + a);
        double fCenter = f(center);

        // Compute the integral using the 10- and 21-point formula.

        res10 = 0;
        res21 = W21b[5] * fCenter;
        resAbs = W21b[5] * Math.Abs(fCenter);

        for (k = 0; k < 5; k++)
        {
            double abscissa = halfLength * X1[k];
            double fval1 = f(center + abscissa);
            double fval2 = f(center - abscissa);
            double fval = fval1 + fval2;
            res10 += W10[k] * fval;
            res21 += W21a[k] * fval;
            resAbs += W21a[k] * (Math.Abs(fval1) + Math.Abs(fval2));
            savfun[k] = fval;
            fv1[k] = fval1;
            fv2[k] = fval2;
        }

        for (k = 0; k < 5; k++)
        {
            double abscissa = halfLength * X2[k];
            double fval1 = f(center + abscissa);
            double fval2 = f(center - abscissa);
            double fval = fval1 + fval2;
            res21 += W21b[k] * fval;
            resAbs += W21b[k] * (Math.Abs(fval1) + Math.Abs(fval2));
            savfun[k + 5] = fval;
            fv3[k] = fval1;
            fv4[k] = fval2;
        }

        result = res21 * halfLength;
        resAbs *= halfLength;
        double mean = 0.5 * res21;
        resasc = W21b[5] * Math.Abs(fCenter - mean);

        for (k = 0; k < 5; k++)
        {
            resasc += (W21a[k] * (Math.Abs(fv1[k] - mean) + Math.Abs(fv2[k] - mean)))
                + (W21b[k] * (Math.Abs(fv3[k] - mean) + Math.Abs(fv4[k] - mean)));
        }

        err = RescaleError((res21 - res10) * halfLength, resAbs, resasc);
        resasc *= halfLength;

        // test for convergence.
        if (err < AbsoluteAccuracy || err < relativeAccuracy * Math.Abs(result))
        {
            return new IntegrationResult(result, 21) { AbsoluteError = err };
        }

        // compute the integral using the 43-point formula.

        res43 = W43b[11] * fCenter;

        for (k = 0; k < 10; k++)
        {
            res43 += savfun[k] * W43a[k];
        }

        for (k = 0; k < 11; k++)
        {
            double abscissa = halfLength * X3[k];
            double fval = f(center + abscissa) + f(center - abscissa);
            res43 += fval * W43b[k];
            savfun[k + 10] = fval;
        }

        // test for convergence.

        result = res43 * halfLength;
        err = RescaleError((res43 - res21) * halfLength, resAbs, resasc);

        if (err < AbsoluteAccuracy || err < relativeAccuracy * Math.Abs(result))
        {
            return new IntegrationResult(result, 43) { AbsoluteError = err };
        }

        // compute the integral using the 87-point formula.

        res87 = W87b[22] * fCenter;

        for (k = 0; k < 21; k++)
        {
            res87 += savfun[k] * W87a[k];
        }

        for (k = 0; k < 22; k++)
        {
            double abscissa = halfLength * X4[k];
            res87 += W87b[k] * (f(center + abscissa) + f(center - abscissa));
        }

        // test for convergence.
        result = res87 * halfLength;
        err = RescaleError((res87 - res43) * halfLength, resAbs, resasc);

        return new IntegrationResult(result, 87) { AbsoluteError = err };
    }

    private static double RescaleError(double err, double resultAbs, double resultAsc)
    {
        err = Math.Abs(err);
        if (resultAsc != 0 && err != 0)
        {
            double scale = Math.Pow(200 * err / resultAsc, 1.5);
            if (scale < 1)
            {
                err = resultAsc * scale;
            }
            else
            {
                err = resultAsc;
            }
        }

        if (resultAbs > NumericLimits.QL_MIN_POSITIVE_REAL / (50 * NumericLimits.QL_EPSILON))
        {
            double min_err = 50 * NumericLimits.QL_EPSILON * resultAbs;
            if (min_err > err)
            {
                err = min_err;
            }
        }

        return err;
    }
}

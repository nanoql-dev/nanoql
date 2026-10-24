// SPDX-FileCopyrightText: Copyright John Maddock 2008.
// SPDX-FileCopyrightText: Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/distributions/non_central_chi_squared.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using static NanoQL.Numerics.CommonErrorHandlingFunctions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's non-central chi-squared distribution (<c>non_central_chi_squared.hpp</c>) for <c>double</c> under the
/// default policy: the constructor's checks, <c>pdf</c>, <c>cdf</c> and <c>quantile</c> with their complements. The
/// <c>cdf</c> sums Poisson-weighted central terms: from the Poisson weights' peak in both directions for its smaller
/// tail (D. Benton and K. Krishnamoorthy, Comput. Stat. Data Anal. 43, 2003), or forwards by Ding's method (Appl.
/// Stat. 41(2), 1992, AS 275) below a non-centrality of 200; the <c>pdf</c> is the Bessel form below a non-centrality
/// of 50 and the series above; the <c>quantile</c> roots the <c>cdf</c> from Pearson's approximation. Boost's
/// <c>non_central_chi_squared_distribution</c> holds only its degrees of freedom and non-centrality, so the functions
/// take them.
/// </summary>
/// <remarks>
/// Boost evaluates in <c>long double</c>, MSVC's <c>double</c>, and its names follow the type each check or error is
/// raised in: <c>double</c> for the <c>cdf</c>'s checks and the final narrowing, <c>long double</c> for the
/// <c>pdf</c>'s and the <c>quantile</c>'s checks, the series' errors, and the <c>cdf</c> the quantile roots. Under
/// the default policy a domain error on the caller's variate or probability throws
/// <see cref="ArgumentOutOfRangeException"/>; one on the distribution's parameters, an overflow and an evaluation
/// error throw <see cref="NanoQLException"/>, each with Boost's message.
/// </remarks>
internal static class NonCentralChiSquaredFunctions
{
    // policies::get_epsilon<long double>(), tools::min_value, max_value and log_max_value of double
    private const double Epsilon = 2.220446049250313E-16;
    private const double MinValue = 2.2250738585072014E-308;
    private const double MaxValue = double.MaxValue;
    private const double LogMaxValue = 709.0;

    /// <summary>
    /// <c>non_central_chi_squared_distribution(df, lambda)</c>'s constructor: it holds its two parameters, positive
    /// and finite degrees of freedom and a finite non-centrality not below 0.
    /// </summary>
    /// <param name="df">The degrees of freedom.</param>
    /// <param name="lambda">The non-centrality.</param>
    /// <exception cref="NanoQLException">A parameter is refused.</exception>
    internal static void NonCentralChiSquaredDistribution(double df, double lambda)
    {
        const string function =
            "boost::math::non_central_chi_squared_distribution<double>::non_central_chi_squared_distribution(double,double)";
        CheckDf(function, df);
        CheckNonCentrality(function, lambda);
    }

    /// <summary><c>pdf(non_central_chi_squared_distribution, x)</c>: the density at <paramref name="x"/>.</summary>
    /// <param name="k">The distribution's degrees of freedom.</param>
    /// <param name="l">The distribution's non-centrality.</param>
    /// <param name="x">The variate, finite and not below 0.</param>
    /// <returns>The density.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is negative or isn't finite.</exception>
    /// <exception cref="NanoQLException">A parameter is refused, the density overflows, or a series runs out of
    /// terms.</exception>
    internal static double Pdf(double k, double l, double x)
    {
        const string function = "pdf(non_central_chi_squared_distribution<long double>, long double)";

        CheckDf(function, k);
        CheckNonCentrality(function, l);
        CheckPositiveX(function, x, nameof(x));

        if (l == 0)
        {
            return ChiSquaredFunctions.Pdf(k, x);
        }

        // Special case:
        if (x == 0)
        {
            return 0;
        }

        double r;
        if (l > 50)
        {
            r = NonCentralChiSquarePdf(x, k, l);
        }
        else
        {
            r = Math.Log(x / l) * (k / 4 - (double)0.5f) - (x + l) / 2;
            if (Math.Abs(r) >= LogMaxValue / 4)
            {
                r = NonCentralChiSquarePdf(x, k, l);
            }
            else
            {
                r = Math.Exp(r);
                r = (double)0.5f * r * BesselFunctions.CylBesselI(k / 2 - 1, Math.Sqrt(l * x));
            }
        }

        return CheckedNarrowing(r, "pdf(non_central_chi_squared_distribution<double>, double)");
    }

    /// <summary>
    /// <c>cdf(non_central_chi_squared_distribution, x)</c>: the probability of a variate below <paramref name="x"/>.
    /// </summary>
    /// <param name="k">The distribution's degrees of freedom.</param>
    /// <param name="l">The distribution's non-centrality.</param>
    /// <param name="x">The variate, finite and not below 0.</param>
    /// <returns>The cumulative probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is negative or isn't finite.</exception>
    /// <exception cref="NanoQLException">A parameter is refused, or a series runs out of terms.</exception>
    internal static double Cdf(double k, double l, double x) =>
        CheckedCdf(k, l, x, false, "boost::math::non_central_chi_squared_distribution<double>::cdf(double)");

    /// <summary>
    /// <c>cdf(complement(non_central_chi_squared_distribution, x))</c>: the probability of a variate above
    /// <paramref name="x"/>.
    /// </summary>
    /// <param name="k">The distribution's degrees of freedom.</param>
    /// <param name="l">The distribution's non-centrality.</param>
    /// <param name="x">The variate, finite and not below 0.</param>
    /// <returns>The complement probability.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is negative or isn't finite.</exception>
    /// <exception cref="NanoQLException">A parameter is refused, or a series runs out of terms.</exception>
    internal static double CdfComplement(double k, double l, double x) =>
        CheckedCdf(k, l, x, true, "boost::math::non_central_chi_squared_distribution<double>::cdf(double)");

    /// <summary>
    /// <c>quantile(non_central_chi_squared_distribution, p)</c>: the variate below which the probability is p.
    /// </summary>
    /// <param name="k">The distribution's degrees of freedom.</param>
    /// <param name="l">The distribution's non-centrality.</param>
    /// <param name="p">The probability, in [0, 1].</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="p"/> is outside [0, 1].</exception>
    /// <exception cref="NanoQLException">A parameter is refused; p is 1, an overflow; or the root isn't found.</exception>
    internal static double Quantile(double k, double l, double p) => NccsQuantile(k, l, p, false);

    /// <summary>
    /// <c>quantile(complement(non_central_chi_squared_distribution, q))</c>: the variate above which the probability
    /// is q.
    /// </summary>
    /// <param name="k">The distribution's degrees of freedom.</param>
    /// <param name="l">The distribution's non-centrality.</param>
    /// <param name="q">The probability, in [0, 1].</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="q"/> is outside [0, 1].</exception>
    /// <exception cref="NanoQLException">A parameter is refused; q is 0, an overflow; or the root isn't found.</exception>
    internal static double QuantileComplement(double k, double l, double q) => NccsQuantile(k, l, q, true);

    // cdf and cdf(complement(...)): the checks under the caller's name, then non_central_chi_squared_cdf
    private static double CheckedCdf(double k, double l, double x, bool invert, string function)
    {
        CheckDf(function, k);
        CheckNonCentrality(function, l);
        CheckPositiveX(function, x, nameof(x));

        return NonCentralChiSquaredCdf(x, k, l, invert);
    }

    // detail::non_central_chi_squared_cdf: the smaller tail's series, its sign turned for the other
    private static double NonCentralChiSquaredCdf(double x, double k, double l, bool invert)
    {
        double result;
        if (l == 0)
        {
            return invert == false ? ChiSquaredFunctions.Cdf(k, x) : ChiSquaredFunctions.CdfComplement(k, x);
        }
        else if (x > k + l)
        {
            // Complement is the smaller of the two:
            result = NonCentralChiSquareQ(x, k, l, invert ? 0 : -1);
            invert = !invert;
        }
        else if (l < 200)
        {
            // For small values of the non-centrality parameter
            // we can use Ding's method:
            result = NonCentralChiSquarePDing(x, k, l, invert ? -1 : 0);
        }
        else
        {
            // For largers values of the non-centrality
            // parameter Ding's method will consume an
            // extra-ordinary number of terms, and worse
            // may return zero when the result is in fact
            // finite, use Krishnamoorthy's method instead:
            result = NonCentralChiSquareP(x, k, l, invert ? -1 : 0);
        }

        if (invert)
        {
            result = -result;
        }

        return CheckedNarrowing(result, "boost::math::non_central_chi_squared_cdf<double>(double, double, double)");
    }

    // detail::non_central_chi_square_q: the complement of the cdf, Poisson-weighted central complements summed from
    // the Poisson weights' peak forwards, then backwards (Benton and Krishnamoorthy)
    private static double NonCentralChiSquareQ(double x, double f, double theta, double initSum)
    {
        //
        // Computes the complement of the Non-Central Chi-Square
        // Distribution CDF by summing a weighted sum of complements
        // of the central-distributions.  The weighting factor is
        // a Poisson Distribution.
        //
        // This is an application of the technique described in:
        //
        // Computing discrete mixtures of continuous
        // distributions: noncentral chisquare, noncentral t
        // and the distribution of the square of the sample
        // multiple correlation coefficient.
        // D. Benton, K. Krishnamoorthy.
        // Computational Statistics & Data Analysis 43 (2003) 249 - 267
        //

        // Special case:
        if (x == 0)
        {
            return 1;
        }

        //
        // Initialize the variables we'll be using:
        //
        double lambda = theta / 2;
        double del = f / 2;
        double y = x / 2;
        ulong maxIter = SeriesFunctions.MaxSeriesIterations;
        double errtol = Epsilon;
        double sum = initSum;

        //
        // k is the starting location for iteration, we'll
        // move both forwards and backwards from this point.
        // k is chosen as the peek of the Poisson weights, which
        // will occur *before* the largest term.
        //
        long k = RoundFunctions.LLRound(lambda);

        // Forwards and backwards Poisson weights:
        double poisf = GammaFunctions.GammaPDerivative(1 + k, lambda);
        double poisb = poisf * k / lambda;

        // Initial forwards central chi squared term:
        double gamf = GammaFunctions.GammaQ(del + k, y);

        // Forwards and backwards recursion terms on the central chi squared:
        double xtermf = GammaFunctions.GammaPDerivative(del + 1 + k, y);
        double xtermb = xtermf * (del + k) / y;

        // Initial backwards central chi squared term:
        double gamb = gamf - xtermb;

        //
        // Forwards iteration first, this is the
        // stable direction for the gamma function
        // recurrences:
        //
        long i;
        for (i = k; (ulong)(i - k) < maxIter; ++i)
        {
            double term = poisf * gamf;
            sum += term;
            poisf *= lambda / (i + 1);
            gamf += xtermf;
            xtermf *= y / (del + i + 1);
            if (((sum == 0) || (Math.Abs(term / sum) < errtol)) && (term >= poisf * gamf))
            {
                break;
            }
        }

        // Error check:
        if ((ulong)(i - k) >= maxIter)
        {
            throw SeriesDidNotConverge("cdf", sum);
        }

        //
        // Now backwards iteration: the gamma
        // function recurrences are unstable in this
        // direction, we rely on the terms diminishing in size
        // faster than we introduce cancellation errors.
        // For this reason it's very important that we start
        // *before* the largest term so that backwards iteration
        // is strictly converging.
        //
        for (i = k - 1; i >= 0; --i)
        {
            double term = poisb * gamb;
            sum += term;
            poisb *= i / lambda;
            xtermb *= (del + i) / y;
            gamb -= xtermb;
            if ((sum == 0) || (Math.Abs(term / sum) < errtol))
            {
                break;
            }
        }

        return sum;
    }

    // detail::non_central_chi_square_p_ding: the cdf by Ding's stable forward recursion (AS 275), for a small
    // non-centrality
    private static double NonCentralChiSquarePDing(double x, double f, double theta, double initSum)
    {
        //
        // This is an implementation of:
        //
        // Algorithm AS 275:
        // Computing the Non-Central #2 Distribution Function
        // Cherng G. Ding
        // Applied Statistics, Vol. 41, No. 2. (1992), pp. 478-482.
        //
        // This uses a stable forward iteration to sum the
        // CDF, unfortunately this can not be used for large
        // values of the non-centrality parameter because:
        // * The first term may underflow to zero.
        // * We may need an extra-ordinary number of terms
        //   before we reach the first *significant* term.
        //

        // Special case:
        if (x == 0)
        {
            return 0;
        }

        double tk = GammaFunctions.GammaPDerivative(f / 2 + 1, x / 2);
        double lambda = theta / 2;
        double vk = Math.Exp(-lambda);
        double uk = vk;
        double sum = initSum + tk * vk;
        if (sum == 0)
        {
            return sum;
        }

        ulong maxIter = SeriesFunctions.MaxSeriesIterations;
        double errtol = Epsilon;

        int i;
        double lterm = 0, term = 0;
        for (i = 1; (ulong)i < maxIter; ++i)
        {
            tk = tk * x / (f + 2 * i);
            uk = uk * lambda / i;
            vk = vk + uk;
            lterm = term;
            term = vk * tk;
            sum += term;
            if ((Math.Abs(term / sum) < errtol) && (term <= lterm))
            {
                break;
            }
        }

        // Error check:
        if ((ulong)i >= maxIter)
        {
            throw SeriesDidNotConverge("cdf", sum);
        }

        return sum;
    }

    // detail::non_central_chi_square_p: the cdf, Poisson-weighted central terms summed from the Poisson weights' peak
    // backwards, then forwards (Benton and Krishnamoorthy), for a large non-centrality
    private static double NonCentralChiSquareP(double y, double n, double lambda, double initSum)
    {
        //
        // This is taken more or less directly from:
        //
        // Computing discrete mixtures of continuous
        // distributions: noncentral chisquare, noncentral t
        // and the distribution of the square of the sample
        // multiple correlation coefficient.
        // D. Benton, K. Krishnamoorthy.
        // Computational Statistics & Data Analysis 43 (2003) 249 - 267
        //
        // We're summing a Poisson weighting term multiplied by
        // a central chi squared distribution.
        //

        // Special case:
        if (y == 0)
        {
            return 0;
        }

        ulong maxIter = SeriesFunctions.MaxSeriesIterations;
        double errtol = Epsilon;
        double errorf = 0, errorb = 0;

        double x = y / 2;
        double del = lambda / 2;

        //
        // Starting location for the iteration, we'll iterate
        // both forwards and backwards from this point.  The
        // location chosen is the maximum of the Poisson weight
        // function, which ocurrs *after* the largest term in the
        // sum.
        //
        long k = RoundFunctions.LLRound(del);
        double a = n / 2 + k;

        // Central chi squared term for forward iteration:
        double gamkf = GammaFunctions.GammaP(a, x);

        if (lambda == 0)
        {
            return gamkf;
        }

        // Central chi squared term for backward iteration:
        double gamkb = gamkf;

        // Forwards Poisson weight:
        double poiskf = GammaFunctions.GammaPDerivative(k + 1, del);

        // Backwards Poisson weight:
        double poiskb = poiskf;

        // Forwards gamma function recursion term:
        double xtermf = GammaFunctions.GammaPDerivative(a, x);

        // Backwards gamma function recursion term:
        double xtermb = xtermf * x / a;
        double sum = initSum + poiskf * gamkf;
        if (sum == 0)
        {
            return sum;
        }

        int i = 1;

        //
        // Backwards recursion first, this is the stable
        // direction for gamma function recurrences:
        //
        while (i <= k)
        {
            xtermb *= (a - i + 1) / x;
            gamkb += xtermb;
            poiskb = poiskb * (k - i + 1) / del;
            errorf = errorb;
            errorb = gamkb * poiskb;
            sum += errorb;
            if ((Math.Abs(errorb / sum) < errtol) && (errorb <= errorf))
            {
                break;
            }

            ++i;
        }

        i = 1;

        //
        // Now forwards recursion, the gamma function
        // recurrence relation is unstable in this direction,
        // so we rely on the magnitude of successive terms
        // decreasing faster than we introduce cancellation error.
        // For this reason it's vital that k is chosen to be *after*
        // the largest term, so that successive forward iterations
        // are strictly (and rapidly) converging.
        //
        do
        {
            xtermf = xtermf * x / (a + i - 1);
            gamkf = gamkf - xtermf;
            poiskf = poiskf * del / (k + i);
            errorf = poiskf * gamkf;
            sum += errorf;
            ++i;
        }
        while ((Math.Abs(errorf / sum) > errtol) && ((ulong)i < maxIter));

        // Error check:
        if ((ulong)i >= maxIter)
        {
            throw SeriesDidNotConverge("cdf", sum);
        }

        return sum;
    }

    // detail::non_central_chi_square_pdf: the density as its Poisson-weighted series of central densities, from the
    // Poisson weights' peak in both directions
    private static double NonCentralChiSquarePdf(double x, double n, double lambda)
    {
        //
        // As above but for the PDF:
        //
        ulong maxIter = SeriesFunctions.MaxSeriesIterations;
        double errtol = Epsilon;
        double x2 = x / 2;
        double n2 = n / 2;
        double l2 = lambda / 2;
        double sum = 0;
        long k = TruncFunctions.LLTrunc(l2);
        double pois = GammaFunctions.GammaPDerivative(k + 1, l2) * GammaFunctions.GammaPDerivative(n2 + k, x2);
        if (pois == 0)
        {
            return 0;
        }

        double poisb = pois;
        for (long i = k; ; ++i)
        {
            sum += pois;
            if (pois / sum < errtol)
            {
                break;
            }

            if ((ulong)(i - k) >= maxIter)
            {
                throw SeriesDidNotConverge("pdf", sum);
            }

            pois *= l2 * x2 / ((i + 1) * (n2 + i));
        }

        for (long i = k - 1; i >= 0; --i)
        {
            poisb *= (i + 1) * (n2 + i) / (l2 * x2);
            sum += poisb;
            if (poisb / sum < errtol)
            {
                break;
            }
        }

        return sum / 2;
    }

    // detail::nccs_quantile: Pearson's approximation (Biometrika 46, 364, 1959) or, where it falls below 0.005, T. Luu's
    // asymptotic one (thesis, UCL, 2016) as the guess, then the generic quantile
    private static double NccsQuantile(double k, double l, double p, bool comp)
    {
        const string function = "quantile(non_central_chi_squared_distribution<long double>, long double)";
        const string narrowing = "quantile(non_central_chi_squared_distribution<double>, double)";

        CheckDf(function, k);
        CheckNonCentrality(function, l);
        CheckProbability(function, p, comp ? "q" : nameof(p));

        //
        // Special cases get short-circuited first:
        //
        if (p == 0)
        {
            return comp ? throw new NanoQLException($"Error in function {narrowing}: Overflow Error") : 0;
        }

        if (p == 1)
        {
            return comp ? 0 : throw new NanoQLException($"Error in function {narrowing}: Overflow Error");
        }

        //
        // This is Pearson's approximation to the quantile, see
        // Pearson, E. S. (1959) "Note on an approximation to the distribution of
        // noncentral chi squared", Biometrika 46: 364.
        // See also:
        // "A comparison of approximations to percentiles of the noncentral chi2-distribution",
        // Hardeo Sahai and Mario Miguel Ojeda, Revista de Matematica: Teoria y Aplicaciones 2003 10(1-2) : 57-76.
        // Note that the latter reference refers to an approximation of the CDF, when they really mean the quantile.
        //
        double b = -(l * l) / (k + 3 * l);
        double c = (k + 3 * l) / (k + 2 * l);
        double ff = (k + 2 * l) / (c * c);
        double guess;
        if (comp)
        {
            guess = b + c * ChiSquaredFunctions.QuantileComplement(ff, p);
        }
        else
        {
            guess = b + c * ChiSquaredFunctions.Quantile(ff, p);
        }

        //
        // Sometimes guess goes very small or negative, in that case we have
        // to do something else for the initial guess, this approximation
        // was provided in a private communication from Thomas Luu, PhD candidate,
        // University College London.  It's an asymptotic expansion for the
        // quantile which usually gets us within an order of magnitude of the
        // correct answer.
        // Fast and accurate parallel computation of quantile functions for random number generation,
        // Thomas LuuDoctorial Thesis 2016
        // http://discovery.ucl.ac.uk/1482128/
        //
        if (guess < 0.005)
        {
            double pp = comp ? 1 - p : p;
            guess = Math.Pow(Math.Pow(2, k / 2 - 1) * Math.Exp(l / 2) * pp * k * GammaFunctions.TGamma(k / 2), 2 / k);
            if (guess == 0)
            {
                guess = MinValue;
            }
        }

        // the cdf the quantile roots is non_central_chi_squared_distribution<long double>'s, its checks so named
        const string cdfFunction = "boost::math::non_central_chi_squared_distribution<long double>::cdf(long double)";
        double result = GenericQuantileFunctions.GenericQuantile(
            x => CheckedCdf(k, l, x, false, cdfFunction),
            x => CheckedCdf(k, l, x, true, cdfFunction),
            (0, MaxValue),
            p,
            guess,
            comp,
            function);

        return CheckedNarrowing(result, narrowing);
    }

    // policies::checked_narrowing_cast<double> from long double, MSVC's double: only an infinite value overflows
    private static double CheckedNarrowing(double value, string function) =>
        Math.Abs(value) > MaxValue ? throw new NanoQLException($"Error in function {function}: Overflow Error") : value;

    // the series' evaluation error: "Series did not converge, closest value was %1%"
    private static NanoQLException SeriesDidNotConverge(string kind, double sum) =>
        new($"Error in function {kind}(non_central_chi_squared_distribution<long double>, long double): Series did not "
            + $"converge, closest value was {sum.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
}

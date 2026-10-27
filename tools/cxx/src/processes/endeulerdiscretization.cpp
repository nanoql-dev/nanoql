// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/endeulerdiscretization.hpp, .cpp, which the wheel doesn't expose: the end-point Euler discretization on
// a Black-Scholes-Merton process at 100, flat forwards of 3% and 1% and Black volatilities of 15%, 25% and 30% at 90,
// 365 and 730 days (a variance curve, so the local volatility moves with time), and on a Heston process at 100 on the
// same forwards (v0 0.04, kappa 1.5, theta 0.06, sigma 0.4, rho -0.6), from the evaluation date on Act/365.
// "EndEulerDiscretization.1d" takes (member, t0, x0, dt): drift, diffusion or variance on the first.
// "EndEulerDiscretization.nd" takes (member, t0, x0, dt), x0 the Heston state: drift (an array), diffusion or
// covariance (matrices, by rows).

#include "convert.hpp"

#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/endeulerdiscretization.hpp>
#include <ql/processes/hestonprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackvariancecurve.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<GeneralizedBlackScholesProcess> blackScholes() {
            Date d = Settings::instance().evaluationDate();
            DayCounter c = Actual365Fixed();
            std::vector<Date> dates = {d + 90, d + 365, d + 730};
            std::vector<Volatility> vols = {0.15, 0.25, 0.30};
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.01, c)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.03, c)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackVarianceCurve>(d, dates, vols, c, false)));
        }

        ext::shared_ptr<HestonProcess> heston() {
            Date d = Settings::instance().evaluationDate();
            DayCounter c = Actual365Fixed();
            return ext::make_shared<HestonProcess>(
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.03, c)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, 0.01, c)),
                Handle<Quote>(ext::make_shared<SimpleQuote>(100.0)), 0.04, 1.5, 0.06, 0.4, -0.6);
        }

        json::value array(const Array& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return out;
        }

        json::value matrix(const Matrix& m) {
            json::array out;
            for (Size i = 0; i < m.rows(); ++i) {
                json::array row;
                for (Size j = 0; j < m.columns(); ++j)
                    row.emplace_back(m[i][j]);
                out.emplace_back(row);
            }
            return out;
        }

        const Register unit({
            {"EndEulerDiscretization.1d",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::string member = text(a[0]);
                 Real t0 = real(a[1]), x0 = real(a[2]), dt = real(a[3]);
                 if (member != "drift" && member != "diffusion" && member != "variance")
                     throw InvalidParams("unknown member " + member);
                 return [=] {
                     auto p = blackScholes();
                     EndEulerDiscretization e;
                     const StochasticProcess1D& q = *p;
                     if (member == "drift")
                         return json::value(e.drift(q, t0, x0, dt));
                     if (member == "diffusion")
                         return json::value(e.diffusion(q, t0, x0, dt));
                     return json::value(e.variance(q, t0, x0, dt));
                 };
             }},
            {"EndEulerDiscretization.nd",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::string member = text(a[0]);
                 Real t0 = real(a[1]);
                 std::vector<Real> x = reals(a[2]);
                 Real dt = real(a[3]);
                 if (x.size() != 2)
                     throw InvalidParams("expected a Heston state (S, v)");
                 if (member != "drift" && member != "diffusion" && member != "covariance")
                     throw InvalidParams("unknown member " + member);
                 return [=] {
                     auto p = heston();
                     EndEulerDiscretization e;
                     Array x0(x.begin(), x.end());
                     if (member == "drift")
                         return array(e.drift(*p, t0, x0, dt));
                     if (member == "diffusion")
                         return matrix(e.diffusion(*p, t0, x0, dt));
                     return matrix(e.covariance(*p, t0, x0, dt));
                 };
             }},
        });

    }

}

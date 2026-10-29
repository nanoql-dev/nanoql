// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/models/normalclvmodel.hpp, .cpp and squarerootclvmodel.hpp, .cpp: the CLV models, which the wheel
// doesn't bind. Each model is collocated to a Black-Scholes-Merton process of flat forwards and a constant volatility
// from the reference date on Actual/365 (Fixed).
//
// "NormalCLVModel::values" takes (referenceDate, spot, r, q, vol, [speed, sigma, x0, level], maturities, order, pMax,
// pMin, dates, mapping): the kernel's Ornstein-Uhlenbeck parameters, the maturities in days from the reference date,
// the Lagrange order, the probabilities (null for none), the dates in days to read the collocation points at and the
// mapping's [t, x] points; it returns {"x", "y", "cdf", "g"}: the collocation points X and Y at each date in turn, the
// cdf at each date's Y points, and g at each mapping point.
//
// "SquareRootCLVModel::values" takes (referenceDate, spot, r, q, vol, [theta, kappa, sigma, x0], maturities, order,
// pMax, pMin, dates, mapping) and returns the same, on the square-root kernel.

// MSVC reports Boost.Math's forceinline functions it doesn't inline (C4714), as the harness's Boost sources do.
#pragma warning(disable : 4714)

#include "convert.hpp"

#include <ql/experimental/models/normalclvmodel.hpp>
#include <ql/experimental/models/squarerootclvmodel.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/processes/ornsteinuhlenbeckprocess.hpp>
#include <ql/processes/squarerootprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<GeneralizedBlackScholesProcess> bs(const Date& reference, Real spot, Real r, Real q, Real vol) {
            DayCounter dc = Actual365Fixed();
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, q, dc)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, r, dc)),
                Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(reference, NullCalendar(), vol, dc)));
        }

        std::vector<Date> days(const Date& reference, const std::vector<Integer>& d) {
            std::vector<Date> out;
            for (Integer n : d)
                out.push_back(reference + n);
            return out;
        }

        std::vector<std::vector<Real>> pairs(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of [t, x] points, got " + json::serialize(v));
            std::vector<std::vector<Real>> out;
            for (const json::value& p : v.get_array()) {
                std::vector<Real> r = reals(p);
                if (r.size() != 2)
                    throw InvalidParams("a mapping point is [t, x]");
                out.push_back(r);
            }
            return out;
        }

        template <class M>
        json::value describe(const M& m, const std::vector<Date>& dates, const std::vector<std::vector<Real>>& mapping) {
            json::array x, y, cdf, g;
            for (const Date& d : dates) {
                Array xs = m.collocationPointsX(d), ys = m.collocationPointsY(d);
                for (Real v : xs)
                    x.emplace_back(v);
                for (Real v : ys) {
                    y.emplace_back(v);
                    cdf.emplace_back(m.cdf(d, v));
                }
            }
            auto f = m.g();
            for (const auto& p : mapping)
                g.emplace_back(f(p[0], p[1]));
            json::object out;
            out["x"] = x;
            out["y"] = y;
            out["cdf"] = cdf;
            out["g"] = g;
            return json::value(out);
        }

        const Register unit({
            {"NormalCLVModel::values",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 DateSpec reference = date(a[0]);
                 Real spot = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]);
                 std::vector<Real> ou = reals(a[5]);
                 if (ou.size() != 4)
                     throw InvalidParams("an Ornstein-Uhlenbeck kernel is [speed, sigma, x0, level]");
                 std::vector<Integer> maturities = integers(a[6]);
                 Size order = static_cast<Size>(integer(a[7]));
                 Real pMax = real(a[8]), pMin = real(a[9]);
                 std::vector<Integer> dates = integers(a[10]);
                 auto mapping = pairs(a[11]);
                 return [=] {
                     Date ref = makeDate(reference);
                     NormalCLVModel m(bs(ref, spot, r, q, vol),
                                      ext::make_shared<OrnsteinUhlenbeckProcess>(ou[0], ou[1], ou[2], ou[3]),
                                      days(ref, maturities), order, pMax, pMin);
                     return describe(m, days(ref, dates), mapping);
                 };
             }},
            {"SquareRootCLVModel::values",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 DateSpec reference = date(a[0]);
                 Real spot = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]);
                 std::vector<Real> sq = reals(a[5]);
                 if (sq.size() != 4)
                     throw InvalidParams("a square-root kernel is [theta, kappa, sigma, x0]");
                 std::vector<Integer> maturities = integers(a[6]);
                 Size order = static_cast<Size>(integer(a[7]));
                 Real pMax = real(a[8]), pMin = real(a[9]);
                 std::vector<Integer> dates = integers(a[10]);
                 auto mapping = pairs(a[11]);
                 return [=] {
                     Date ref = makeDate(reference);
                     SquareRootCLVModel m(bs(ref, spot, r, q, vol),
                                          ext::make_shared<SquareRootProcess>(sq[0], sq[1], sq[2], sq[3]),
                                          days(ref, maturities), order, pMax, pMin);
                     return describe(m, days(ref, dates), mapping);
                 };
             }},
        });

    }

}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/montecarlo/longstaffschwartzpathpricer.hpp: the Longstaff-Schwartz path pricer, which the wheel doesn't
// expose, on QuantLib's AmericanPathPricer (ql/pricingengines/vanilla/mcamericanengine.hpp) and a flat discount curve,
// over paths given. The method
//
//     LongstaffSchwartzPathPricer  (times, payoff, polynomialOrder, polynomialType, referenceDate, rate,
//                                   calibrationPaths, pricingPaths)
//
// builds the grid from the times (TimeGrid(begin, end)), the early-exercise pricer on the payoff (a spec of
// instruments/payoffspec.hpp) and the basis, the curve a FlatForward from the reference date at the rate on
// Actual/365 (Fixed), and the pricer; it prices each calibration path (stored, 0), calibrates, prices each pricing
// path, and returns {"calibration" (the calibration paths' values), "coefficients" (the regression's, one list per
// time step from the first), "postProcessing" (each call of the hook: {"i", "state", "price", "exercise"}), "prices",
// "exerciseProbability"}. A path is its values, one per time of the grid. The coefficients and the hook are
// protected; a class deriving the pricer reads them.

#include "convert.hpp"
#include "instruments/payoffspec.hpp"

#include <ql/math/array.hpp>
#include <ql/methods/montecarlo/longstaffschwartzpathpricer.hpp>
#include <ql/methods/montecarlo/path.hpp>
#include <ql/pricingengines/vanilla/mcamericanengine.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>
#include <ql/timegrid.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value list(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        class Recording : public LongstaffSchwartzPathPricer<Path> {
          public:
            using LongstaffSchwartzPathPricer<Path>::LongstaffSchwartzPathPricer;

            json::value coefficients() const {
                json::array out;
                for (Size i = 0; i + 2 < len_; ++i) {
                    json::array c;
                    for (Real x : coeff_[i])
                        c.emplace_back(x);
                    out.emplace_back(std::move(c));
                }
                return json::value(std::move(out));
            }

            json::array calls;

          protected:
            void post_processing(const Size i, const std::vector<Real>& state, const std::vector<Real>& price,
                                 const std::vector<Real>& exercise) override {
                json::object call;
                call["i"] = static_cast<std::int64_t>(i);
                call["state"] = list(state);
                call["price"] = list(price);
                call["exercise"] = list(exercise);
                calls.emplace_back(std::move(call));
            }
        };

        std::vector<std::vector<Real>> paths(const json::value& v, Size n) {
            if (!v.is_array())
                throw InvalidParams("expected paths, a list of lists of reals");
            std::vector<std::vector<Real>> out;
            for (const json::value& p : v.get_array()) {
                out.push_back(reals(p));
                if (out.back().size() != n)
                    throw InvalidParams("a path holds one value per time of the grid");
            }
            return out;
        }

        Size size(const json::value& v) {
            long long n = int64(v);
            if (n < 0)
                throw InvalidParams("expected a non-negative size");
            return Size(n);
        }

        const Register unit({
            {"LongstaffSchwartzPathPricer",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 std::vector<Real> times = reals(a[0]);
                 payoffs::PayoffSpec payoff = payoffs::payoff(a[1]);
                 Size order = size(a[2]);
                 auto type = enumeration<LsmBasisSystem::PolynomialType>(a[3]);
                 DateSpec reference = date(a[4]);
                 Real rate = real(a[5]);
                 Size n = TimeGrid(times.begin(), times.end()).size();
                 std::vector<std::vector<Real>> calibration = paths(a[6], n), pricing = paths(a[7], n);
                 return [=] {
                     TimeGrid grid(times.begin(), times.end());
                     auto early = ext::make_shared<AmericanPathPricer>(payoffs::makePayoff(payoff), order, type);
                     auto curve = ext::make_shared<FlatForward>(makeDate(reference), rate, Actual365Fixed());
                     Recording pricer(grid, early, curve);
                     json::array calibrated;
                     for (const auto& p : calibration)
                         calibrated.emplace_back(pricer(Path(grid, Array(p.begin(), p.end()))));
                     pricer.calibrate();
                     json::array prices;
                     for (const auto& p : pricing)
                         prices.emplace_back(pricer(Path(grid, Array(p.begin(), p.end()))));
                     json::object out;
                     out["calibration"] = std::move(calibrated);
                     out["coefficients"] = pricer.coefficients();
                     out["postProcessing"] = std::move(pricer.calls);
                     out["prices"] = std::move(prices);
                     out["exerciseProbability"] = pricer.exerciseProbability();
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}

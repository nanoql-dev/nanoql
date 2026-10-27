// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/volatility/garch.hpp, .cpp, which the wheel doesn't expose. "Garch11.calculate" takes (alpha, beta, omega,
// dates, values): the volatility series of the returns. "Garch11.calibrate" takes (values, mode): the model calibrated
// to the returns on consecutive dates, its alpha, beta, omega and log-likelihood. "Garch11.calibrateFromGuess" takes
// (values, omega, alpha, beta): the calibration by QuantLib's Simplex from that guess. "Garch11.to_r2" takes (values):
// the squared returns and their mean. "Garch11.costFunction" takes (values, alpha, beta, omega). "Garch11.forecast"
// takes (alpha, beta, vl, r, sigma2).

#include "convert.hpp"

#include <ql/math/optimization/endcriteria.hpp>
#include <ql/math/optimization/simplex.hpp>
#include <ql/models/volatility/garch.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        TimeSeries<Volatility> series(const std::vector<Real>& values) {
            std::vector<Date> dates;
            Date d(7, July, 1962);
            for (Size i = 0; i < values.size(); ++i, d += 1)
                dates.push_back(d);
            return TimeSeries<Volatility>(dates.begin(), dates.end(), values.begin());
        }

        json::value model(const Garch11& g) {
            json::object out;
            out["alpha"] = g.alpha();
            out["beta"] = g.beta();
            out["omega"] = g.omega();
            out["logLikelihood"] = g.logLikelihood();
            return out;
        }

        const Register unit({
            {"Garch11.calculate",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real alpha = real(a[0]), beta = real(a[1]), omega = real(a[2]);
                 std::vector<Integer> serials = integers(a[3]);
                 std::vector<Real> values = reals(a[4]);
                 if (serials.size() != values.size() || serials.size() < 2)
                     throw InvalidParams("expected two dates or more and a value for each");
                 return [=] {
                     std::vector<Date> dates;
                     for (Integer s : serials)
                         dates.emplace_back(static_cast<Date::serial_type>(s));
                     TimeSeries<Volatility> ts(dates.begin(), dates.end(), values.begin());
                     // the static overload by its type: MSVC reads the member one in a lambda's lookup
                     auto calculate = static_cast<TimeSeries<Volatility> (*)(const TimeSeries<Volatility>&, Real, Real, Real)>(
                         &Garch11::calculate);
                     TimeSeries<Volatility> r = calculate(ts, alpha, beta, omega);
                     json::array ds, vs;
                     for (const auto& p : r) {
                         ds.emplace_back(static_cast<Integer>(p.first.serialNumber()));
                         vs.emplace_back(p.second);
                     }
                     json::object out;
                     out["dates"] = ds;
                     out["values"] = vs;
                     return json::value(out);
                 };
             }},
            {"Garch11.calibrate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> values = reals(a[0]);
                 std::string mode = text(a[1]);
                 Garch11::Mode m;
                 if (mode == "MomentMatchingGuess")
                     m = Garch11::MomentMatchingGuess;
                 else if (mode == "GammaGuess")
                     m = Garch11::GammaGuess;
                 else if (mode == "BestOfTwo")
                     m = Garch11::BestOfTwo;
                 else if (mode == "DoubleOptimization")
                     m = Garch11::DoubleOptimization;
                 else
                     throw InvalidParams("unknown mode " + mode);
                 return [=] { return model(Garch11(series(values), m)); };
             }},
            {"Garch11.calibrateFromGuess",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Real> values = reals(a[0]);
                 Array guess(3);
                 guess[0] = real(a[1]);
                 guess[1] = real(a[2]);
                 guess[2] = real(a[3]);
                 return [=] {
                     Garch11 g(0.0, 0.0, 0.0);
                     Simplex method(0.001);
                     g.calibrate(series(values), method, EndCriteria(10000, 500, 1e-8, 1e-8, 1e-8), guess);
                     return model(g);
                 };
             }},
            {"Garch11.to_r2",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 std::vector<Real> values = reals(a[0]);
                 return [=] {
                     std::vector<Volatility> r2;
                     Real mean = Garch11::to_r2(values.begin(), values.end(), r2);
                     json::array rs;
                     for (Real x : r2)
                         rs.emplace_back(x);
                     json::object out;
                     out["r2"] = rs;
                     out["mean"] = mean;
                     return json::value(out);
                 };
             }},
            {"Garch11.costFunction",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::vector<Real> values = reals(a[0]);
                 Real alpha = real(a[1]), beta = real(a[2]), omega = real(a[3]);
                 return [=] {
                     using It = std::vector<Real>::const_iterator;
                     auto cost = static_cast<Real (*)(It, It, Real, Real, Real)>(&Garch11::costFunction<It>);
                     return json::value(cost(values.cbegin(), values.cend(), alpha, beta, omega));
                 };
             }},
            {"Garch11.forecast",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real alpha = real(a[0]), beta = real(a[1]), vl = real(a[2]), r = real(a[3]), sigma2 = real(a[4]);
                 return [=] { return json::value(Garch11(alpha, beta, vl).forecast(r, sigma2)); };
             }},
        });

    }

}

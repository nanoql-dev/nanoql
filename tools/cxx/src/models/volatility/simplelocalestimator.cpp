// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/volatility/simplelocalestimator.hpp, and garmanklass.hpp's GarmanKlassSimpleSigma, which the wheel doesn't
// expose. "SimpleLocalEstimator.calculate" takes (y, dates, values): the volatility series of the quotes.
// "GarmanKlassSimpleSigma.calculate" takes (y, dates, open, close, high, low): the volatility series of the interval
// prices.

#include "convert.hpp"

#include <ql/models/volatility/garmanklass.hpp>
#include <ql/models/volatility/simplelocalestimator.hpp>
#include <ql/prices.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::vector<Date> dates(const std::vector<Integer>& serials) {
            std::vector<Date> out;
            for (Integer s : serials)
                out.emplace_back(static_cast<Date::serial_type>(s));
            return out;
        }

        json::value result(const TimeSeries<Volatility>& r) {
            json::array ds, vs;
            for (const auto& p : r) {
                ds.emplace_back(static_cast<Integer>(p.first.serialNumber()));
                vs.emplace_back(p.second);
            }
            json::object out;
            out["dates"] = ds;
            out["values"] = vs;
            return out;
        }

        const Register unit({
            {"SimpleLocalEstimator.calculate",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real y = real(a[0]);
                 std::vector<Integer> serials = integers(a[1]);
                 std::vector<Real> values = reals(a[2]);
                 if (serials.empty() || serials.size() != values.size())
                     throw InvalidParams("expected a date or more and a value for each");
                 return [=] {
                     std::vector<Date> d = dates(serials);
                     TimeSeries<Real> ts(d.begin(), d.end(), values.begin());
                     return result(SimpleLocalEstimator(y).calculate(ts));
                 };
             }},
            {"GarmanKlassSimpleSigma.calculate",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real y = real(a[0]);
                 std::vector<Integer> serials = integers(a[1]);
                 std::vector<Real> open = reals(a[2]), close = reals(a[3]), high = reals(a[4]), low = reals(a[5]);
                 return [=] {
                     TimeSeries<IntervalPrice> ts = IntervalPrice::makeSeries(dates(serials), open, close, high, low);
                     return result(GarmanKlassSimpleSigma(y).calculate(ts));
                 };
             }},
        });

    }

}

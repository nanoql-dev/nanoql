// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/swaption/swaptionvoldiscrete.hpp: SwaptionVolatilityDiscrete's members on a volatility
// matrix spec (swaptionvolmatrixspec.hpp), the matrix built inside the call.
//
//     SwaptionVolatilityDiscrete::optionDateFromTime   (matrix, time): the date, or Date's refusal of the serial
//     SwaptionVolatilityDiscrete::optionTenors         (matrix): [[length, units], ...]
//     SwaptionVolatilityDiscrete::optionDates          (matrix): serials
//     SwaptionVolatilityDiscrete::optionTimes          (matrix)
//     SwaptionVolatilityDiscrete::swapTenors           (matrix): [[length, units], ...]
//     SwaptionVolatilityDiscrete::swapLengths          (matrix)

#include "convert.hpp"
#include "termstructures/volatility/swaption/swaptionvolmatrixspec.hpp"

#include <ql/termstructures/volatility/swaption/swaptionvoldiscrete.hpp>

#include <functional>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeSwaptionVolMatrix;
        using termstructures::swaptionVolMatrix;
        using termstructures::SwaptionVolMatrixSpec;

        json::value periods(const std::vector<Period>& p) {
            json::array out;
            for (const Period& x : p) {
                json::array pair;
                pair.emplace_back(x.length());
                pair.emplace_back(static_cast<int>(x.units()));
                out.emplace_back(std::move(pair));
            }
            return out;
        }

        json::value reals(const std::vector<Real>& x) {
            json::array out;
            for (Real r : x)
                out.emplace_back(r);
            return out;
        }

        json::value dates(const std::vector<Date>& d) {
            json::array out;
            for (const Date& x : d)
                out.emplace_back(result(x));
            return out;
        }

        // A member of the matrix that takes no argument.
        Method inspector(std::function<json::value(const SwaptionVolatilityDiscrete&)> f) {
            return [=](const Args& a) -> Call {
                arity(a, 1);
                SwaptionVolMatrixSpec s = swaptionVolMatrix(a[0]);
                return [=] { return f(*makeSwaptionVolMatrix(s)); };
            };
        }

        const Register unit({
            {"SwaptionVolatilityDiscrete::optionDateFromTime",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 SwaptionVolMatrixSpec s = swaptionVolMatrix(a[0]);
                 Time t = real(a[1]);
                 return [=] { return result(makeSwaptionVolMatrix(s)->optionDateFromTime(t)); };
             }},
            {"SwaptionVolatilityDiscrete::optionTenors",
             inspector([](const SwaptionVolatilityDiscrete& m) { return periods(m.optionTenors()); })},
            {"SwaptionVolatilityDiscrete::optionDates",
             inspector([](const SwaptionVolatilityDiscrete& m) { return dates(m.optionDates()); })},
            {"SwaptionVolatilityDiscrete::optionTimes",
             inspector([](const SwaptionVolatilityDiscrete& m) { return reals(m.optionTimes()); })},
            {"SwaptionVolatilityDiscrete::swapTenors",
             inspector([](const SwaptionVolatilityDiscrete& m) { return periods(m.swapTenors()); })},
            {"SwaptionVolatilityDiscrete::swapLengths",
             inspector([](const SwaptionVolatilityDiscrete& m) { return reals(m.swapLengths()); })},
        });

    }

}

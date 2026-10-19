// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/optionlet/optionletstripper.hpp: OptionletStripper's members on an OptionletStripper1
// built inside each call from a stripper spec of stripperspec.hpp, the ones the wheel doesn't bind (the tenors, payment
// dates, accrual periods, frequency and payment lag) and the others, for the strippers the wheel can't build (an Ibor
// payment lag without an optionlet frequency, whose std::optional the wheel's proxy refuses as None):
//
//     OptionletStripper::optionletFixingTenors   (stripper): [[length, units], ...]
//     OptionletStripper::optionletPaymentDates   (stripper)
//     OptionletStripper::optionletAccrualPeriods (stripper)
//     OptionletStripper::optionletFrequency      (stripper): [length, units], or null for none
//     OptionletStripper::paymentLag              (stripper)
//     OptionletStripper::optionletFixingDates, optionletFixingTimes, atmOptionletRates (stripper)
//     OptionletStripper::optionletStrikes, optionletVolatilities (stripper, i): or QuantLib's refusal past the dates
//     OptionletStripper1::switchStrike           (stripper)

#include "convert.hpp"
#include "termstructures/volatility/optionlet/stripperspec.hpp"

#include <ql/time/period.hpp>

#include <cstdint>
#include <optional>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value dates(const std::vector<Date>& ds) {
            json::array out;
            for (const Date& d : ds)
                out.push_back(result(d));
            return out;
        }

        json::value numbers(const std::vector<Real>& xs) {
            json::array out;
            for (Real x : xs)
                out.push_back(json::value(x));
            return out;
        }

        json::value tenor(const Period& p) {
            return json::array{static_cast<std::int64_t>(p.length()), static_cast<std::int64_t>(p.units())};
        }

        Call whole(const Args& a, json::value (*f)(const OptionletStripper1&)) {
            arity(a, 1);
            strippers::StripperSpec s = strippers::stripper(a[0]);
            return [=] { return f(*strippers::makeStripper(s)); };
        }

        Call atIndex(const Args& a, json::value (*f)(const OptionletStripper1&, Size)) {
            arity(a, 2);
            strippers::StripperSpec s = strippers::stripper(a[0]);
            Integer i = integer(a[1]);
            if (i == Null<Integer>() || i < 0)
                throw InvalidParams("expected a Size, got " + json::serialize(a[1]));
            return [=] { return f(*strippers::makeStripper(s), static_cast<Size>(i)); };
        }

        const Register unit({
            {"OptionletStripper::optionletFixingTenors",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) {
                     json::array out;
                     for (const Period& p : s.optionletFixingTenors())
                         out.push_back(tenor(p));
                     return json::value(out);
                 });
             }},
            {"OptionletStripper::optionletPaymentDates",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) { return dates(s.optionletPaymentDates()); });
             }},
            {"OptionletStripper::optionletAccrualPeriods",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) { return numbers(s.optionletAccrualPeriods()); });
             }},
            {"OptionletStripper::optionletFrequency",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) {
                     std::optional<Period> f = s.optionletFrequency();
                     return f ? tenor(*f) : json::value(nullptr);
                 });
             }},
            {"OptionletStripper::paymentLag",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) {
                     return json::value(static_cast<std::int64_t>(s.paymentLag()));
                 });
             }},
            {"OptionletStripper::optionletFixingDates",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) { return dates(s.optionletFixingDates()); });
             }},
            {"OptionletStripper::optionletFixingTimes",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) { return numbers(s.optionletFixingTimes()); });
             }},
            {"OptionletStripper::atmOptionletRates",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) { return numbers(s.atmOptionletRates()); });
             }},
            {"OptionletStripper::optionletStrikes",
             [](const Args& a) {
                 return atIndex(a, [](const OptionletStripper1& s, Size i) { return numbers(s.optionletStrikes(i)); });
             }},
            {"OptionletStripper::optionletVolatilities",
             [](const Args& a) {
                 return atIndex(a, [](const OptionletStripper1& s, Size i) {
                     return numbers(s.optionletVolatilities(i));
                 });
             }},
            {"OptionletStripper1::switchStrike",
             [](const Args& a) {
                 return whole(a, [](const OptionletStripper1& s) { return json::value(s.switchStrike()); });
             }},
        });

    }

}

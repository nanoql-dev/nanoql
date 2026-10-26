// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/vanillastorageoption.hpp: VanillaStorageOption, which the wheel doesn't bind.
//
// "VanillaStorageOption::validate" takes (dates, capacity, load, changeRate): a Bermudan exercise on the dates (serials)
// and the option's quantities. It sets up the option's arguments and validates them, returning "valid"; the
// arguments' refusal is QuantLib's, its message the error's.
//
// "VanillaStorageOption::isExpired" takes the same and returns the option's isExpired under the case's settings.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/vanillastorageoption.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<BermudanExercise> exercise(const std::vector<Integer>& serials) {
            std::vector<Date> dates;
            for (Integer s : serials)
                dates.emplace_back(static_cast<Date::serial_type>(s));
            return ext::make_shared<BermudanExercise>(dates);
        }

        const Register unit({
            {"VanillaStorageOption::validate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 const std::vector<Integer> dates = integers(a[0]);
                 const Real capacity = real(a[1]), load = real(a[2]), changeRate = real(a[3]);
                 if (dates.empty())
                     throw InvalidParams("an exercise date at least");
                 return [=] {
                     VanillaStorageOption option(exercise(dates), capacity, load, changeRate);
                     VanillaStorageOption::arguments arguments;
                     option.setupArguments(&arguments);
                     arguments.validate();
                     return json::value("valid");
                 };
             }},
            {"VanillaStorageOption::isExpired",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 const std::vector<Integer> dates = integers(a[0]);
                 const Real capacity = real(a[1]), load = real(a[2]), changeRate = real(a[3]);
                 if (dates.empty())
                     throw InvalidParams("an exercise date at least");
                 return [=] {
                     VanillaStorageOption option(exercise(dates), capacity, load, changeRate);
                     return json::value(option.isExpired());
                 };
             }},
        });

    }

}

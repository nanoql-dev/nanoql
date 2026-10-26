// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/lookbackoption.hpp, .cpp, whose argument validation the wheel doesn't expose. Each call validates the
// arguments of an option European in a year from the evaluation date; true when they pass.
// "ContinuousFloatingLookbackOption::arguments::validate" takes (minmax), a floating call.
// "ContinuousFixedLookbackOption::arguments::validate" takes (minmax), a call struck at 100.
// "ContinuousPartialFloatingLookbackOption::arguments::validate" takes (optionType, lambda, lookbackPeriodEnd), a
// floating option with the running extremum at 100, the lookback end a serial number.
// "ContinuousPartialFixedLookbackOption::arguments::validate" takes (lookbackPeriodStart), a call struck at 100, the
// lookback start a serial number. Only the partial options' defined cases: QuantLib's validation dereferences a
// European exercise and a floating payoff unchecked.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/lookbackoption.hpp>
#include <ql/settings.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<Exercise> aYear() {
            Date today = Settings::instance().evaluationDate();
            return ext::make_shared<EuropeanExercise>(today + 365);
        }

        const Register unit({
            {"ContinuousFloatingLookbackOption::arguments::validate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real minmax = real(a[0]);
                 return [=] {
                     ContinuousFloatingLookbackOption::arguments args;
                     args.payoff = ext::make_shared<FloatingTypePayoff>(Option::Call);
                     args.exercise = aYear();
                     args.minmax = minmax;
                     args.validate();
                     return json::value(true);
                 };
             }},
            {"ContinuousFixedLookbackOption::arguments::validate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 Real minmax = real(a[0]);
                 return [=] {
                     ContinuousFixedLookbackOption::arguments args;
                     args.payoff = ext::make_shared<PlainVanillaPayoff>(Option::Call, 100.0);
                     args.exercise = aYear();
                     args.minmax = minmax;
                     args.validate();
                     return json::value(true);
                 };
             }},
            {"ContinuousPartialFloatingLookbackOption::arguments::validate",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 auto type = enumeration<Option::Type>(a[0]);
                 Real lambda = real(a[1]);
                 DateSpec end = date(a[2]);
                 return [=] {
                     ContinuousPartialFloatingLookbackOption::arguments args;
                     args.payoff = ext::make_shared<FloatingTypePayoff>(type);
                     args.exercise = aYear();
                     args.minmax = 100.0;
                     args.lambda = lambda;
                     args.lookbackPeriodEnd = makeDate(end);
                     args.validate();
                     return json::value(true);
                 };
             }},
            {"ContinuousPartialFixedLookbackOption::arguments::validate",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 DateSpec start = date(a[0]);
                 return [=] {
                     ContinuousPartialFixedLookbackOption::arguments args;
                     args.payoff = ext::make_shared<PlainVanillaPayoff>(Option::Call, 100.0);
                     args.exercise = aYear();
                     args.minmax = 0.0;
                     args.lookbackPeriodStart = makeDate(start);
                     args.validate();
                     return json::value(true);
                 };
             }},
        });

    }

}

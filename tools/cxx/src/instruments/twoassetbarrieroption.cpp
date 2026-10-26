// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/twoassetbarrieroption.hpp, .cpp, whose expiry, argument validation and trigger the wheel doesn't
// expose. "TwoAssetBarrierOption::isExpired" takes (exerciseDate): a down-and-out call struck at 100 with barrier 90,
// European at the date, under the harness's settings. "TwoAssetBarrierOption::arguments::validate" takes
// (barrierType, barrier): the arguments of a call struck at 100, European in a year from the evaluation date, with that
// type and barrier (null for none), validated; true when they pass. "TwoAssetBarrierOption::engine::triggered" takes
// (barrierType, barrier, underlying), through an engine of its own whose arguments hold them.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/twoassetbarrieroption.hpp>
#include <ql/settings.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        class TriggerProbe : public TwoAssetBarrierOption::engine {
          public:
            TriggerProbe(Barrier::Type type, Real barrier) {
                arguments_.barrierType = type;
                arguments_.barrier = barrier;
            }
            void calculate() const override {}
            bool probe(Real underlying) const { return triggered(underlying); }
        };

        const Register unit({
            {"TwoAssetBarrierOption::isExpired",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 DateSpec d = date(a[0]);
                 return [=] {
                     TwoAssetBarrierOption option(Barrier::DownOut, 90.0,
                                                  ext::make_shared<PlainVanillaPayoff>(Option::Call, 100.0),
                                                  ext::make_shared<EuropeanExercise>(makeDate(d)));
                     return json::value(option.isExpired());
                 };
             }},
            {"TwoAssetBarrierOption::arguments::validate",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 auto t = enumeration<Barrier::Type>(a[0]);
                 Real barrier = real(a[1]);
                 return [=] {
                     TwoAssetBarrierOption::arguments args;
                     args.payoff = ext::make_shared<PlainVanillaPayoff>(Option::Call, 100.0);
                     Date today = Settings::instance().evaluationDate();
                     args.exercise = ext::make_shared<EuropeanExercise>(today + 365);
                     args.barrierType = t;
                     args.barrier = barrier;
                     args.validate();
                     return json::value(true);
                 };
             }},
            {"TwoAssetBarrierOption::engine::triggered",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 auto t = enumeration<Barrier::Type>(a[0]);
                 Real barrier = real(a[1]);
                 Real underlying = real(a[2]);
                 return [=] { return json::value(TriggerProbe(t, barrier).probe(underlying)); };
             }},
        });

    }

}

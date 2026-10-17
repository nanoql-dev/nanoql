// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/incompletegamma.hpp. Each function takes (a, x), which calls QuantLib with its header's default accuracy
// and iteration limit, or (a, x, accuracy, maxIteration).

#include "convert.hpp"

#include <ql/math/incompletegamma.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Arguments {
            Real a, x;
            bool defaults;
            Real accuracy;
            Integer maxIteration;
        };

        Arguments arguments(const Args& a) {
            if (a.size() != 2 && a.size() != 4)
                throw InvalidParams("expected 2 or 4 arguments, got " + std::to_string(a.size()));
            if (a.size() == 2)
                return {real(a[0]), real(a[1]), true, 0.0, 0};
            return {real(a[0]), real(a[1]), false, real(a[2]), integer(a[3])};
        }

        // withDefaults calls the function with two arguments, so that the header's defaults apply.
        template <class WithDefaults, class WithAll>
        Method method(WithDefaults withDefaults, WithAll withAll) {
            return [withDefaults, withAll](const Args& a) -> Call {
                Arguments s = arguments(a);
                return [=] {
                    return json::value(s.defaults ? withDefaults(s.a, s.x)
                                                  : withAll(s.a, s.x, s.accuracy, s.maxIteration));
                };
            };
        }

        const Register unit({
            {"incompleteGammaFunction",
             method([](Real a, Real x) { return incompleteGammaFunction(a, x); },
                    [](Real a, Real x, Real accuracy, Integer maxIteration) {
                        return incompleteGammaFunction(a, x, accuracy, maxIteration);
                    })},
            {"incompleteGammaFunctionSeriesRepr",
             method([](Real a, Real x) { return incompleteGammaFunctionSeriesRepr(a, x); },
                    [](Real a, Real x, Real accuracy, Integer maxIteration) {
                        return incompleteGammaFunctionSeriesRepr(a, x, accuracy, maxIteration);
                    })},
            {"incompleteGammaFunctionContinuedFractionRepr",
             method([](Real a, Real x) { return incompleteGammaFunctionContinuedFractionRepr(a, x); },
                    [](Real a, Real x, Real accuracy, Integer maxIteration) {
                        return incompleteGammaFunctionContinuedFractionRepr(a, x, accuracy, maxIteration);
                    })},
        });

    }

}

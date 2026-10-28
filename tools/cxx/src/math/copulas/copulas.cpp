// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/copulas/*.hpp, .cpp: the thirteen copulas' operator(), which the wheel doesn't bind. Each call is named after
// its class and takes the constructor's parameters (theta, rho, or a1 and a2; none for the independent, max and min
// copulas) then the lists of points xs and ys; it returns the rows of C(x, y), one per x, a value per y.

#include "convert.hpp"

#include <ql/math/copulas/alimikhailhaqcopula.hpp>
#include <ql/math/copulas/claytoncopula.hpp>
#include <ql/math/copulas/farliegumbelmorgensterncopula.hpp>
#include <ql/math/copulas/frankcopula.hpp>
#include <ql/math/copulas/galamboscopula.hpp>
#include <ql/math/copulas/gaussiancopula.hpp>
#include <ql/math/copulas/gumbelcopula.hpp>
#include <ql/math/copulas/huslerreisscopula.hpp>
#include <ql/math/copulas/independentcopula.hpp>
#include <ql/math/copulas/marshallolkincopula.hpp>
#include <ql/math/copulas/maxcopula.hpp>
#include <ql/math/copulas/mincopula.hpp>
#include <ql/math/copulas/plackettcopula.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class C>
        json::value grid(const C& copula, const std::vector<Real>& xs, const std::vector<Real>& ys) {
            json::array rows;
            for (Real x : xs) {
                json::array row;
                for (Real y : ys)
                    row.emplace_back(copula(x, y));
                rows.emplace_back(std::move(row));
            }
            return json::value(std::move(rows));
        }

        // A copula of one parameter, built inside the call so that its constructor's refusal is the call's.
        template <class C>
        Method oneParameter() {
            return [](const Args& a) -> Call {
                arity(a, 3);
                Real p = real(a[0]);
                std::vector<Real> xs = reals(a[1]), ys = reals(a[2]);
                return [=] { return grid(C(p), xs, ys); };
            };
        }

        template <class C>
        Method noParameter() {
            return [](const Args& a) -> Call {
                arity(a, 2);
                std::vector<Real> xs = reals(a[0]), ys = reals(a[1]);
                return [=] { return grid(C(), xs, ys); };
            };
        }

        const Register unit({
            {"AliMikhailHaqCopula", oneParameter<AliMikhailHaqCopula>()},
            {"ClaytonCopula", oneParameter<ClaytonCopula>()},
            {"FarlieGumbelMorgensternCopula", oneParameter<FarlieGumbelMorgensternCopula>()},
            {"FrankCopula", oneParameter<FrankCopula>()},
            {"GalambosCopula", oneParameter<GalambosCopula>()},
            {"GaussianCopula", oneParameter<GaussianCopula>()},
            {"GumbelCopula", oneParameter<GumbelCopula>()},
            {"HuslerReissCopula", oneParameter<HuslerReissCopula>()},
            {"PlackettCopula", oneParameter<PlackettCopula>()},
            {"IndependentCopula", noParameter<IndependentCopula>()},
            {"MaxCopula", noParameter<MaxCopula>()},
            {"MinCopula", noParameter<MinCopula>()},
            {"MarshallOlkinCopula",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real a1 = real(a[0]), a2 = real(a[1]);
                 std::vector<Real> xs = reals(a[2]), ys = reals(a[3]);
                 return [=] { return grid(MarshallOlkinCopula(a1, a2), xs, ys); };
             }},
        });

    }

}

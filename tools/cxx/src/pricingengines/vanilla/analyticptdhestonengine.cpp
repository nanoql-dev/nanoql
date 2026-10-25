// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/analyticptdhestonengine.hpp, .cpp: the engine's characteristic function members, which
// the wheel doesn't expose. "AnalyticPTDHestonEngine::chF" takes (process, grid, pTimes, theta, kappa, sigma, rho, zs,
// ts): PiecewiseTimeDependentHestonModel on models/equity/hestonspec.cpp's process's curves, spot and v0, its four
// parameters each a ConstantParameter where one value is given and a PiecewiseConstantParameter on pTimes otherwise,
// on TimeGrid(grid); AnalyticPTDHestonEngine on it (the members read no integration). It returns {"chF", "lnChF"} for
// each complex z ([real, imaginary]) at each t. The evaluation date is the case's Settings.

#include "complexvalue.hpp"
#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/models/equity/piecewisetimedependenthestonmodel.hpp>
#include <ql/models/parameter.hpp>
#include <ql/pricingengines/vanilla/analyticptdhestonengine.hpp>

#include <complex>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        Parameter parameter(const std::vector<Real>& values, const std::vector<Time>& pTimes,
                            const Constraint& constraint) {
            if (values.size() == 1)
                return ConstantParameter(values[0], constraint);
            PiecewiseConstantParameter p(pTimes, constraint);
            if (values.size() != pTimes.size() + 1)
                throw InvalidParams("a piecewise parameter needs one value more than its step times");
            for (Size i = 0; i < values.size(); ++i)
                p.setParam(i, values[i]);
            return p;
        }

        const Register unit({
            {"AnalyticPTDHestonEngine::chF",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 equity::ProcessSpec p = equity::process(a[0]);
                 std::vector<Real> grid = reals(a[1]), pTimes = reals(a[2]), theta = reals(a[3]), kappa = reals(a[4]),
                                   sigma = reals(a[5]), rho = reals(a[6]);
                 std::vector<std::complex<Real>> zs = complexValues(a[7]);
                 std::vector<Real> ts = reals(a[8]);
                 return [=] {
                     ext::shared_ptr<HestonProcess> process = equity::makeProcess(p);
                     auto model = ext::make_shared<PiecewiseTimeDependentHestonModel>(
                         process->riskFreeRate(), process->dividendYield(), process->s0(), p.v0,
                         parameter(theta, pTimes, PositiveConstraint()), parameter(kappa, pTimes, PositiveConstraint()),
                         parameter(sigma, pTimes, PositiveConstraint()),
                         parameter(rho, pTimes, BoundaryConstraint(-1.0, 1.0)), TimeGrid(grid.begin(), grid.end()));
                     AnalyticPTDHestonEngine engine(model);
                     json::array chF, lnChF;
                     for (const std::complex<Real>& z : zs)
                         for (Real t : ts) {
                             chF.push_back(complexResult(engine.chF(z, t)));
                             lnChF.push_back(complexResult(engine.lnChF(z, t)));
                         }
                     json::object out;
                     out["chF"] = chF;
                     out["lnChF"] = lnChF;
                     return json::value(out);
                 };
             }},
        });

    }

}

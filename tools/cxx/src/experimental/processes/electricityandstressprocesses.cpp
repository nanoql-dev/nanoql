// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/processes/gemanroncoroniprocess.hpp, .cpp, extendedblackscholesprocess.hpp, .cpp and
// vegastressedblackscholesprocess.hpp, .cpp: the three processes, which the wheel doesn't bind.
//
// "GemanRoncoroniProcess::paths" takes (paths, steps): vpp.cpp's testGemanRoncoroniProcess set-up, its Geman-Roncoroni
// process and extended Ornstein-Uhlenbeck process in a StochasticProcessArray correlated 0.25, on its 250 steps a year
// to ten years and PseudoRandom's generator at seed 421; it returns {"electricity", "fuel"}, the first paths' first
// steps in turn.
//
// "ExtendedBlackScholesMertonProcess::values" takes (spot, r, q, vol, discretization, points), the points [[t, x, dt,
// dw], ...], on flat curves and a constant volatility from 16 October 2026 on Actual/365 (Fixed), the discretization
// QuantLib's integer; it returns {"drift", "diffusion", "evolve"}, each point's in turn.
//
// "VegaStressedBlackScholesProcess::values" takes (spot, r, q, vol, box, points), the box [lower time, upper time,
// lower asset, upper asset, stress]; it returns {"drift", "diffusion", "expectation", "stdDeviation", "variance",
// "evolve"}, each point's in turn. "VegaStressedBlackScholesProcess::setters" takes the same, builds the process on
// QuantLib's default box and sets the box through its five setters; it returns {"box", "diffusion"}, the box read back
// and each point's diffusion.
//
// "GemanRoncoroniProcess::values" takes (points), the points [[t, x, dt], ...], on vpp.cpp's process; it returns
// {"drift", "diffusion", "stdDeviation"}, each point's in turn.

#include "convert.hpp"

#include <ql/experimental/processes/extendedblackscholesprocess.hpp>
#include <ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp>
#include <ql/experimental/processes/gemanroncoroniprocess.hpp>
#include <ql/experimental/processes/vegastressedblackscholesprocess.hpp>
#include <ql/math/randomnumbers/rngtraits.hpp>
#include <ql/methods/montecarlo/multipathgenerator.hpp>
#include <ql/processes/stochasticprocessarray.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <cmath>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Date reference(16, October, 2026);

        Handle<YieldTermStructure> flat(Real r) {
            return Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, r, Actual365Fixed()));
        }

        Handle<BlackVolTermStructure> constant(Real vol) {
            return Handle<BlackVolTermStructure>(
                ext::make_shared<BlackConstantVol>(reference, NullCalendar(), vol, Actual365Fixed()));
        }

        std::vector<std::vector<Real>> rows(const json::value& v, std::size_t width) {
            if (!v.is_array())
                throw InvalidParams("expected a list of points, got " + json::serialize(v));
            std::vector<std::vector<Real>> out;
            for (const json::value& p : v.get_array()) {
                std::vector<Real> r = reals(p);
                if (r.size() != width)
                    throw InvalidParams("a point takes " + std::to_string(width) + " numbers");
                out.push_back(r);
            }
            return out;
        }

        struct linear {
            Real alpha, beta;
            Real operator()(Real x) const { return alpha + beta * x; }
        };

        const Register unit({
            {"GemanRoncoroniProcess::paths",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Size nPaths = static_cast<Size>(integer(a[0]));
                 Size nSteps = static_cast<Size>(integer(a[1]));
                 return [=] {
                     auto grProcess = ext::make_shared<GemanRoncoroniProcess>(
                         3.3, 3.1, 0.05, -0.09, 0.07, -0.40, -0.40, 1.6, 1.0, 0.5, 10.0, -7.0, -0.3, 35.0, 9.0, 0.10, 1.9);
                     ext::shared_ptr<StochasticProcess1D> eouProcess(new ExtendedOrnsteinUhlenbeckProcess(
                         5.0, std::sqrt(1.4), 1.1, linear{1.0, 0.08}, ExtendedOrnsteinUhlenbeckProcess::Trapezodial));
                     std::vector<ext::shared_ptr<StochasticProcess1D>> processes = {grProcess, eouProcess};
                     Matrix correlation(2, 2, 1.0);
                     correlation[0][1] = correlation[1][0] = 0.25;
                     auto pArray = ext::make_shared<StochasticProcessArray>(processes, correlation);
                     const Time T = 10.0;
                     const Size steps = 2500;
                     TimeGrid grid(T, steps);
                     auto rsg = PseudoRandom::make_sequence_generator(pArray->size() * (grid.size() - 1),
                                                                      BigNatural(421));
                     MultiPathGenerator<PseudoRandom::rsg_type> generator(pArray, grid, rsg, false);
                     json::array electricity, fuel;
                     for (Size n = 0; n < nPaths; ++n) {
                         const MultiPath& path = generator.next().value;
                         for (Size i = 0; i <= nSteps; ++i) {
                             electricity.emplace_back(path[0][i]);
                             fuel.emplace_back(path[1][i]);
                         }
                     }
                     json::object out;
                     out["electricity"] = electricity;
                     out["fuel"] = fuel;
                     return json::value(out);
                 };
             }},
            {"ExtendedBlackScholesMertonProcess::values",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real spot = real(a[0]), r = real(a[1]), q = real(a[2]), vol = real(a[3]);
                 auto d = enumeration<ExtendedBlackScholesMertonProcess::Discretization>(a[4]);
                 auto points = rows(a[5], 4);
                 return [=] {
                     ExtendedBlackScholesMertonProcess p(Handle<Quote>(ext::make_shared<SimpleQuote>(spot)), flat(q),
                                                         flat(r), constant(vol),
                                                         ext::shared_ptr<StochasticProcess1D::discretization>(
                                                             new EulerDiscretization),
                                                         d);
                     json::array drift, diffusion, evolve;
                     for (const auto& x : points) {
                         drift.emplace_back(p.drift(x[0], x[1]));
                         diffusion.emplace_back(p.diffusion(x[0], x[1]));
                         evolve.emplace_back(p.evolve(x[0], x[1], x[2], x[3]));
                     }
                     json::object out;
                     out["drift"] = drift;
                     out["diffusion"] = diffusion;
                     out["evolve"] = evolve;
                     return json::value(out);
                 };
             }},
            {"VegaStressedBlackScholesProcess::values",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real spot = real(a[0]), r = real(a[1]), q = real(a[2]), vol = real(a[3]);
                 std::vector<Real> box = reals(a[4]);
                 if (box.size() != 5)
                     throw InvalidParams("a stress box is [lower time, upper time, lower asset, upper asset, stress]");
                 auto points = rows(a[5], 4);
                 return [=] {
                     VegaStressedBlackScholesProcess p(Handle<Quote>(ext::make_shared<SimpleQuote>(spot)), flat(q),
                                                       flat(r), constant(vol), box[0], box[1], box[2], box[3], box[4]);
                     json::array drift, diffusion, expectation, stdDeviation, variance, evolve;
                     for (const auto& x : points) {
                         drift.emplace_back(p.drift(x[0], x[1]));
                         diffusion.emplace_back(p.diffusion(x[0], x[1]));
                         expectation.emplace_back(p.expectation(x[0], x[1], x[2]));
                         stdDeviation.emplace_back(p.stdDeviation(x[0], x[1], x[2]));
                         variance.emplace_back(p.variance(x[0], x[1], x[2]));
                         evolve.emplace_back(p.evolve(x[0], x[1], x[2], x[3]));
                     }
                     json::object out;
                     out["drift"] = drift;
                     out["diffusion"] = diffusion;
                     out["expectation"] = expectation;
                     out["stdDeviation"] = stdDeviation;
                     out["variance"] = variance;
                     out["evolve"] = evolve;
                     return json::value(out);
                 };
             }},
            {"VegaStressedBlackScholesProcess::setters",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real spot = real(a[0]), r = real(a[1]), q = real(a[2]), vol = real(a[3]);
                 std::vector<Real> box = reals(a[4]);
                 if (box.size() != 5)
                     throw InvalidParams("a stress box is [lower time, upper time, lower asset, upper asset, stress]");
                 auto points = rows(a[5], 4);
                 return [=] {
                     VegaStressedBlackScholesProcess p(Handle<Quote>(ext::make_shared<SimpleQuote>(spot)), flat(q),
                                                       flat(r), constant(vol));
                     p.setLowerTimeBorderForStressTest(box[0]);
                     p.setUpperTimeBorderForStressTest(box[1]);
                     p.setLowerAssetBorderForStressTest(box[2]);
                     p.setUpperAssetBorderForStressTest(box[3]);
                     p.setStressLevel(box[4]);
                     json::array diffusion;
                     for (const auto& x : points)
                         diffusion.emplace_back(p.diffusion(x[0], x[1]));
                     json::object out;
                     out["box"] = json::array{p.getLowerTimeBorderForStressTest(), p.getUpperTimeBorderForStressTest(),
                                              p.getLowerAssetBorderForStressTest(),
                                              p.getUpperAssetBorderForStressTest(), p.getStressLevel()};
                     out["diffusion"] = diffusion;
                     return json::value(out);
                 };
             }},
            {"GemanRoncoroniProcess::values",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto points = rows(a[0], 3);
                 return [=] {
                     GemanRoncoroniProcess p(3.3, 3.1, 0.05, -0.09, 0.07, -0.40, -0.40, 1.6, 1.0, 0.5, 10.0, -7.0, -0.3,
                                             35.0, 9.0, 0.10, 1.9);
                     json::array drift, diffusion, stdDeviation;
                     for (const auto& x : points) {
                         drift.emplace_back(p.drift(x[0], x[1]));
                         diffusion.emplace_back(p.diffusion(x[0], x[1]));
                         stdDeviation.emplace_back(p.stdDeviation(x[0], x[1], x[2]));
                     }
                     json::object out;
                     out["drift"] = drift;
                     out["diffusion"] = diffusion;
                     out["stdDeviation"] = stdDeviation;
                     return json::value(out);
                 };
             }},
        });

    }

}

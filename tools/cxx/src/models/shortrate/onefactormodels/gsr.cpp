// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/shortrate/onefactormodels/gsr.hpp and gaussian1dmodel.hpp: the members the wheel doesn't bind, on a GSR
// spec (gsrspec.hpp): numeraireTime(), yGrid(stdDevs, gridPoints, T, t, y), the calibration flags FixedReversions(),
// FixedVolatilities(), MoveVolatility(i) and MoveReversion(i), and Gaussian1dModel's static gaussianPolynomialIntegral
// and gaussianShiftedPolynomialIntegral.

#include "convert.hpp"
#include "models/shortrate/onefactormodels/gsrspec.hpp"

#include <ql/models/shortrate/onefactormodels/gaussian1dmodel.hpp>
#include <ql/models/shortrate/onefactormodels/gsr.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value flags(const std::vector<bool>& f) {
            json::array out;
            for (bool b : f)
                out.emplace_back(b);
            return json::value(std::move(out));
        }

        Size index(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected an index, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            {"Gsr::numeraireTime",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 gaussian1d::GsrSpec s = gaussian1d::gsr(a[0]);
                 return [=] { return json::value(gaussian1d::makeGsr(s)->numeraireTime()); };
             }},
            // yGrid(model, stdDevs, gridPoints, T, t, y)
            {"Gsr::yGrid",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 gaussian1d::GsrSpec s = gaussian1d::gsr(a[0]);
                 Real stdDevs = real(a[1]);
                 Integer gridPoints = integer(a[2]);
                 Real T = real(a[3]), t = real(a[4]), y = real(a[5]);
                 return [=] {
                     Array grid = gaussian1d::makeGsr(s)->yGrid(stdDevs, gridPoints, T, t, y);
                     json::array out;
                     for (Real x : grid)
                         out.emplace_back(x);
                     return json::value(std::move(out));
                 };
             }},
            {"Gsr::FixedReversions",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 gaussian1d::GsrSpec s = gaussian1d::gsr(a[0]);
                 return [=] { return flags(gaussian1d::makeGsr(s)->FixedReversions()); };
             }},
            {"Gsr::FixedVolatilities",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 gaussian1d::GsrSpec s = gaussian1d::gsr(a[0]);
                 return [=] { return flags(gaussian1d::makeGsr(s)->FixedVolatilities()); };
             }},
            {"Gsr::MoveVolatility",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 gaussian1d::GsrSpec s = gaussian1d::gsr(a[0]);
                 Size i = index(a[1]);
                 return [=] { return flags(gaussian1d::makeGsr(s)->MoveVolatility(i)); };
             }},
            {"Gsr::MoveReversion",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 gaussian1d::GsrSpec s = gaussian1d::gsr(a[0]);
                 Size i = index(a[1]);
                 return [=] { return flags(gaussian1d::makeGsr(s)->MoveReversion(i)); };
             }},
            // gaussianPolynomialIntegral(a, b, c, d, e, x0, x1)
            {"Gaussian1dModel::gaussianPolynomialIntegral",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::vector<Real> x;
                 for (const auto& v : a)
                     x.push_back(real(v));
                 return [=] {
                     return json::value(
                         Gaussian1dModel::gaussianPolynomialIntegral(x[0], x[1], x[2], x[3], x[4], x[5], x[6]));
                 };
             }},
            // gaussianShiftedPolynomialIntegral(a, b, c, d, e, h, x0, x1)
            {"Gaussian1dModel::gaussianShiftedPolynomialIntegral",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 std::vector<Real> x;
                 for (const auto& v : a)
                     x.push_back(real(v));
                 return [=] {
                     return json::value(Gaussian1dModel::gaussianShiftedPolynomialIntegral(x[0], x[1], x[2], x[3],
                                                                                          x[4], x[5], x[6], x[7]));
                 };
             }},
        });

    }

}

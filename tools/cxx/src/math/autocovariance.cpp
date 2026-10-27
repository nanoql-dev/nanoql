// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/autocovariance.hpp, which the wheel doesn't expose. "convolutions", "autocovariances" and "autocorrelations"
// take (data, maxLag): the maxLag + 1 values. "autocovariancesReuse" and "autocorrelationsReuse" take (data, maxLag,
// reuse): the mean, the values, and the data after the call (centred where reuse is true).

#include "convert.hpp"

#include <ql/math/autocovariance.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value array(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return out;
        }

        Size lag(const json::value& v) {
            Integer l = integer(v);
            if (l < 0)
                throw InvalidParams("expected a non-negative lag");
            return static_cast<Size>(l);
        }

        const Register unit({
            {"convolutions",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> data = reals(a[0]);
                 Size maxLag = lag(a[1]);
                 return [=] {
                     std::vector<Real> out(maxLag + 1);
                     convolutions(data.begin(), data.end(), out.begin(), maxLag);
                     return array(out);
                 };
             }},
            {"autocovariances",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> data = reals(a[0]);
                 Size maxLag = lag(a[1]);
                 return [=] {
                     std::vector<Real> out(maxLag + 1);
                     autocovariances(data.begin(), data.end(), out.begin(), maxLag);
                     return array(out);
                 };
             }},
            {"autocorrelations",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::vector<Real> data = reals(a[0]);
                 Size maxLag = lag(a[1]);
                 return [=] {
                     std::vector<Real> out(maxLag + 1);
                     autocorrelations(data.begin(), data.end(), out.begin(), maxLag);
                     return array(out);
                 };
             }},
            {"autocovariancesReuse",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> data = reals(a[0]);
                 Size maxLag = lag(a[1]);
                 bool reuse = boolean(a[2]);
                 return [=] {
                     std::vector<Real> d = data, out(maxLag + 1);
                     Real mean = autocovariances(d.begin(), d.end(), out.begin(), maxLag, reuse);
                     json::object o;
                     o["mean"] = mean;
                     o["values"] = array(out);
                     o["data"] = array(d);
                     return json::value(o);
                 };
             }},
            {"autocorrelationsReuse",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<Real> data = reals(a[0]);
                 Size maxLag = lag(a[1]);
                 bool reuse = boolean(a[2]);
                 return [=] {
                     std::vector<Real> d = data, out(maxLag + 1);
                     Real mean = autocorrelations(d.begin(), d.end(), out.begin(), maxLag, reuse);
                     json::object o;
                     o["mean"] = mean;
                     o["values"] = array(out);
                     o["data"] = array(d);
                     return json::value(o);
                 };
             }},
        });

    }

}

// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// One Excel instance at a time: the tests of instances of their own run one by one, before the runner's.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

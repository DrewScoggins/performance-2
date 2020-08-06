// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using BenchmarkDotNet.Attributes;
using System;
using MicroBenchmarks;

namespace Benchstone.BenchF
{
[BenchmarkCategory(Categories.Runtime, Categories.Benchstones, Categories.JIT, Categories.BenchF)]
public class BenchMrk
{
    public const int Iterations = 4000000;
    
    private static int s_i, s_n;
    private static float s_p, s_a, s_x, s_f, s_e;

    [Benchmark(Description = nameof(BenchMrk))]
    public bool Test()
    {
        s_p = (float)Math.Acos(-1.0);
        s_a = 0.0F;
        s_n = Iterations;
        s_f = s_p / s_n;
        for (s_i = 1; s_i <= s_n; ++s_i)
        {
            s_f = s_p / s_n;
            s_x = s_f * s_i;
            s_e = (float)(Math.Abs(Math.Log(Math.Exp(s_x)) / s_x) - Math.Sqrt((Math.Sin(s_x) * Math.Sin(s_x)) + Math.Cos(s_x) * Math.Cos(s_x)));
            s_a = s_a + Math.Abs(s_e);
        }

        return true;
    }
}
}

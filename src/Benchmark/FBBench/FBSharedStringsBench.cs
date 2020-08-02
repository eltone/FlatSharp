﻿/*
 * Copyright 2020 James Courtney
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Benchmark.FBBench
{
    using BenchmarkDotNet.Attributes;
    using FlatSharp;
    using FlatSharp.Attributes;
    using FlatSharp.Unsafe;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [ShortRunJob]
    public class FBSharedStringBench
    {
        public VectorTable<string> nonSharedStringVector;
        public VectorTable<SharedString> randomSharedStringVector;
        public VectorTable<SharedString> guassianSharedStringVector;

        public byte[] serializedGuassianStringVector = new byte[10 * 1024 * 1024];
        public byte[] serializedRandomStringVector = new byte[10 * 1024 * 1024];
        public byte[] serializedNonSharedStringVector = new byte[10 * 1024 * 1024];

        [Params(
            //10, 
            //50, 
            //100, 
            //250, 
            500,
            1001)]
        public int LruLookbackSize { get; set; }

        [Params(1000)]
        public int VectorLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            Random random = new Random();

            List<string> guids =
                Enumerable.Range(0, this.VectorLength)
                .Select(i => Guid.NewGuid().ToString()).ToList();

            List<string> randomGuids = Enumerable.Range(0, this.VectorLength)
                .Select(i => guids[random.Next(0, guids.Count)]).ToList();

            List<string> guassianGuids = Enumerable.Range(0, this.VectorLength)
                .Select(i => guids[NextGaussianInt(random, 0, guids.Count - 1)]).ToList();

            nonSharedStringVector = new VectorTable<string> { Vector = randomGuids.ToList() };
            randomSharedStringVector = new VectorTable<SharedString> { Vector = randomGuids.Select(SharedString.Create).ToList() };
            guassianSharedStringVector = new VectorTable<SharedString> { Vector = guassianGuids.Select(SharedString.Create).ToList() };

            int nonSharedSize = this.Serialize_NonSharedString();
            int guassianLruSharedSize = this.Serialize_GuassianStringVector_LRU();
            int guassianPlruSharedSize = this.Serialize_GuassianStringVector_PLRU();

            Console.WriteLine($"NonShared: {nonSharedSize}");
            Console.WriteLine($"GuassianLru: {guassianLruSharedSize}");
            Console.WriteLine($"GuassianPlru: {guassianPlruSharedSize}");
        }

        [Benchmark]
        public int Serialize_NonSharedString()
        {
            return FlatBufferSerializer.Default.Serialize(
                nonSharedStringVector,
                serializedNonSharedStringVector,
                SpanWriter.Instance);
        }

        [Benchmark]
        public int Serialize_RandomSharedString_LRU()
        {
            return FlatBufferSerializer.Default.Serialize(
                randomSharedStringVector,
                this.serializedRandomStringVector,
                new SpanWriter(this.LruLookbackSize));
        }


        [Benchmark]
        public int Serialize_RandomSharedString_PLRU()
        {
            return FlatBufferSerializer.Default.Serialize(
                randomSharedStringVector,
                this.serializedRandomStringVector,
                new SpanWriter(new SharedStringWriter(new JumerLru<string, LinkedList<int>>(this.LruLookbackSize))));
        }

        [Benchmark]
        public int Serialize_GuassianStringVector_LRU()
        {
            return FlatBufferSerializer.Default.Serialize(
                guassianSharedStringVector,
                this.serializedGuassianStringVector,
                new SpanWriter(this.LruLookbackSize));
        }


        [Benchmark]
        public int Serialize_GuassianStringVector_PLRU()
        {
            return FlatBufferSerializer.Default.Serialize(
                guassianSharedStringVector,
                this.serializedGuassianStringVector,
                new SpanWriter(new SharedStringWriter(new JumerLru<string, LinkedList<int>>(this.LruLookbackSize))));
        }

        [Benchmark]
        public void Parse_NonSharedStringVector_FromNonSharedSource()
        {
            FlatBufferSerializer.Default.Parse<VectorTable<string>>(
                this.serializedNonSharedStringVector);
        }

        //[Benchmark]
        public void Parse_NonSharedStringVector_FromRandomSharedSource()
        {
            FlatBufferSerializer.Default.Parse<VectorTable<string>>(
                this.serializedRandomStringVector);
        }

        //[Benchmark]
        public void Parse_NonSharedStringVector_FromGuassianRandomStringSource()
        {
            FlatBufferSerializer.Default.Parse<VectorTable<string>>(
                this.serializedGuassianStringVector);
        }

        [Benchmark]
        public void Parse_SharedStringVector_FromNonSharedSource()
        {
            InputBuffer buffer = new ArrayInputBuffer(this.serializedRandomStringVector);
            buffer.SharedStringLruCache = new LruCache<int, SharedString>(this.LruLookbackSize);

            FlatBufferSerializer.Default.Parse<VectorTable<SharedString>>(buffer);
        }

        [Benchmark]
        public void Parse_SharedStringVector_FromGuassianRandomStringSource_LRU()
        {
            InputBuffer buffer = new ArrayInputBuffer(this.serializedGuassianStringVector);
            buffer.SharedStringLruCache = new LruCache<int, SharedString>(this.LruLookbackSize);

            FlatBufferSerializer.Default.Parse<VectorTable<SharedString>>(buffer);
        }


        [Benchmark]
        public void Parse_SharedStringVector_FromGuassianRandomStringSource_PLRU()
        {
            InputBuffer buffer = new ArrayInputBuffer(this.serializedGuassianStringVector);
            buffer.SharedStringLruCache = new JumerLru<int, SharedString>(this.LruLookbackSize);

            FlatBufferSerializer.Default.Parse<VectorTable<SharedString>>(buffer);
        }

        [FlatBufferTable]
        public class VectorTable<T>
        {
            [FlatBufferItem(0)]
            public virtual IList<T> Vector { get; set; }
        }

        public static int NextGaussianInt(Random r, int min, int max)
        {
            double random = NextGaussian(r, (max - min) / 2.0, (max - min) / 6.0); // Gives a nice distribution.
            return Math.Max(min, Math.Min(max, (int)random));
        }

        public static double NextGaussian(Random r, double mu = 0, double sigma = 1)
        {
            var u1 = r.NextDouble();
            var u2 = r.NextDouble();

            var rand_std_normal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                Math.Sin(2.0 * Math.PI * u2);

            var rand_normal = mu + sigma * rand_std_normal;

            return rand_normal;
        }
    }
}
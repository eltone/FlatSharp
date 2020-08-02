﻿/*
 * Copyright 2018 James Courtney
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

namespace Benchmark
{
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Exporters;
    using BenchmarkDotNet.Jobs;
    using BenchmarkDotNet.Loggers;
    using BenchmarkDotNet.Running;

    public class Program
    {
        public static void Main(string[] args)
        {
            //var summary = BenchmarkRunner.Run<FBBench.FBSerializeBench>();
            //var summary2 = BenchmarkRunner.Run<FBBench.FBDeserializeBench>();
            //var summary3 = BenchmarkRunner.Run<FBBench.OthersDeserializeBench>();
            var summary4 = BenchmarkRunner.Run<FBBench.FBSharedStringBench>();

            //MarkdownExporter.Console.ExportToLog(summary, new ConsoleLogger());
            //MarkdownExporter.Console.ExportToLog(summary2, new ConsoleLogger());
            //MarkdownExporter.Console.ExportToLog(summary3, new ConsoleLogger());
            //MarkdownExporter.Console.ExportToLog(summary4, new ConsoleLogger());

            //MarkdownExporter.GitHub.ExportToFiles(summary, new ConsoleLogger());
            //MarkdownExporter.GitHub.ExportToFiles(summary2, new ConsoleLogger());
            //MarkdownExporter.GitHub.ExportToFiles(summary3, new ConsoleLogger());
            //MarkdownExporter.GitHub.ExportToFiles(summary4, new ConsoleLogger());
        }
    }
}

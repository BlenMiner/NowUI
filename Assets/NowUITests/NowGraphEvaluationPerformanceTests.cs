using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using NowUI.NodeGraph;

/// <summary>
/// CPU-only graph evaluation. Graph construction, string IDs and link validation
/// are outside measurement; indexed cases include index construction/disposal.
/// A changing source value catches stale cross-frame memos;
/// callback counts distinguish real shared evaluation from missing dependencies.
/// </summary>
public class NowGraphEvaluationPerformanceTests
{
    const int ConstantKind = 1;
    const int IncrementKind = 2;
    const int AllocationSamples = 8;

    sealed class EvaluationWorkload
    {
        public readonly NowNodeGraph graph = new NowNodeGraph();
        public readonly NowNodeGraphEvaluator<int> evaluator;
        public readonly string[] roots;
        public readonly NowNode source;
        public int handlerCalls;
        public int checksum;
        readonly int _chainLength;
        readonly bool _batched;
        readonly bool _indexed;
        public bool indexed => _indexed;

        public EvaluationWorkload(int chainLength, int rootCount, bool batched, bool rootsAfterChain, bool indexed = false)
        {
            _chainLength = chainLength;
            _batched = batched;
            _indexed = indexed;
            evaluator = new NowNodeGraphEvaluator<int>()
                .Kind(ConstantKind, ctx => { ++handlerCalls; return ctx.node.userId; })
                .Kind(IncrementKind, ctx => { ++handlerCalls; return ctx.Input("in", -1000000) + 1; });
            // The 512-node stress case intentionally exceeds the default guard.
            // Raise it explicitly so a truncated walk cannot look like a speedup.
            evaluator.maximumDepth = chainLength + 2;
            source = AddNode("source", ConstantKind);
            string previous = source.id;
            for (int i = 1; i < chainLength; ++i)
            {
                var node = AddNode("chain-" + i, IncrementKind);
                Assert.IsTrue(graph.TryAddLink(previous, "out", node.id, "in"));
                previous = node.id;
            }

            roots = new string[rootCount];
            for (int i = 0; i < rootCount; ++i)
            {
                if (rootsAfterChain)
                {
                    var node = AddNode("root-" + i, IncrementKind);
                    Assert.IsTrue(graph.TryAddLink(previous, "out", node.id, "in"));
                    roots[i] = node.id;
                }
                else
                    roots[i] = previous;
            }
        }

        NowNode AddNode(string id, int kind)
        {
            var node = graph.AddNode(id, id, Vector2.zero);
            node.kindId = kind;
            if (kind == IncrementKind)
                node.AddInput("in", "In");
            node.AddOutput("out", "Out");
            return node;
        }

        public void Evaluate()
        {
            source.userId = source.userId == 1 ? 2 : 1;
            handlerCalls = 0;
            checksum = 0;
            if (_batched)
            {
                using (_indexed ? evaluator.BeginIndexedBatch(graph) : evaluator.BeginBatch())
                    EvaluateRoots();
            }
            else
                EvaluateRoots();
        }

        void EvaluateRoots()
        {
            for (int i = 0; i < roots.Length; ++i)
            {
                if (_indexed && !_batched)
                {
                    // Each independent evaluation pays index construction;
                    // no precomputed topology is hidden outside the timer.
                    using (evaluator.BeginIndexedBatch(graph))
                        checksum += evaluator.Evaluate(graph, roots[i]);
                }
                else
                    checksum += evaluator.Evaluate(graph, roots[i]);
            }
        }

        public void Verify(bool rootsAfterChain)
        {
            int perRoot = _chainLength + (rootsAfterChain ? 1 : 0);
            Assert.AreEqual((source.userId + perRoot - 1) * roots.Length, checksum);
            int expectedCalls = _batched
                ? _chainLength + (rootsAfterChain ? roots.Length : 0)
                : perRoot * roots.Length;
            Assert.AreEqual(expectedCalls, handlerCalls, "Every dependency must run, with memoization scoped to this operation.");
            Assert.AreEqual(_chainLength + (rootsAfterChain ? roots.Length : 0), graph.nodes.Count);
            Assert.AreEqual(graph.nodes.Count - 1, graph.links.Count);
        }
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(16)]
    [TestCase(128)]
    [TestCase(512)]
    public void DependencyChainCpuEvaluation(int nodeCount)
    {
        MeasureWorkload(new EvaluationWorkload(nodeCount, 1, false, false), false);
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(16)]
    [TestCase(128)]
    [TestCase(512)]
    public void DependencyChainIndexedCpuEvaluation(int nodeCount)
    {
        MeasureWorkload(new EvaluationWorkload(nodeCount, 1, false, false, true), false);
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(4, false)]
    [TestCase(16, false)]
    [TestCase(64, false)]
    [TestCase(4, true)]
    [TestCase(16, true)]
    [TestCase(64, true)]
    public void SharedDependenciesCpuEvaluation(int rootCount, bool batched)
    {
        MeasureWorkload(new EvaluationWorkload(64, rootCount, batched, true), true);
    }

    [Test, Performance, Category("NowUI.Overview")]
    [TestCase(4, false)]
    [TestCase(16, false)]
    [TestCase(64, false)]
    [TestCase(4, true)]
    [TestCase(16, true)]
    [TestCase(64, true)]
    public void SharedDependenciesIndexedCpuEvaluation(int rootCount, bool batched)
    {
        MeasureWorkload(new EvaluationWorkload(64, rootCount, batched, true, true), true);
    }

    static void MeasureWorkload(EvaluationWorkload workload, bool rootsAfterChain)
    {
        workload.Evaluate();
        workload.Verify(rootsAfterChain);
        workload.Evaluate();
        workload.Verify(rootsAfterChain);
        Action operation = workload.Evaluate;
        Measure.Method(operation)
            .SampleGroup(new SampleGroup("CPU.Evaluate", SampleUnit.Millisecond, false))
            .WarmupCount(5).MeasurementCount(64).IterationsPerMeasurement(1).Run();

        using var allocations = new NowBenchmarkAllocations();
        allocations.Begin();
        for (int i = 0; i < AllocationSamples; ++i)
            operation();
        long allocated = allocations.End();
        allocations.Report(allocated / (double)AllocationSamples);
        workload.Verify(rootsAfterChain);
        Counter("Graph.Nodes", workload.graph.nodes.Count);
        Counter("Graph.Links", workload.graph.links.Count);
        Counter("Graph.Roots", workload.roots.Length);
        Counter("Graph.HandlerCalls", workload.handlerCalls);
        Counter("Graph.Checksum", workload.checksum);
        Counter("Graph.Indexed", workload.indexed ? 1 : 0);
    }

    static void Counter(string name, int value)
    {
        Measure.Custom(new SampleGroup(name, SampleUnit.Undefined, false), value);
    }
}

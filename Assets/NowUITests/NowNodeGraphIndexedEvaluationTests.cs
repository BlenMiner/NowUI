using System;
using System.Collections.Generic;
using NUnit.Framework;
using NowUI.NodeGraph;
using UnityEngine;

public class NowNodeGraphIndexedEvaluationTests
{
    static NowNode Add(NowNodeGraph graph, string id, int kind, int value = 0)
    {
        var node = graph.AddNode(id, id, Vector2.zero);
        node.kindId = kind;
        node.userId = value;
        node.AddInput("in", "In");
        node.AddOutput("out", "Out");
        return node;
    }

    static void Link(NowNodeGraph graph, string source, string target)
    {
        graph.links.Add(new NowNodeLink(source, "out", target, "in"));
    }

    static NowNodeGraphEvaluator<int> Evaluator() => new NowNodeGraphEvaluator<int>()
        .Kind(1, ctx => ctx.node.userId)
        .Kind(2, ctx => ctx.Input("in", 7) + 1);

    [TestCase(false)]
    [TestCase(true)]
    public void LookupModesKeepFallbackPortsAndHandlerSemantics(bool indexed)
    {
        var graph = new NowNodeGraph();
        Add(graph, "source", 1, 12);
        Add(graph, "root", 2);
        Add(graph, "unconnected", 2);
        Add(graph, "unknown-kind", 9);
        Link(graph, "source", "root");
        var evaluator = Evaluator();
        using (indexed ? evaluator.BeginIndexedBatch(graph) : evaluator.BeginBatch())
        {
            Assert.AreEqual(13, evaluator.Evaluate(graph, "root"));
            Assert.AreEqual(8, evaluator.Evaluate(graph, "unconnected"));
            Assert.IsFalse(evaluator.TryEvaluate(graph, "missing", out _));
            Assert.IsFalse(evaluator.TryEvaluate(graph, "source", "missing-port", -1, out _));
            Assert.IsFalse(evaluator.TryEvaluate(graph, "unknown-kind", out _));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LookupModesKeepCycleAndDepthFallbacks(bool indexed)
    {
        var graph = new NowNodeGraph();
        Add(graph, "a", 2);
        Add(graph, "b", 2);
        Add(graph, "c", 2);
        Link(graph, "b", "a");
        Link(graph, "c", "b");
        Link(graph, "a", "c");
        var evaluator = Evaluator();
        using (indexed ? evaluator.BeginIndexedBatch(graph) : evaluator.BeginBatch())
            Assert.AreEqual(10, evaluator.Evaluate(graph, "a"));
        evaluator.maximumDepth = 2;
        using (indexed ? evaluator.BeginIndexedBatch(graph) : evaluator.BeginBatch())
            Assert.AreEqual(9, evaluator.Evaluate(graph, "a"));
    }

    [Test]
    public void IndexedScopesRebuildAfterDirectIdentityPortAndListEdits()
    {
        var graph = new NowNodeGraph();
        var first = Add(graph, "source", 1, 10);
        var duplicate = Add(graph, "source", 1, 20);
        Add(graph, "root", 2);
        Link(graph, "source", "root");
        var evaluator = Evaluator();
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(11, evaluator.Evaluate(graph, "root"));

        graph.nodes[0] = duplicate;
        graph.nodes[1] = first;
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(21, evaluator.Evaluate(graph, "root"), "Same-count reordering must change first-match resolution.");

        duplicate.id = "renamed";
        duplicate.outputs[0].id = "renamed-port";
        graph.links[0] = new NowNodeLink("renamed", "renamed-port", "root", "in");
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(21, evaluator.Evaluate(graph, "root"), "Direct ID/port fields must refresh at the next scope.");

        var replacement = new NowNode("renamed", "Replacement", Vector2.zero) { kindId = 1, userId = 30 };
        replacement.AddOutput("renamed-port", "Out");
        graph.nodes = new List<NowNode> { replacement, graph.nodes[2] };
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(31, evaluator.Evaluate(graph, "root"));
    }

    [Test]
    public void IndexedInputLookupKeepsFirstDuplicateAndRefreshesReorderedLinks()
    {
        var graph = new NowNodeGraph();
        Add(graph, "first", 1, 10);
        Add(graph, "second", 1, 20);
        Add(graph, "root", 2);
        Link(graph, "first", "root");
        Link(graph, "second", "root");
        var evaluator = Evaluator().Kind(2, ctx => ctx.HasInput("in") ? ctx.Input("in") : -100);
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(10, evaluator.Evaluate(graph, "root"));
        graph.links.Reverse();
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(20, evaluator.Evaluate(graph, "root"));
        graph.links.Clear();
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(-100, evaluator.Evaluate(graph, "root"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExistingEvaluationStillFollowsTopologyEditsInsideHandlers(bool batch)
    {
        var graph = new NowNodeGraph();
        Add(graph, "first", 1, 10);
        Add(graph, "second", 1, 20);
        Add(graph, "root", 2);
        Link(graph, "first", "root");
        var evaluator = Evaluator().Kind(2, ctx =>
        {
            int first = ctx.Input("in");
            ctx.graph.links[0] = new NowNodeLink("second", "out", "root", "in");
            return first + (ctx.HasInput("in") ? ctx.Input("in") : -100);
        });
        if (batch)
        {
            using (evaluator.BeginBatch())
                Assert.AreEqual(30, evaluator.Evaluate(graph, "root"));
        }
        else
            Assert.AreEqual(30, evaluator.Evaluate(graph, "root"));
    }

    [Test]
    public void NestedIndexedAndOrdinaryBatchesKeepMemoUntilOutermostDisposal()
    {
        var graph = new NowNodeGraph();
        var source = Add(graph, "source", 1, 10);
        var evaluator = Evaluator();
        using (evaluator.BeginIndexedBatch(graph))
        {
            Assert.AreEqual(10, evaluator.Evaluate(graph, "source"));
            source.userId = 20;
            using (evaluator.BeginIndexedBatch(graph))
            using (evaluator.BeginBatch())
                Assert.AreEqual(10, evaluator.Evaluate(graph, "source"));
            Assert.AreEqual(10, evaluator.Evaluate(graph, "source"));
        }
        Assert.AreEqual(20, evaluator.Evaluate(graph, "source"));
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(20, evaluator.Evaluate(graph, "source"));
    }

    [Test]
    public void IndexedScopeRejectsOtherGraphsAndLiveModeTransitions()
    {
        var graph = new NowNodeGraph();
        var other = new NowNodeGraph();
        Add(graph, "same", 1, 10);
        Add(other, "same", 1, 20);
        var evaluator = Evaluator();
        Assert.Throws<ArgumentNullException>(() => evaluator.BeginIndexedBatch(null));
        using (evaluator.BeginBatch())
            Assert.Throws<InvalidOperationException>(() => evaluator.BeginIndexedBatch(graph));
        using (evaluator.BeginIndexedBatch(graph))
        {
            Assert.Throws<InvalidOperationException>(() => evaluator.BeginIndexedBatch(other));
            Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(other, "same"));
            Assert.AreEqual(10, evaluator.Evaluate(graph, "same"));
        }
        Assert.AreEqual(20, evaluator.Evaluate(other, "same"));
    }

    [Test]
    public void FailedIndexBuildAndHandlerExceptionDoNotPoisonFollowingScopes()
    {
        var graph = new NowNodeGraph();
        Add(graph, "source", 1, 10);
        var evaluator = Evaluator();
        var links = graph.links;
        graph.links = null;
        Assert.Throws<NullReferenceException>(() => evaluator.BeginIndexedBatch(graph));
        graph.links = links;
        Assert.AreEqual(10, evaluator.Evaluate(graph, "source"));

        evaluator.Kind(1, _ => throw new InvalidOperationException("handler"));
        Assert.Throws<InvalidOperationException>(() =>
        {
            using (evaluator.BeginIndexedBatch(graph))
                evaluator.Evaluate(graph, "source");
        });
        evaluator.Kind(1, ctx => ctx.node.userId);
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(10, evaluator.Evaluate(graph, "source"));
    }

    [Test]
    public void OrdinaryHandlerCannotSwitchItsActiveWalkIntoIndexedMode()
    {
        var graph = new NowNodeGraph();
        Add(graph, "source", 1, 10);
        var evaluator = Evaluator();
        evaluator.Kind(1, ctx =>
        {
            Assert.Throws<InvalidOperationException>(() => evaluator.BeginIndexedBatch(ctx.graph));
            return ctx.node.userId;
        });
        Assert.AreEqual(10, evaluator.Evaluate(graph, "source"));
        evaluator.Kind(1, ctx => ctx.node.userId);
        using (evaluator.BeginIndexedBatch(graph))
            Assert.AreEqual(10, evaluator.Evaluate(graph, "source"));
    }

    [Test]
    public void IndexedScopeConstructionAndEvaluationAllocateNothingAfterWarmup()
    {
        var graph = new NowNodeGraph();
        Add(graph, "source", 1, 10);
        Add(graph, "root", 2);
        Link(graph, "source", "root");
        var evaluator = Evaluator();
        for (int i = 0; i < 3; ++i)
            using (evaluator.BeginIndexedBatch(graph))
                evaluator.Evaluate(graph, "root");
        using var allocations = new NowBenchmarkAllocations(reportAvailability: false);
        allocations.RequireAvailable();
        allocations.Begin();
        int result;
        using (evaluator.BeginIndexedBatch(graph))
            result = evaluator.Evaluate(graph, "root");
        long allocated = allocations.End();
        Assert.AreEqual(11, result);
        allocations.AssertZero(allocated, "Rebuilding a warmed index and evaluating its graph must not allocate.");
    }
}

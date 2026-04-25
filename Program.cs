using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using Flee.CalcEngine.PublicTypes;
using Flee.PublicTypes;
using Yale.Engine;

namespace Yale.Performance;

// -----------------------------------------------------------------
// Scenario 1: Compile a fresh expression (cold path)
// Real world: a user defines a new formula or rule at runtime.
// Measures: how much overhead does initial parsing/compilation add?
// -----------------------------------------------------------------
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class CompilationBenchmarks
{
    private ExpressionContext _fleeCtx = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fleeCtx = new ExpressionContext();
        _fleeCtx.Variables.Add("a", 0);
        _fleeCtx.Variables.Add("b", 0);
    }

    [Benchmark(Baseline = true, Description = "Flee  – compile: a + b")]
    public object Flee_Simple() => _fleeCtx.CompileGeneric<int>("a + b");

    [Benchmark(Description = "Yale  – compile: a + b")]
    public object Yale_Simple()
    {
        var inst = new ComputeInstance(new ComputeInstanceOptions { AutoRecalculate = false });
        inst.Variables.Add("a", 0);
        inst.Variables.Add("b", 0);
        inst.AddExpression<int>("result", "a + b");
        return inst;
    }

    [Benchmark(Description = "Flee  – compile: complex arithmetic")]
    public object Flee_Complex()
        => _fleeCtx.CompileGeneric<int>("(a * 3 + b * 7) / (a + 1) - (b - a) * 2 + a * b");

    [Benchmark(Description = "Yale  – compile: complex arithmetic")]
    public object Yale_Complex()
    {
        var inst = new ComputeInstance(new ComputeInstanceOptions { AutoRecalculate = false });
        inst.Variables.Add("a", 0);
        inst.Variables.Add("b", 0);
        inst.AddExpression<int>("result", "(a * 3 + b * 7) / (a + 1) - (b - a) * 2 + a * b");
        return inst;
    }
}

// -----------------------------------------------------------------
// Scenario 2: Compile once, evaluate many times with changing inputs
// Real world: apply a pricing rule or scoring formula to many records.
// Measures: per-evaluation cost after the expression is compiled.
// -----------------------------------------------------------------
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class EvaluationBenchmarks
{
    private ExpressionContext _fleeCtx = null!;
    private IGenericExpression<int> _fleeSimple = null!;
    private IGenericExpression<int> _fleeComplex = null!;

    private ComputeInstance _yaleSimple = null!;
    private ComputeInstance _yaleComplex = null!;

    private int _a = 1;
    private int _b = 1;

    [GlobalSetup]
    public void Setup()
    {
        _fleeCtx = new ExpressionContext();
        _fleeCtx.Variables.Add("a", _a);
        _fleeCtx.Variables.Add("b", _b);
        _fleeSimple  = _fleeCtx.CompileGeneric<int>("a + b");
        _fleeComplex = _fleeCtx.CompileGeneric<int>(
            "(a * 3 + b * 7) / (a + 1) - (b - a) * 2 + a * b");

        _yaleSimple = new ComputeInstance(new ComputeInstanceOptions { AutoRecalculate = false });
        _yaleSimple.Variables.Add("a", _a);
        _yaleSimple.Variables.Add("b", _b);
        _yaleSimple.AddExpression<int>("result", "a + b");

        _yaleComplex = new ComputeInstance(new ComputeInstanceOptions { AutoRecalculate = false });
        _yaleComplex.Variables.Add("a", _a);
        _yaleComplex.Variables.Add("b", _b);
        _yaleComplex.AddExpression<int>("result",
            "(a * 3 + b * 7) / (a + 1) - (b - a) * 2 + a * b");
    }

    [Benchmark(Baseline = true, Description = "Flee  – eval: a + b")]
    public int Flee_Simple()
    {
        _a = (_a % 999) + 1;
        _fleeCtx.Variables["a"] = _a;
        return _fleeSimple.Evaluate();
    }

    [Benchmark(Description = "Yale  – eval: a + b")]
    public int Yale_Simple()
    {
        _a = (_a % 999) + 1;
        _yaleSimple.Variables["a"] = _a;
        return _yaleSimple.GetResult<int>("result");
    }

    [Benchmark(Description = "Flee  – eval: complex arithmetic")]
    public int Flee_Complex()
    {
        _a = (_a % 999) + 1;
        _b = (_b % 999) + 1;
        _fleeCtx.Variables["a"] = _a;
        _fleeCtx.Variables["b"] = _b;
        return _fleeComplex.Evaluate();
    }

    [Benchmark(Description = "Yale  – eval: complex arithmetic")]
    public int Yale_Complex()
    {
        _a = (_a % 999) + 1;
        _b = (_b % 999) + 1;
        _yaleComplex.Variables["a"] = _a;
        _yaleComplex.Variables["b"] = _b;
        return _yaleComplex.GetResult<int>("result");
    }
}

// -----------------------------------------------------------------
// Scenario 3: Small dependency graph with changing inputs
// Real world: a spreadsheet-style model where outputs depend on shared
// inputs (e.g. order pricing: subtotal → discount → tax → total).
// Measures: full graph traversal cost when one input variable changes.
// -----------------------------------------------------------------
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class DependencyGraphBenchmarks
{
    //  price, quantity
    //      └─► subtotal = price * quantity
    //               ├─► discount = subtotal / 10
    //               └─► tax      = (subtotal - discount) / 10
    //                        └─► total = subtotal - discount + tax

    private CalculationEngine _fleeEngine = null!;
    private ExpressionContext _fleeCtx = null!;

    private ComputeInstance _yaleInstance = null!;

    private int _price = 100;

    [GlobalSetup]
    public void Setup()
    {
        _fleeCtx = new ExpressionContext();
        _fleeCtx.Variables.Add("price", 100);
        _fleeCtx.Variables.Add("quantity", 5);
        _fleeEngine = new CalculationEngine();
        _fleeEngine.Add("subtotal", "price * quantity", _fleeCtx);
        _fleeEngine.Add("discount", "subtotal / 10", _fleeCtx);
        _fleeEngine.Add("tax", "(subtotal - discount) / 10", _fleeCtx);
        _fleeEngine.Add("total", "subtotal - discount + tax", _fleeCtx);

        _yaleInstance = new ComputeInstance(new ComputeInstanceOptions { AutoRecalculate = false });
        _yaleInstance.Variables.Add("price", 100);
        _yaleInstance.Variables.Add("quantity", 5);
        _yaleInstance.AddExpression<int>("subtotal", "price * quantity");
        _yaleInstance.AddExpression<int>("discount", "subtotal / 10");
        _yaleInstance.AddExpression<int>("tax", "(subtotal - discount) / 10");
        _yaleInstance.AddExpression<int>("total", "subtotal - discount + tax");
    }

    [Benchmark(Baseline = true, Description = "Flee  – dependency graph (4 nodes)")]
    public int Flee_PricingGraph()
    {
        _price = (_price % 200) + 50;
        _fleeCtx.Variables["price"] = _price;
        _fleeEngine.Recalculate("total");
        return _fleeEngine.GetResult<int>("total");
    }

    [Benchmark(Description = "Yale  – dependency graph (4 nodes)")]
    public int Yale_PricingGraph()
    {
        _price = (_price % 200) + 50;
        _yaleInstance.Variables["price"] = _price;
        return _yaleInstance.GetResult<int>("total");
    }
}

internal class Program
{
    private static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

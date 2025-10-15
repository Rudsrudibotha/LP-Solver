using System;
using System.Text;

namespace LP_Solver.Core
{
    public interface ISolver
    {
        LPResult Solve(LP_Solver.Core.Models.LpModel model, SolverOptions options, IReporter reporter);
    }

    public class SolverOptions
    {
        public bool ShowIterations { get; set; }
        public int MaxIterations { get; set; }
        public double Tolerance { get; set; }

        public SolverOptions()
        {
            ShowIterations = true;
            MaxIterations = 10000;
            Tolerance = 1e-9;
        }
    }

    public enum LPStatus
    {
        Optimal,
        Infeasible,
        Unbounded,
        IterationLimit,
        Error
    }

    public class LPResult
    {
        public LPStatus Status { get; set; }
        public double ObjectiveValue { get; set; }
        public double[] VariableValues { get; set; }
        public string Log { get; set; }

        public LPResult()
        {
            VariableValues = new double[0];
            Log = string.Empty;
        }

        public bool IsOptimal
        {
            get { return Status == LPStatus.Optimal; }
        }
    }

    public interface IReporter
    {
        void WriteLine(string message);
        void WriteStep(string title, string details);
        string GetLog();
    }

    public class StringBuilderReporter : IReporter
    {
        private StringBuilder _sb;

        public StringBuilderReporter()
        {
            _sb = new StringBuilder();
        }

        public void WriteLine(string message)
        {
            _sb.AppendLine(message);
        }

        public void WriteStep(string title, string details)
        {
            _sb.AppendLine("---- " + title + " ----");
            _sb.AppendLine(details);
            _sb.AppendLine();
        }

        public string GetLog()
        {
            return _sb.ToString();
        }
    }

    public class ConsoleReporter : IReporter
    {
        private StringBuilderReporter _buffer;

        public ConsoleReporter()
        {
            _buffer = new StringBuilderReporter();
        }

        public void WriteLine(string message)
        {
            Console.WriteLine(message);
            _buffer.WriteLine(message);
        }

        public void WriteStep(string title, string details)
        {
            Console.WriteLine("---- " + title + " ----");
            Console.WriteLine(details);
            Console.WriteLine();
            _buffer.WriteStep(title, details);
        }

        public string GetLog()
        {
            return _buffer.GetLog();
        }
    }
}
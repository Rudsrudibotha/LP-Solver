using System.Globalization;
using System.Text;

namespace LP_Solver.IO
{
    /// <summary>
    /// Simple reporter that buffers text in-memory for printing/exporting.
    /// Compatible with existing solver/menu code.
    /// </summary>
    public class StringBuilderReporter : LP_Solver.Core.IReporter
    {
        private readonly StringBuilder _sb = new StringBuilder();

        // --- Core logging methods commonly used in your code ---

        public void WriteLine(string message)
        {
            _sb.AppendLine(message ?? string.Empty);
        }

        /// <summary>
        /// Optional pretty "step" section used by simplex iteration logs.
        /// </summary>
        public void WriteStep(string title, string content)
        {
            if (!string.IsNullOrEmpty(title))
                _sb.AppendLine("=== " + title + " ===");
            if (!string.IsNullOrEmpty(content))
                _sb.AppendLine(content);
        }

        /// <summary>
        /// Full log text (expected by solvers and menus).
        /// </summary>
        public string GetLog() => _sb.ToString();

        // --- Extra methods some parts of the project may call (safe no-ops / helpers) ---

        public void StartSection(string title)
        {
            if (!string.IsNullOrEmpty(title))
                _sb.AppendLine("=== " + title + " ===");
        }

        public void EndSection()
        {
            _sb.AppendLine();
        }

        public void log(string message) // some code uses lowercase "log"
        {
            _sb.AppendLine(message ?? string.Empty);
        }

        public void UpdateBest(double bestObj, double[] bestX)
        {
            _sb.AppendLine("Best Objective: " + bestObj.ToString("F3", CultureInfo.InvariantCulture));
            if (bestX != null && bestX.Length > 0)
                _sb.AppendLine("x = [" + string.Join(", ", bestX) + "]");
        }

        /// <summary>
        /// Alias some components expect.
        /// </summary>
        public string GetText() => _sb.ToString();

        public void Clear() => _sb.Clear();
    }
}

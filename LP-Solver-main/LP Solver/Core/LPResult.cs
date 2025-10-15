using System;

namespace LP_Solver.Core
{
    /// <summary>
    /// Results type used ONLY for sensitivity features.
    /// Keeps the existing LPResult-based flow untouched.
    /// </summary>
    public class LPREsults
    {
        public LPStatus Status { get; set; }
        public double ObjectiveValue { get; set; }
        public double[] VariableValues { get; set; } = Array.Empty<double>();
        public string Log { get; set; } = string.Empty;

        // Sensitivity requires the final tableau snapshot
        public SimplexTableau FinalTableau { get; set; }

        public bool IsOptimal
        {
            get { return Status == LPStatus.Optimal; }
        }
    }
}

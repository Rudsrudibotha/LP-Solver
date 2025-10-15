using System;
using System.Collections.Generic;

namespace LP_Solver.Core.Models
{
    public class LpModel
    {
        // Problem type: "max" or "min"
        public string ProblemType { get; set; }

        // Objective function coefficients
        public List<double> ObjectiveCoeffs { get; set; }

        // Constraints
        public List<Constraint> Constraints { get; set; }

        // Variable types ("+", "-", "int", "bin", etc.)
        public List<string> VariableTypes { get; set; }

        // Constructor
        public LpModel()
        {
            ObjectiveCoeffs = new List<double>();
            Constraints = new List<Constraint>();
            VariableTypes = new List<string>();
        }

        // Number of variables
        public int NumVars
        {
            get { return ObjectiveCoeffs.Count; }
        }

        // Number of constraints
        public int NumCons
        {
            get { return Constraints.Count; }
        }

        // Convenience property to check if maximization
        public bool IsMaximization
        {
            get { return ProblemType != null && ProblemType.ToLower() == "max"; }
        }
    }

    public class Constraint
    {
        // Coefficients for this constraint
        public List<double> Coefficients { get; set; }

        // Relation: "<=", ">=", "="
        public string Relation { get; set; }

        // Right-hand side value
        public double RHS { get; set; }

        // Constructor
        public Constraint()
        {
            Coefficients = new List<double>();
        }
    }
}
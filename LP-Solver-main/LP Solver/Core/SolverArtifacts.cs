namespace LP_Solver.Core
{
	public static class SolverArtifacts
	{
		public static double[,] LastTableau { get; set; }
		public static int[] LastBasicVars { get; set; }
		public static bool IsMaximization { get; set; }
	}
}

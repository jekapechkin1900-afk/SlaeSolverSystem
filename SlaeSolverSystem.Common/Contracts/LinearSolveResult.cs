namespace SlaeSolverSystem.Common.Contracts;

public record LinearSolveResult(
	long ElapsedMilliseconds,
	double[] SolutionVector,
	int MatrixSize
);
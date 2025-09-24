namespace SlaeSolverSystem.Common.Contracts;

public record CalculationResult(long ElapsedTime, int Iterations, double[] SolutionVector, int MatrixSize);

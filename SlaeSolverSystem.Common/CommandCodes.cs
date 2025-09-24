namespace SlaeSolverSystem.Common;

public static class CommandCodes
{
	// Master -> Worker
	public const byte SetTask = 0x01;
	public const byte IterationVector = 0x02;
	public const byte Reset = 0x03;

	// Worker -> Master
	public const byte TaskAccepted = 0x11;
	public const byte PartialResult = 0x12;
	public const byte WorkerError = 0xFE;

	// GUI Client -> Master
	public const byte StartDistributedCalculation = 0x21; 
	public const byte StartLinearCalculation = 0x23;      
	public const byte StopCalculation = 0x22;

	// Master -> GUI Client
	public const byte StatusUpdate = 0x31;
	public const byte WorkerStatusUpdate = 0x32;
	public const byte ProgressUpdate = 0x33;
	public const byte ResultReady = 0x34;
	public const byte LogMessage = 0x35;
	public const byte LinearResultReady = 0x36;
	public const byte CalculationFailed = 0x3F;
}

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
	public const byte StartGaussLinear = 0x21; // Прямой метод Гаусса (однопоточный)
	public const byte StartSeidelLinear = 0x22; // Гаусс-Зейдель (однопоточный)
	public const byte StartSeidelMultiThreadNoPool = 0x23; // Гаусс-Зейдель (многопоточный, без пула)
	public const byte StartSeidelMultiThreadPool = 0x24; // Гаусс-Зейдель (многопоточный, с пулом)
	public const byte StartSeidelMultiThreadAsync = 0x25; // Гаусс-Зейдель (асинхронный)
	public const byte StartDistributedCalculation = 0x26; // Распределенный
	public const byte RequestPoolState = 0x2F;

	// Master -> GUI Client
	public const byte StatusUpdate = 0x31;
	public const byte WorkerStatusUpdate = 0x32;
	public const byte ProgressUpdate = 0x33;
	public const byte ResultReady = 0x34;
	public const byte LogMessage = 0x35;
	public const byte LinearResultReady = 0x36;
	public const byte PoolStateUpdate = 0x37;
	public const byte CalculationFailed = 0x3F;
}

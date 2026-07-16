using Test.Shared;
using Touchstone.Cli;

string? resultsPath = null;
for (int i = 0; i < args.Length - 1; i++)
{
	if (string.Equals(args[i], "--results", StringComparison.OrdinalIgnoreCase))
	{
		resultsPath = args[i + 1];
	}
}

return await ConsoleRunner.RunAsync(PadlockSuites.All, resultsPath: resultsPath);

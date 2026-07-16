namespace Test.Xunit
{
	using System.Threading;
	using System.Threading.Tasks;
	using Test.Shared;
	using Touchstone.Core;
	using Touchstone.XunitAdapter;
	using global::Xunit;

	public sealed class PadlockTouchstoneTests
	{
		public static TouchstoneTheoryData TestCases()
		{
			return new TouchstoneTheoryData(PadlockSuites.All);
		}

		[Theory]
		[MemberData(nameof(TestCases))]
		public async Task Run(TestCaseDescriptor testCase)
		{
			await testCase.ExecuteAsync(CancellationToken.None);
		}
	}
}

namespace Test.Nunit
{
	using System.Collections;
	using System.Threading;
	using System.Threading.Tasks;
	using NUnit.Framework;
	using Test.Shared;
	using Touchstone.Core;
	using Touchstone.NunitAdapter;

	[TestFixture]
	public sealed class PadlockTouchstoneTests
	{
		private static IEnumerable TestCases()
		{
			return new TouchstoneTestCaseSource(PadlockSuites.All);
		}

		[Test]
		[TestCaseSource(nameof(TestCases))]
		public async Task Run(TestCaseDescriptor testCase)
		{
			await testCase.ExecuteAsync(CancellationToken.None);
		}
	}
}

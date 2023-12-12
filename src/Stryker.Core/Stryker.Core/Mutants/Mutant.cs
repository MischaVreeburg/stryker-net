namespace Stryker.Core.Mutants
{
    /// <summary>
    /// This interface should only contain readonly properties to ensure that others than the mutation test process cannot modify mutants.
    /// </summary>
    public interface IReadOnlyMutant
    {
        int Id { get; }
        Mutation Mutation { get; }
        MutantStatus ResultStatus { get; }
        string ResultStatusReason { get; }
        ITestGuids CoveringTests { get; }
        ITestGuids KillingTests { get; }
        ITestGuids AssessingTests { get; }
        bool CountForStats { get; }
        bool IsStaticValue { get; }
    }

    /// <summary>
    /// Represents a single mutation on domain level
    /// </summary>
    public class Mutant : IReadOnlyMutant
    {
        MutantStatus IReadOnlyMutant.ResultStatus
        {
            get
            {
                return ResultStatus == MutantStatus.Ignored
                    ? ResultStatusBaseline == MutantStatus.Pending
                        ? ResultStatus
                        : ResultStatusBaseline
                    : ResultStatus;
            }
        }
        string IReadOnlyMutant.ResultStatusReason
        {
            get
            {
                return ResultStatus == MutantStatus.Ignored

                    ? ResultStatusBaseline == MutantStatus.Pending
                        ? ResultStatusReason
                        : ResultStatusReasonBaseline
                    : ResultStatusReason;
            }
        }

        public int Id { get; set; }

        public Mutation Mutation { get; set; }

        public MutantStatus ResultStatus { get; set; }

        public ITestGuids CoveringTests { get; set; } = TestGuidsList.NoTest();

        public ITestGuids KillingTests { get; set; } = TestGuidsList.NoTest();

        public ITestGuids AssessingTests { get; set; } = TestGuidsList.EveryTest();

        public string ResultStatusReason { get; set; }

        public MutantStatus ResultStatusBaseline { get; set; }
        public string ResultStatusReasonBaseline { get; set; }

        public bool CountForStats => ResultStatus != MutantStatus.CompileError && (ResultStatus != MutantStatus.Ignored || ResultStatusBaseline != MutantStatus.Ignored);

        public bool IsStaticValue { get; set; }

        public bool MustBeTestedInIsolation { get; set; }

        public string DisplayName => $"{Id}: {Mutation?.DisplayName}";

        public void AnalyzeTestRun(ITestGuids failedTests, ITestGuids resultRanTests, ITestGuids timedOutTests, bool sessionTimedOut)
        {
            if (AssessingTests.ContainsAny(failedTests))
            {
                ResultStatus = MutantStatus.Killed;
                KillingTests = AssessingTests.Intersect(failedTests);
            }
            else if (AssessingTests.ContainsAny(timedOutTests) || sessionTimedOut)
            {
                ResultStatus = MutantStatus.Timeout;
            }
            else if (resultRanTests.IsEveryTest || (resultRanTests.IsEveryTest is not true && AssessingTests.IsIncludedIn(resultRanTests)))
            {
                ResultStatus = MutantStatus.Survived;
            }
        }
    }
}

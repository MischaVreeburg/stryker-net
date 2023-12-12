using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Stryker.Core.Baseline.Providers;
using Stryker.Core.Baseline.Utils;
using Stryker.Core.Logging;
using Stryker.Core.Mutants;
using Stryker.Core.MutationTest;
using Stryker.Core.Options;
using Stryker.Core.ProjectComponents;
using Stryker.Core.Reporters.Json;
using Stryker.Core.Reporters.Json.SourceFiles;

namespace Stryker.Core.MutantFilters
{
    public class BaselineMutantFilter : IMutantFilter
    {
        private readonly IBaselineProvider _baselineProvider;
        private readonly IGitInfoProvider _gitInfoProvider;
        private readonly ILogger<BaselineMutantFilter> _logger;
        private readonly IBaselineMutantHelper _baselineMutantHelper;
        private readonly MutationTestInput _input;

        private readonly StrykerOptions _options;
        private static JsonReport _baseline;

        public MutantFilter Type => MutantFilter.Baseline;
        public string DisplayName => "baseline filter";

        public BaselineMutantFilter(StrykerOptions options, MutationTestInput mutationTestInput, IBaselineProvider baselineProvider = null, IGitInfoProvider gitInfoProvider = null, IBaselineMutantHelper baselineMutantHelper = null)
        {
            _logger = ApplicationLogging.LoggerFactory.CreateLogger<BaselineMutantFilter>();
            _baselineProvider = baselineProvider ?? BaselineProviderFactory.Create(options);
            _gitInfoProvider = gitInfoProvider ?? new GitInfoProvider(options);
            _baselineMutantHelper = baselineMutantHelper ?? new BaselineMutantHelper();

            _options = options;
            _input = mutationTestInput;

            if (options.WithBaseline && _baseline is null)
            {
                _baseline = GetBaselineAsync().Result;
            }
        }

        public IEnumerable<Mutant> FilterMutants(IEnumerable<Mutant> mutants, IReadOnlyFileLeaf file, StrykerOptions options)
        {
            if (options.WithBaseline)
            {
                if (_baseline == null)
                {
                    _logger.LogDebug("Returning all mutants on {RelativePath} because there is no baseline available", file.RelativePath);
                }
                else
                {
                    UpdateMutantsWithBaselineStatus(mutants, file);
                }
            }

            return mutants;
        }

        private void UpdateMutantsWithBaselineStatus(IEnumerable<Mutant> mutants, IReadOnlyFileLeaf file)
        {
            SourceFile baselineFile;
            if (_options.IsSolutionContext)
            //Bug Test key with Projectname - relative path. => else will fail in solution mode!!
            {
                var solutionRootInBaselineReport = _baseline.ProjectRoot;
                var solutionRoot = new FileInfo(_options.SolutionPath).DirectoryName;
                var correctedFileName = file.FullPath.Replace(solutionRoot, solutionRootInBaselineReport);

                if (!_baseline.Files.TryGetValue(FilePathUtils.NormalizePathSeparators(correctedFileName), out baselineFile))
                {
                    return;
                }
            }
            else
            {
                if (!_baseline.Files.ContainsKey(FilePathUtils.NormalizePathSeparators(file.RelativePath)))
                {
                    return;
                }

                baselineFile = _baseline.Files[FilePathUtils.NormalizePathSeparators(file.RelativePath)];
            }

            if (baselineFile is { })
            {
                foreach (var baselineMutant in baselineFile.Mutants)
                {
                    var baselineMutantSourceCode = _baselineMutantHelper.GetMutantSourceCode(baselineFile.Source, baselineMutant);

                    if (string.IsNullOrEmpty(baselineMutantSourceCode))
                    {
                        _logger.LogWarning("Unable to find mutant span in original baseline source code. This indicates a bug in stryker. Please report this on github.");
                        continue;
                    }

                    IEnumerable<Mutant> matchingMutants = _baselineMutantHelper.GetMutantMatchingSourceCode(mutants, baselineMutant, baselineMutantSourceCode);

                    SetMutantStatusToBaselineMutantStatus(baselineMutant, matchingMutants);
                }
            }
        }

        private void SetMutantStatusToBaselineMutantStatus(JsonMutant baselineMutant, IEnumerable<Mutant> matchingMutants)
        {
            if (matchingMutants.Count() == 1)
            {
                var matchingMutant = matchingMutants.First();
                matchingMutant.ResultStatusBaseline = (MutantStatus)Enum.Parse(typeof(MutantStatus), baselineMutant.Status);
                matchingMutant.ResultStatusReasonBaseline = "Result based on previous run";
            }
            else
            {
                foreach (var matchingMutant in matchingMutants)
                {
                    matchingMutant.ResultStatus = MutantStatus.Pending;
                    matchingMutant.ResultStatusReason = "Result based on previous run was inconclusive";
                }
            }
        }

        private async Task<JsonReport> GetBaselineAsync()
        {
            var branchName = _gitInfoProvider.GetCurrentBranchName();

            var baselineLocation = $"baseline/{branchName}";

            var report = await _baselineProvider.Load(baselineLocation);

            if (report == null)
            {
                _logger.LogInformation("We could not locate a baseline for branch {branchName}, now trying fallback version {FallbackVersion}", branchName, _options.FallbackVersion);

                return await GetFallbackBaselineAsync();
            }

            _logger.LogInformation("Found baseline report for current branch {branchName}", branchName);

            return report;
        }

        private async Task<JsonReport> GetFallbackBaselineAsync(bool baseline = true)
        {
            var report = await _baselineProvider.Load($"{(baseline ? "baseline/" : "")}{_options.FallbackVersion}");

            if (report == null)
            {
                if (baseline)
                {
                    _logger.LogDebug("We could not locate a baseline report for the fallback version. Now trying regular fallback version.");
                    return await GetFallbackBaselineAsync(false);
                }
                _logger.LogInformation("We could not locate a baseline report for the current branch, version or fallback version. Now running a complete test to establish a fresh baseline.");
                return null;
            }

            _logger.LogInformation("Found fallback report using version {FallbackVersion}", _options.FallbackVersion);

            return report;
        }
    }
}

namespace MathCollegeSiteBack.Contracts;

public sealed record ExperimentRequest(int Trials);
public sealed record DieFaceResult(int Face, int Count, decimal RelativeFrequency, decimal TheoreticalProbability);
public sealed record DieExperimentResult(int Trials, IReadOnlyList<DieFaceResult> Faces);
public sealed record CoinExperimentResult(
    int Trials,
    int Heads,
    int Tails,
    decimal HeadsFrequency,
    decimal TailsFrequency,
    decimal TheoreticalProbability);
public sealed record ProbabilityRequest(long FavorableOutcomes, long TotalOutcomes);
public sealed record CombinatoricsRequest(int N, int K);
public sealed record CombinatoricsResult(string Combination, string Arrangement);
public sealed record StatisticsRequest(decimal[]? Values);
public sealed record StatisticsResult(
    int Count,
    double Mean,
    double Median,
    IReadOnlyList<double> Modes,
    double PopulationVariance,
    double PopulationStandardDeviation,
    double? SampleVariance,
    double? SampleStandardDeviation);

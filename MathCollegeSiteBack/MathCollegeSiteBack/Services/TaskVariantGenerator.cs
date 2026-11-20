using System.Globalization;
using MathCollegeSiteBack.Models;

namespace MathCollegeSiteBack.Services;

public sealed record TaskVariant(
    string Type,
    string Prompt,
    string ExplanationTitle,
    string Explanation,
    decimal? CorrectNumber);

/// <summary>
/// Produces a repeatable variant for one task in one lesson run.
/// The same run/task pair always gets the same values, including after a reload.
/// A new run gets a different run id and therefore new values.
/// </summary>
public static class TaskVariantGenerator
{
    private static readonly HashSet<string> GeneratedTypes =
    [
        "generated:addition",
        "generated:subtraction",
        "generated:multiplication",
        "generated:probability",
        "generated:combinations",
        "generated:relative-frequency",
        "generated:arrangements",
        "generated:mean",
        "generated:median",
        "generated:range",
        "generated:conditional-probability",
        "generated:union-probability",
        "generated:binomial-expectation",
        "generated:bernoulli-variance",
        "generated:normal-zscore",
        "generated:bayes",
        "generated:binomial-probability",
        "generated:uniform-probability",
        "generated:confidence-margin",
        "generated:hypothesis-z",
        "generated:linear-slope",
        "generated:chi-square"
    ];

    public static bool IsGeneratedType(string type) => GeneratedTypes.Contains(type);

    public static TaskVariant Create(int runId, LessonTask task)
    {
        if (task.Type == "choice")
            return new TaskVariant("choice", task.Prompt, task.ExplanationTitle, task.Explanation, null);

        if (task.Type == "number")
            return new TaskVariant("number", task.Prompt, task.ExplanationTitle, task.Explanation, task.CorrectNumber);

        if (!IsGeneratedType(task.Type))
            throw new InvalidOperationException($"Неизвестный тип задания: {task.Type}");

        var random = CreateRandom(runId, task.Id);

        return task.Type switch
        {
            "generated:addition" => Addition(random, task),
            "generated:subtraction" => Subtraction(random, task),
            "generated:multiplication" => Multiplication(random, task),
            "generated:probability" => Probability(random, task),
            "generated:combinations" => Combinations(random, task),
            "generated:relative-frequency" => RelativeFrequency(random, task),
            "generated:arrangements" => Arrangements(random, task),
            "generated:mean" => Mean(random, task),
            "generated:median" => Median(random, task),
            "generated:range" => Range(random, task),
            "generated:conditional-probability" => ConditionalProbability(random, task),
            "generated:union-probability" => UnionProbability(random, task),
            "generated:binomial-expectation" => BinomialExpectation(random, task),
            "generated:bernoulli-variance" => BernoulliVariance(random, task),
            "generated:normal-zscore" => NormalZScore(random, task),
            "generated:bayes" => Bayes(random, task),
            "generated:binomial-probability" => BinomialProbability(random, task),
            "generated:uniform-probability" => UniformProbability(random, task),
            "generated:confidence-margin" => ConfidenceMargin(random, task),
            "generated:hypothesis-z" => HypothesisZ(random, task),
            "generated:linear-slope" => LinearSlope(random, task),
            "generated:chi-square" => ChiSquare(random, task),
            _ => throw new InvalidOperationException($"Неизвестный генератор задания: {task.Type}")
        };
    }

    private static Random CreateRandom(int runId, int taskId)
    {
        var seed = unchecked((uint)runId * 2_654_435_761u
            ^ (uint)taskId * 2_246_822_519u
            ^ 0x9E3779B9u);
        if (seed == 0)
            seed = 1;

        return new Random(unchecked((int)seed));
    }

    private static TaskVariant Addition(Random random, LessonTask task)
    {
        var a = random.Next(12, 90);
        var b = random.Next(11, 90);
        var answer = a + b;

        return Build(task, answer,
            ("{a}", a), ("{b}", b), ("{answer}", answer));
    }

    private static TaskVariant Subtraction(Random random, LessonTask task)
    {
        var answer = random.Next(11, 90);
        var b = random.Next(1, 50);
        var a = answer + b;

        return Build(task, answer,
            ("{a}", a), ("{b}", b), ("{answer}", answer));
    }

    private static TaskVariant Multiplication(Random random, LessonTask task)
    {
        var a = random.Next(2, 13);
        var b = random.Next(2, 13);
        var answer = a * b;

        return Build(task, answer,
            ("{a}", a), ("{b}", b), ("{answer}", answer));
    }

    private static TaskVariant Probability(Random random, LessonTask task)
    {
        var total = random.Next(5, 101);
        var favorable = random.Next(1, total);
        var answer = decimal.Round((decimal)favorable / total, 4);

        return Build(task, answer,
            ("{favorable}", favorable), ("{total}", total), ("{answer}", answer));
    }

    private static TaskVariant Combinations(Random random, LessonTask task)
    {
        var n = random.Next(10, 51);
        var k = random.Next(2, Math.Min(9, n - 1));
        var answer = Combination(n, k);

        return Build(task, answer,
            ("{n}", n), ("{k}", k), ("{answer}", answer));
    }

    private static TaskVariant RelativeFrequency(Random random, LessonTask task)
    {
        var total = random.Next(2, 11) * 100;
        var observed = random.Next(1, total);
        var answer = decimal.Round((decimal)observed / total, 4);

        return Build(task, answer,
            ("{observed}", observed), ("{total}", total), ("{answer}", answer));
    }

    private static TaskVariant Arrangements(Random random, LessonTask task)
    {
        var n = random.Next(6, 21);
        var k = random.Next(2, 7);
        var answer = 1m;
        for (var i = 0; i < k; i++)
            answer *= n - i;

        return Build(task, answer,
            ("{n}", n), ("{k}", k), ("{answer}", answer));
    }

    private static TaskVariant Mean(Random random, LessonTask task)
    {
        var values = Enumerable.Range(0, 4).Select(_ => random.Next(10, 100)).ToArray();
        var answer = values.Average();
        var valueList = string.Join(", ", values);

        return Build(task, (decimal)answer,
            ("{values}", valueList), ("{sum}", values.Sum()), ("{answer}", (decimal)answer));
    }

    private static TaskVariant Median(Random random, LessonTask task)
    {
        var values = Enumerable.Range(0, 5).Select(_ => random.Next(1, 101)).Order().ToArray();
        var answer = values[2];
        var valueList = string.Join(", ", values);

        return Build(task, answer,
            ("{values}", valueList), ("{answer}", answer));
    }

    private static TaskVariant Range(Random random, LessonTask task)
    {
        var low = random.Next(1, 101);
        var high = low + random.Next(1, 101);
        var answer = high - low;

        return Build(task, answer,
            ("{low}", low), ("{high}", high), ("{answer}", answer));
    }

    private static TaskVariant ConditionalProbability(Random random, LessonTask task)
    {
        var groupTotal = random.Next(10, 101);
        var favorable = random.Next(1, groupTotal);
        var answer = decimal.Round((decimal)favorable / groupTotal, 4);

        return Build(task, answer,
            ("{favorable}", favorable), ("{groupTotal}", groupTotal), ("{answer}", answer));
    }

    private static TaskVariant UnionProbability(Random random, LessonTask task)
    {
        var total = random.Next(20, 101);
        var a = random.Next(2, total / 2);
        var b = random.Next(2, total / 2);
        var intersection = random.Next(0, Math.Min(a, b) + 1);
        var union = a + b - intersection;
        if (union > total)
            intersection += union - total;
        union = a + b - intersection;
        var answer = decimal.Round((decimal)union / total, 4);

        return Build(task, answer,
            ("{a}", a), ("{b}", b), ("{intersection}", intersection),
            ("{total}", total), ("{union}", union), ("{answer}", answer));
    }

    private static TaskVariant BinomialExpectation(Random random, LessonTask task)
    {
        var trials = random.Next(5, 101);
        var probabilityTenths = random.Next(1, 10);
        var probability = probabilityTenths / 10m;
        var answer = trials * probability;

        return Build(task, answer,
            ("{n}", trials), ("{p}", probability), ("{answer}", answer));
    }

    private static TaskVariant BernoulliVariance(Random random, LessonTask task)
    {
        var probabilityTenths = random.Next(1, 10);
        var probability = probabilityTenths / 10m;
        var answer = probability * (1m - probability);

        return Build(task, answer,
            ("{p}", probability), ("{answer}", answer));
    }

    private static TaskVariant NormalZScore(Random random, LessonTask task)
    {
        var mean = random.Next(50, 201);
        var sigma = random.Next(2, 21);
        var zScore = random.Next(-3, 4);
        var value = mean + zScore * sigma;

        return Build(task, zScore,
            ("{x}", value), ("{mean}", mean), ("{sigma}", sigma), ("{answer}", zScore));
    }

    private static TaskVariant Bayes(Random random, LessonTask task)
    {
        var prevalencePercent = random.Next(1, 21);
        var sensitivityPercent = random.Next(60, 100);
        var falsePositivePercent = random.Next(1, 21);
        var prevalence = prevalencePercent / 100m;
        var sensitivity = sensitivityPercent / 100m;
        var falsePositive = falsePositivePercent / 100m;
        var positiveProbability = prevalence * sensitivity + (1m - prevalence) * falsePositive;
        var answer = decimal.Round(prevalence * sensitivity / positiveProbability, 4);

        return Build(task, answer,
            ("{prevalence}", prevalencePercent),
            ("{sensitivity}", sensitivityPercent),
            ("{falsePositive}", falsePositivePercent),
            ("{answer}", answer));
    }

    private static TaskVariant BinomialProbability(Random random, LessonTask task)
    {
        var trials = random.Next(3, 13);
        var successes = random.Next(0, trials + 1);
        var probability = random.Next(1, 10) / 10m;
        var coefficient = Combination(trials, successes);
        var probabilityValue = (double)probability;
        var answer = decimal.Round((decimal)((double)coefficient
            * Math.Pow(probabilityValue, successes)
            * Math.Pow(1d - probabilityValue, trials - successes)), 4);

        return Build(task, answer,
            ("{n}", trials), ("{k}", successes),
            ("{p}", probability), ("{answer}", answer));
    }

    private static TaskVariant UniformProbability(Random random, LessonTask task)
    {
        var length = random.Next(20, 101);
        var lower = random.Next(0, length - 1);
        var upper = random.Next(lower + 1, length + 1);
        var answer = decimal.Round((decimal)(upper - lower) / length, 4);

        return Build(task, answer,
            ("{length}", length), ("{lower}", lower),
            ("{upper}", upper), ("{answer}", answer));
    }

    private static TaskVariant ConfidenceMargin(Random random, LessonTask task)
    {
        var rootN = random.Next(5, 16);
        var sampleSize = rootN * rootN;
        var standardDeviation = random.Next(5, 51);
        var answer = decimal.Round(2m * standardDeviation / rootN, 4);

        return Build(task, answer,
            ("{sigma}", standardDeviation), ("{n}", sampleSize),
            ("{rootN}", rootN), ("{answer}", answer));
    }

    private static TaskVariant HypothesisZ(Random random, LessonTask task)
    {
        var nullMean = random.Next(40, 81);
        var standardError = random.Next(1, 11);
        var zScore = random.Next(-4, 5);
        var sampleMean = nullMean + standardError * zScore;

        return Build(task, zScore,
            ("{sampleMean}", sampleMean), ("{nullMean}", nullMean),
            ("{standardError}", standardError), ("{answer}", zScore));
    }

    private static TaskVariant LinearSlope(Random random, LessonTask task)
    {
        var x1 = random.Next(0, 21);
        var x2 = x1 + random.Next(1, 11);
        var slope = random.Next(1, 11);
        var intercept = random.Next(0, 31);
        var y1 = intercept + slope * x1;
        var y2 = intercept + slope * x2;

        return Build(task, slope,
            ("{x1}", x1), ("{x2}", x2), ("{y1}", y1),
            ("{y2}", y2), ("{answer}", slope));
    }

    private static TaskVariant ChiSquare(Random random, LessonTask task)
    {
        var expectedPerCategory = random.Next(20, 201);
        var difference = random.Next(1, Math.Min(21, expectedPerCategory));
        var observedFirst = expectedPerCategory + difference;
        var observedSecond = expectedPerCategory - difference;
        var answer = decimal.Round(
            (decimal)difference * difference / expectedPerCategory * 2m,
            4);

        return Build(task, answer,
            ("{expected}", expectedPerCategory), ("{observedFirst}", observedFirst),
            ("{observedSecond}", observedSecond), ("{answer}", answer));
    }

    private static TaskVariant Build(
        LessonTask task,
        decimal answer,
        params (string Token, object Value)[] replacements)
    {
        var prompt = task.Prompt;
        var explanation = task.Explanation;

        foreach (var (token, value) in replacements)
        {
            var formatted = value is decimal decimalValue
                ? decimalValue.ToString("0.####", CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

            prompt = prompt.Replace(token, formatted, StringComparison.Ordinal);
            explanation = explanation.Replace(token, formatted, StringComparison.Ordinal);
        }

        return new TaskVariant("number", prompt, task.ExplanationTitle, explanation, answer);
    }

    private static decimal Combination(int n, int k)
    {
        k = Math.Min(k, n - k);
        var result = 1m;
        for (var i = 1; i <= k; i++)
            result = result * (n - k + i) / i;
        return result;
    }
}

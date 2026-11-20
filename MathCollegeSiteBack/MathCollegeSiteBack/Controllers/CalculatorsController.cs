using System.Numerics;
using MathCollegeSiteBack.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace MathCollegeSiteBack.Controllers;

[ApiController]
[Route("api/calculators")]
public sealed class CalculatorsController : ControllerBase
{
    [HttpPost("probability")]
    public IActionResult CalculateProbability(ProbabilityRequest request)
    {
        if (request.TotalOutcomes <= 0 || request.FavorableOutcomes < 0
            || request.FavorableOutcomes > request.TotalOutcomes)
            return BadRequest(new { message = "Проверьте числа: общее количество должно быть положительным, благоприятные исходы — от 0 до общего количества." });

        var probability = decimal.Round((decimal)request.FavorableOutcomes / request.TotalOutcomes, 8);
        return Ok(new
        {
            request.FavorableOutcomes,
            request.TotalOutcomes,
            probability,
            percent = decimal.Round(probability * 100m, 4)
        });
    }

    [HttpPost("combinatorics")]
    public ActionResult<CombinatoricsResult> CalculateCombinatorics(CombinatoricsRequest request)
    {
        if (request.N is < 0 or > 10_000 || request.K < 0 || request.K > request.N)
            return BadRequest(new { message = "Требуется 0 ≤ k ≤ n ≤ 10 000." });

        var combination = BigInteger.One;
        var k = Math.Min(request.K, request.N - request.K);
        for (var i = 1; i <= k; i++)
            combination = combination * (request.N - k + i) / i;

        var arrangement = BigInteger.One;
        for (var i = 0; i < request.K; i++)
            arrangement *= request.N - i;

        return Ok(new CombinatoricsResult(combination.ToString(), arrangement.ToString()));
    }

    [HttpPost("statistics")]
    public ActionResult<StatisticsResult> CalculateStatistics(StatisticsRequest request)
    {
        var values = request.Values;
        if (values is null || values.Length is < 1 or > 10_000)
            return BadRequest(new { message = "Введите от 1 до 10 000 чисел." });
        if (values.Any(value => value < -1_000_000_000_000m || value > 1_000_000_000_000m))
            return BadRequest(new { message = "Модуль каждого числа должен быть не больше 1 000 000 000 000." });

        var sorted = values.Select(value => (double)value).Order().ToArray();
        var mean = sorted.Average();
        var middle = sorted.Length / 2;
        var median = sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2d
            : sorted[middle];
        var populationVariance = sorted.Sum(value => Math.Pow(value - mean, 2)) / sorted.Length;
        var frequencies = values.GroupBy(value => value)
            .Select(group => new { Value = (double)group.Key, Count = group.Count() })
            .ToList();
        var maxFrequency = frequencies.Max(item => item.Count);
        var modes = maxFrequency == 1
            ? Array.Empty<double>()
            : frequencies.Where(item => item.Count == maxFrequency)
                .Select(item => item.Value).Order().ToArray();

        double? sampleVariance = sorted.Length > 1
            ? sorted.Sum(value => Math.Pow(value - mean, 2)) / (sorted.Length - 1)
            : null;

        return Ok(new StatisticsResult(
            sorted.Length,
            Round(mean),
            Round(median),
            modes.Select(Round).ToArray(),
            Round(populationVariance),
            Round(Math.Sqrt(populationVariance)),
            sampleVariance is null ? null : Round(sampleVariance.Value),
            sampleVariance is null ? null : Round(Math.Sqrt(sampleVariance.Value))));
    }

    private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}

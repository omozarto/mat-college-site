using MathCollegeSiteBack.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace MathCollegeSiteBack.Controllers;

[ApiController]
[Route("api/experiments")]
public sealed class ExperimentsController : ControllerBase
{
    [HttpPost("die")]
    public ActionResult<DieExperimentResult> RollDie(ExperimentRequest request)
    {
        if (request.Trials is < 1 or > 100_000)
            return BadRequest(new { message = "Количество бросков должно быть от 1 до 100 000." });

        var counts = new int[6];
        for (var i = 0; i < request.Trials; i++)
            counts[Random.Shared.Next(6)]++;

        var theoretical = decimal.Round(1m / 6m, 6);
        var faces = Enumerable.Range(1, 6)
            .Select(face => new DieFaceResult(
                face,
                counts[face - 1],
                decimal.Round((decimal)counts[face - 1] / request.Trials, 6),
                theoretical))
            .ToList();

        return Ok(new DieExperimentResult(request.Trials, faces));
    }

    [HttpPost("coin")]
    public ActionResult<CoinExperimentResult> FlipCoin(ExperimentRequest request)
    {
        if (request.Trials is < 1 or > 100_000)
            return BadRequest(new { message = "Количество бросков должно быть от 1 до 100 000." });

        var heads = 0;
        for (var i = 0; i < request.Trials; i++)
            if (Random.Shared.Next(2) == 0) heads++;

        var tails = request.Trials - heads;
        return Ok(new CoinExperimentResult(
            request.Trials,
            heads,
            tails,
            decimal.Round((decimal)heads / request.Trials, 6),
            decimal.Round((decimal)tails / request.Trials, 6),
            0.5m));
    }
}

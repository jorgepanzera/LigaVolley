using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Seasons;

namespace LigaVolley.Application.Seasons;

public sealed class SeasonService(ISeasonRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<SeasonSummaryDto>> ListAsync(bool? active, short? year, CancellationToken cancellationToken)
        => (await repository.ListAsync(active, year, cancellationToken)).Select(ToSummary).ToArray();

    public async Task<SeasonDto> GetAsync(int id, CancellationToken cancellationToken)
        => ToDto(await GetRequiredAsync(id, false, cancellationToken));

    public async Task<SeasonDto> CreateAsync(CreateSeasonRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueYearAsync(request.Year, null, cancellationToken);
        var season = new Season(request.Year, request.Name, request.StartDate, request.EndDate);
        repository.Add(season);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(season);
    }

    public async Task<SeasonDto> UpdateAsync(int id, UpdateSeasonRequest request, CancellationToken cancellationToken)
    {
        var season = await GetRequiredAsync(id, true, cancellationToken);
        await EnsureUniqueYearAsync(request.Year, id, cancellationToken);
        season.Update(request.Year, request.Name, request.StartDate, request.EndDate);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(season);
    }

    public async Task<SeasonDto> SetActiveAsync(int id, bool active, CancellationToken cancellationToken)
    {
        var season = await GetRequiredAsync(id, true, cancellationToken);
        season.SetActive(active);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(season);
    }

    private async Task<Season> GetRequiredAsync(int id, bool tracking, CancellationToken cancellationToken)
        => await repository.GetAsync(id, tracking, cancellationToken)
           ?? throw new ResourceNotFoundException("Season", id);

    private async Task EnsureUniqueYearAsync(short year, int? excludingId, CancellationToken cancellationToken)
    {
        if (await repository.YearExistsAsync(year, excludingId, cancellationToken))
        {
            throw new ResourceConflictException("season_year_conflict", $"A season for year {year} already exists.");
        }
    }

    private static SeasonDto ToDto(Season season)
        => new(season.SeasonId, season.Year, season.Name, season.StartDate, season.EndDate, season.Active);

    private static SeasonSummaryDto ToSummary(Season season)
        => new(season.SeasonId, season.Year, season.Name, season.Active);
}

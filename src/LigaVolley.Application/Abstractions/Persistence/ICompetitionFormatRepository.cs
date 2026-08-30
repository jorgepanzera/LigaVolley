using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface ICompetitionFormatRepository
{
    Task<IReadOnlyList<CompetitionFormat>> ListAsync(bool? active, short? teamCount, CancellationToken cancellationToken);
    Task<CompetitionFormat?> GetAsync(int id, bool tracking, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken);
    Task<CompetitionFormatUsage> GetUsageAsync(int id, CancellationToken cancellationToken);
    void Add(CompetitionFormat format);
    void PrepareReplacement(CompetitionFormat format);
}

public sealed record CompetitionFormatUsage(int DraftCompetitionCount, int OperationalCompetitionCount)
{
    public bool Used => DraftCompetitionCount + OperationalCompetitionCount > 0;
    public bool IsStructurallyLocked => OperationalCompetitionCount > 0;
}

using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Application.Divisions;

public sealed class DivisionService(IDivisionRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<DivisionSummaryDto>> ListAsync(Gender? gender, bool? active, CancellationToken cancellationToken)
        => (await repository.ListAsync(gender, active, cancellationToken)).Select(ToSummary).ToArray();

    public async Task<DivisionDto> GetAsync(int id, CancellationToken cancellationToken)
        => ToDto(await GetRequiredAsync(id, false, cancellationToken));

    public async Task<DivisionDto> CreateAsync(CreateDivisionRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(request.Name, request.LevelOrder, request.Gender, null, cancellationToken);
        var division = new Division(request.Name, request.LevelOrder, request.Gender);
        repository.Add(division);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(division);
    }

    public async Task<DivisionDto> UpdateAsync(int id, UpdateDivisionRequest request, CancellationToken cancellationToken)
    {
        var division = await GetRequiredAsync(id, true, cancellationToken);
        await EnsureUniqueAsync(request.Name, request.LevelOrder, request.Gender, id, cancellationToken);
        division.Update(request.Name, request.LevelOrder, request.Gender);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(division);
    }

    public async Task<DivisionDto> SetActiveAsync(int id, bool active, CancellationToken cancellationToken)
    {
        var division = await GetRequiredAsync(id, true, cancellationToken);
        division.SetActive(active);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(division);
    }

    private async Task<Division> GetRequiredAsync(int id, bool tracking, CancellationToken cancellationToken)
        => await repository.GetAsync(id, tracking, cancellationToken)
           ?? throw new ResourceNotFoundException("Division", id);

    private async Task EnsureUniqueAsync(string name, short levelOrder, Gender gender, int? excludingId, CancellationToken cancellationToken)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (await repository.NameExistsAsync(normalizedName, gender, excludingId, cancellationToken))
        {
            throw new ResourceConflictException("division_name_gender_conflict", "A division with the same name and gender already exists.");
        }

        if (await repository.LevelExistsAsync(levelOrder, gender, excludingId, cancellationToken))
        {
            throw new ResourceConflictException("division_level_gender_conflict", "A division with the same level and gender already exists.");
        }
    }

    private static DivisionDto ToDto(Division division)
        => new(division.DivisionId, division.Name, division.LevelOrder, division.Gender, division.Active);

    private static DivisionSummaryDto ToSummary(Division division)
        => new(division.DivisionId, division.Name, division.LevelOrder, division.Gender, division.Active);
}

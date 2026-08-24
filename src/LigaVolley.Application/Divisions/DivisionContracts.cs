using LigaVolley.Domain.Divisions;

namespace LigaVolley.Application.Divisions;

public sealed record CreateDivisionRequest(string Name, short LevelOrder, Gender Gender);
public sealed record UpdateDivisionRequest(string Name, short LevelOrder, Gender Gender);
public sealed record DivisionDto(int DivisionId, string Name, short LevelOrder, Gender Gender, bool Active);
public sealed record DivisionSummaryDto(int DivisionId, string Name, short LevelOrder, Gender Gender, bool Active);

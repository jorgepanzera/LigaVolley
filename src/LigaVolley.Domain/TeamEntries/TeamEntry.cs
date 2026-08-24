using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Teams;

namespace LigaVolley.Domain.TeamEntries;

public enum TeamEntryStatus { Registered, Active, Withdrawn, Disqualified }

public sealed class TeamEntry
{
    private TeamEntry() { }

    public TeamEntry(Competition competition, Team team, short? seed)
    {
        Competition = competition ?? throw new DomainValidationException("Competition is required.");
        Team = team ?? throw new DomainValidationException("Team is required.");
        SetSeed(seed);
        Status = TeamEntryStatus.Registered;
    }

    public int TeamEntryId { get; private set; }
    public int CompetitionId { get; private set; }
    public Competition Competition { get; private set; } = null!;
    public int TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public short? Seed { get; private set; }
    public TeamEntryStatus Status { get; private set; }
    public bool IsValid => Status is TeamEntryStatus.Registered or TeamEntryStatus.Active;

    public void SetSeed(short? seed)
    {
        if (seed <= 0) throw new DomainValidationException("Seed must be greater than zero when provided.");
        Seed = seed;
    }

    public void ChangeStatus(TeamEntryStatus status)
    {
        if (!Enum.IsDefined(status)) throw new DomainValidationException("TeamEntryStatus is invalid.");
        Status = status;
    }
}

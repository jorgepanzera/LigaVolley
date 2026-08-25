using LigaVolley.Domain.Common;
using LigaVolley.Domain.People;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Domain.CompetitionRosters;

public enum CompetitionRosterStatus { Draft, Active, Closed }
public enum CompetitionRosterMemberStatus { Active, Inactive }
public enum PlayerRole { Setter, OutsideHitter, MiddleBlocker, Opposite, Libero }

public sealed class CompetitionRoster
{
    private CompetitionRoster() { }
    public CompetitionRoster(TeamEntry entry)
    {
        TeamEntry = entry ?? throw new DomainValidationException("TeamEntry is required.");
        TeamEntryId = entry.TeamEntryId;
        Status = CompetitionRosterStatus.Draft;
    }
    public int CompetitionRosterId { get; private set; }
    public int TeamEntryId { get; private set; }
    public TeamEntry TeamEntry { get; private set; } = null!;
    public CompetitionRosterStatus Status { get; private set; }
    public List<CompetitionRosterPlayer> Players { get; private set; } = [];
    public List<CompetitionRosterStaff> Staff { get; private set; } = [];

    public void Activate()
    {
        EnsureEditable();
        if (Status != CompetitionRosterStatus.Draft) throw new DomainValidationException("Only a Draft roster can be activated.");
        Status = CompetitionRosterStatus.Active;
    }
    public void Close()
    {
        if (Status != CompetitionRosterStatus.Active) throw new DomainValidationException("Only an Active roster can be closed administratively.");
        Status = CompetitionRosterStatus.Closed;
    }
    public CompetitionRosterPlayer AddPlayer(Player player, short? jerseyNumber, PlayerRole role)
    {
        EnsureEditable();
        if (Players.Any(x => x.PlayerId == player.PlayerId)) throw new DomainValidationException("Player already belongs to this roster.");
        EnsurePlayerCapacity(role, jerseyNumber, null);
        var member = new CompetitionRosterPlayer(this, player, jerseyNumber, role); Players.Add(member); return member;
    }
    public void UpdatePlayer(CompetitionRosterPlayer member, short? jerseyNumber, PlayerRole role)
    { EnsureEditable(); EnsureMember(member); if(member.Status==CompetitionRosterMemberStatus.Active)EnsurePlayerCapacity(role, jerseyNumber, member); member.Update(jerseyNumber, role); }
    public void ChangePlayerStatus(CompetitionRosterPlayer member, CompetitionRosterMemberStatus status)
    {
        EnsureEditable(); EnsureMember(member);
        if (status == CompetitionRosterMemberStatus.Active && member.Status != status) EnsurePlayerCapacity(member.Role, member.JerseyNumber, member);
        member.ChangeStatus(status);
    }
    public CompetitionRosterStaff AddStaff(Coach coach)
    {
        EnsureEditable();
        if (Staff.Any(x => x.CoachId == coach.CoachId)) throw new DomainValidationException("Coach already belongs to this roster.");
        if (Staff.Count(x => x.Status == CompetitionRosterMemberStatus.Active) >= 2) throw new DomainValidationException("A roster cannot contain more than 2 active coaches.");
        var member = new CompetitionRosterStaff(this, coach); Staff.Add(member); return member;
    }
    public void ChangeStaffStatus(CompetitionRosterStaff member, CompetitionRosterMemberStatus status)
    {
        EnsureEditable();
        if (!Staff.Contains(member)) throw new DomainValidationException("Staff member does not belong to this roster.");
        if (status == CompetitionRosterMemberStatus.Active && member.Status != status && Staff.Count(x => x.Status == CompetitionRosterMemberStatus.Active) >= 2)
            throw new DomainValidationException("A roster cannot contain more than 2 active coaches.");
        member.ChangeStatus(status);
    }
    private void EnsurePlayerCapacity(PlayerRole role, short? jersey, CompetitionRosterPlayer? current)
    {
        if (!Enum.IsDefined(role)) throw new DomainValidationException("PlayerRole is invalid.");
        var active = Players.Where(x => x.Status == CompetitionRosterMemberStatus.Active && x != current).ToArray();
        if (active.Length >= 15) throw new DomainValidationException("A roster cannot contain more than 15 active players.");
        if (role == PlayerRole.Libero && active.Count(x => x.Role == PlayerRole.Libero) >= 2) throw new DomainValidationException("A roster cannot contain more than 2 active liberos.");
        if (jersey.HasValue && active.Any(x => x.JerseyNumber == jersey)) throw new DomainValidationException("Jersey number must be unique among active players.");
    }
    private void EnsureEditable() { if (Status == CompetitionRosterStatus.Closed) throw new DomainValidationException("A Closed roster cannot be modified."); }
    private void EnsureMember(CompetitionRosterPlayer member) { if (!Players.Contains(member)) throw new DomainValidationException("Player member does not belong to this roster."); }
}

public sealed class CompetitionRosterPlayer
{
    private CompetitionRosterPlayer() { }
    internal CompetitionRosterPlayer(CompetitionRoster roster, Player player, short? jersey, PlayerRole role) { CompetitionRoster=roster; Player=player; PlayerId=player.PlayerId; Update(jersey,role); Status=CompetitionRosterMemberStatus.Active; }
    public int CompetitionRosterPlayerId { get; private set; }
    public int CompetitionRosterId { get; private set; }
    public CompetitionRoster CompetitionRoster { get; private set; } = null!;
    public int PlayerId { get; private set; }
    public Player Player { get; private set; } = null!;
    public short? JerseyNumber { get; private set; }
    public PlayerRole Role { get; private set; }
    public CompetitionRosterMemberStatus Status { get; private set; }
    internal void Update(short? jersey, PlayerRole role) { if (!Enum.IsDefined(role)) throw new DomainValidationException("PlayerRole is invalid."); JerseyNumber=jersey; Role=role; }
    internal void ChangeStatus(CompetitionRosterMemberStatus status) { if (!Enum.IsDefined(status)) throw new DomainValidationException("Roster member status is invalid."); Status=status; }
}

public sealed class CompetitionRosterStaff
{
    private CompetitionRosterStaff() { }
    internal CompetitionRosterStaff(CompetitionRoster roster, Coach coach) { CompetitionRoster=roster; Coach=coach; CoachId=coach.CoachId; Status=CompetitionRosterMemberStatus.Active; }
    public int CompetitionRosterStaffId { get; private set; }
    public int CompetitionRosterId { get; private set; }
    public CompetitionRoster CompetitionRoster { get; private set; } = null!;
    public int CoachId { get; private set; }
    public Coach Coach { get; private set; } = null!;
    public CompetitionRosterMemberStatus Status { get; private set; }
    internal void ChangeStatus(CompetitionRosterMemberStatus status) { if (!Enum.IsDefined(status)) throw new DomainValidationException("Roster member status is invalid."); Status=status; }
}

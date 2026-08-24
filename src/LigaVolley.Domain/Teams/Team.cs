using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Domain.Teams;

public sealed class Team
{
    private Team() { }
    public Team(string name, Gender gender, Club? club) { Update(name, gender, club); Active = true; }
    public int TeamId { get; private set; }
    public int? ClubId { get; private set; }
    public Club? Club { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Gender Gender { get; private set; }
    public bool Active { get; private set; }
    public void Update(string name, Gender gender, Club? club)
    {
        var text = name?.Trim() ?? "";
        if (text.Length == 0) throw new DomainValidationException("Name is required.");
        if (text.Length > 150) throw new DomainValidationException("Name cannot exceed 150 characters.");
        if (!Enum.IsDefined(gender)) throw new DomainValidationException("Gender is invalid.");
        Name = text; Gender = gender; Club = club; ClubId = club?.ClubId;
    }
    public void SetActive(bool active) => Active = active;
}

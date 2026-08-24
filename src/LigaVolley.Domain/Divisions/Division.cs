using LigaVolley.Domain.Common;

namespace LigaVolley.Domain.Divisions;

public sealed class Division
{
    private Division()
    {
    }

    public Division(string name, short levelOrder, Gender gender)
    {
        Update(name, levelOrder, gender);
        Active = true;
    }

    public int DivisionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public short LevelOrder { get; private set; }
    public Gender Gender { get; private set; }
    public bool Active { get; private set; }

    public void Update(string name, short levelOrder, Gender gender)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
        {
            throw new DomainValidationException("Name is required.");
        }

        if (normalizedName.Length > 50)
        {
            throw new DomainValidationException("Name cannot exceed 50 characters.");
        }

        if (levelOrder <= 0)
        {
            throw new DomainValidationException("LevelOrder must be greater than zero.");
        }

        if (!Enum.IsDefined(gender))
        {
            throw new DomainValidationException("Gender is invalid.");
        }

        Name = normalizedName;
        LevelOrder = levelOrder;
        Gender = gender;
    }

    public void SetActive(bool active) => Active = active;
}

using LigaVolley.Domain.Common;

namespace LigaVolley.Domain.Seasons;

public sealed class Season
{
    private Season()
    {
    }

    public Season(short year, string name, DateOnly? startDate, DateOnly? endDate)
    {
        Update(year, name, startDate, endDate);
        Active = true;
    }

    public int SeasonId { get; private set; }
    public short Year { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool Active { get; private set; }

    public void Update(short year, string name, DateOnly? startDate, DateOnly? endDate)
    {
        var normalizedName = ValidateName(name);
        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
        {
            throw new DomainValidationException("EndDate cannot be earlier than StartDate.");
        }

        Year = year;
        Name = normalizedName;
        StartDate = startDate;
        EndDate = endDate;
    }

    public void SetActive(bool active) => Active = active;

    private static string ValidateName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new DomainValidationException("Name is required.");
        }

        if (normalized.Length > 100)
        {
            throw new DomainValidationException("Name cannot exceed 100 characters.");
        }

        return normalized;
    }
}

using LigaVolley.Domain.Common;

namespace LigaVolley.Domain.Clubs;

public sealed class Club
{
    private Club() { }
    public Club(string name, string? shortName) { Update(name, shortName); Active = true; }
    public int ClubId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? ShortName { get; private set; }
    public bool Active { get; private set; }
    public void Update(string name, string? shortName)
    {
        Name = Required(name, 150, nameof(Name));
        ShortName = Optional(shortName, 50, nameof(ShortName));
    }
    public void SetActive(bool active) => Active = active;
    private static string Required(string? value, int max, string field) { var text = value?.Trim() ?? ""; if (text.Length == 0) throw new DomainValidationException($"{field} is required."); if (text.Length > max) throw new DomainValidationException($"{field} cannot exceed {max} characters."); return text; }
    private static string? Optional(string? value, int max, string field) { var text = value?.Trim(); if (text?.Length > max) throw new DomainValidationException($"{field} cannot exceed {max} characters."); return string.IsNullOrEmpty(text) ? null : text; }
}

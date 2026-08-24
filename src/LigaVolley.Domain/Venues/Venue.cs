using LigaVolley.Domain.Common;

namespace LigaVolley.Domain.Venues;

public sealed class Venue
{
    private Venue() { }
    public Venue(string name, string? address) { Update(name, address); Active = true; }
    public int VenueId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public bool Active { get; private set; }
    public void Update(string name, string? address)
    {
        var normalizedName = name?.Trim() ?? ""; if (normalizedName.Length == 0) throw new DomainValidationException("Name is required."); if (normalizedName.Length > 150) throw new DomainValidationException("Name cannot exceed 150 characters.");
        var normalizedAddress = address?.Trim(); if (normalizedAddress?.Length > 250) throw new DomainValidationException("Address cannot exceed 250 characters.");
        Name = normalizedName; Address = string.IsNullOrEmpty(normalizedAddress) ? null : normalizedAddress;
    }
    public void SetActive(bool active) => Active = active;
}

namespace LigaVolley.Application.Venues;
public sealed record CreateVenueRequest(string Name, string? Address);
public sealed record UpdateVenueRequest(string Name, string? Address);
public sealed record VenueDto(int VenueId, string Name, string? Address, bool Active);
public sealed record VenueSummaryDto(int VenueId, string Name, string? Address, bool Active);

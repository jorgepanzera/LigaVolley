using LigaVolley.Domain.People;
namespace LigaVolley.Application.Abstractions.Persistence;
public interface IPeopleRepository
{
    Task<(IReadOnlyList<Person> Items,int Total)> ListPeopleAsync(string? query,string? documentType,string? documentNumber,DateOnly? birthDate,bool? active,int page,int pageSize,CancellationToken ct);
    Task<Person?> GetPersonAsync(int id,bool tracking,CancellationToken ct); Task<bool> DocumentExistsAsync(string type,string number,int? excludingId,CancellationToken ct); void Add(Person person);
    Task<PersonAdditionalDocument?> GetDocumentAsync(int personId,int documentId,bool tracking,CancellationToken ct); void Add(PersonAdditionalDocument document);
    Task<(IReadOnlyList<Player> Items,int Total)> ListPlayersAsync(string? query,bool? active,int page,int pageSize,CancellationToken ct); Task<Player?> GetPlayerAsync(int id,bool tracking,CancellationToken ct); Task<bool> PlayerExistsAsync(int personId,CancellationToken ct); void Add(Player player);
    Task<(IReadOnlyList<Coach> Items,int Total)> ListCoachesAsync(string? query,bool? active,int page,int pageSize,CancellationToken ct); Task<Coach?> GetCoachAsync(int id,bool tracking,CancellationToken ct); Task<bool> CoachExistsAsync(int personId,CancellationToken ct); void Add(Coach coach);
    Task<(IReadOnlyList<Referee> Items,int Total)> ListRefereesAsync(string? query,bool? active,int page,int pageSize,CancellationToken ct); Task<Referee?> GetRefereeAsync(int id,bool tracking,CancellationToken ct); Task<bool> RefereeExistsAsync(int personId,CancellationToken ct); void Add(Referee referee);
}

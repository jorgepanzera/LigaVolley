using LigaVolley.Domain.Common;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Domain.People;

public sealed class Person
{
    private Person() { }
    public Person(string? documentType, string? documentNumber, string firstName, string lastName, DateOnly? birthDate, Gender? gender, string? email, string? phone)
    {
        Update(documentType, documentNumber, firstName, lastName, birthDate, gender, email, phone);
        Active = true;
    }
    public int PersonId { get; private set; }
    public string? DocumentType { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public DateOnly? BirthDate { get; private set; }
    public Gender? Gender { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool Active { get; private set; }
    public Player? Player { get; private set; }
    public Coach? Coach { get; private set; }
    public Referee? Referee { get; private set; }
    public ICollection<PersonAdditionalDocument> AdditionalDocuments { get; } = new List<PersonAdditionalDocument>();

    public void Update(string? documentType, string? documentNumber, string firstName, string lastName, DateOnly? birthDate, Gender? gender, string? email, string? phone)
    {
        DocumentType = Normalize(documentType)?.ToUpperInvariant(); DocumentNumber = Normalize(documentNumber);
        if ((DocumentType is null) != (DocumentNumber is null)) throw new DomainValidationException("Document type and number must both be present or both be null.");
        FirstName = Required(firstName, 100, "First name"); LastName = Required(lastName, 100, "Last name");
        if (DocumentType?.Length > 20 || DocumentNumber?.Length > 30) throw new DomainValidationException("Document is too long.");
        if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new DomainValidationException("Birth date cannot be in the future.");
        if (gender.HasValue && !Enum.IsDefined(gender.Value)) throw new DomainValidationException("Gender is invalid.");
        Email = Normalize(email); Phone = Normalize(phone);
        if (Email?.Length > 200 || (Email is not null && !System.Net.Mail.MailAddress.TryCreate(Email, out _))) throw new DomainValidationException("Email is invalid.");
        if (Phone?.Length > 50) throw new DomainValidationException("Phone is too long.");
        BirthDate = birthDate; Gender = gender;
    }
    public void SetActive(bool active) => Active = active;
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Required(string? value, int max, string field) { var text = Normalize(value); if (text is null || text.Length > max) throw new DomainValidationException($"{field} is required and must contain at most {max} characters."); return text; }
}

public enum PersonAdditionalDocumentType { HealthCard, LeagueCard }
public enum HealthCardStatus { Valid, Missing, Expired, ValidityUnknown }

public sealed class PersonAdditionalDocument
{
    private PersonAdditionalDocument() { }
    public PersonAdditionalDocument(Person person, PersonAdditionalDocumentType documentType, string? documentNumber, DateOnly? validFrom, DateOnly? validTo, string? notes)
    { Person = person; PersonId = person.PersonId; Update(documentType, documentNumber, validFrom, validTo, notes); Active = true; }
    public int PersonAdditionalDocumentId { get; private set; }
    public int PersonId { get; private set; }
    public Person Person { get; private set; } = null!;
    public PersonAdditionalDocumentType DocumentType { get; private set; }
    public string? DocumentNumber { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public bool Active { get; private set; }
    public string? Notes { get; private set; }
    public void Update(PersonAdditionalDocumentType type, string? number, DateOnly? from, DateOnly? to, string? notes)
    { if (!Enum.IsDefined(type)) throw new DomainValidationException("Document type is invalid."); if (from > to) throw new DomainValidationException("Valid from cannot be after valid to."); DocumentType=type; DocumentNumber=Trim(number, 100,"Document number"); ValidFrom=from; ValidTo=to; Notes=Trim(notes,500,"Notes"); }
    public void SetActive(bool active) => Active=active;
    private static string? Trim(string? value,int max,string field) { var text=string.IsNullOrWhiteSpace(value)?null:value.Trim(); if(text?.Length>max) throw new DomainValidationException($"{field} is too long."); return text; }
}

public sealed class Player { private Player(){} public Player(Person person){Person=person;PersonId=person.PersonId;Active=true;} public int PlayerId{get;private set;} public int PersonId{get;private set;} public Person Person{get;private set;}=null!; public bool Active{get;private set;} public void SetActive(bool value)=>Active=value; }
public sealed class Coach { private Coach(){} public Coach(Person person){Person=person;PersonId=person.PersonId;Active=true;} public int CoachId{get;private set;} public int PersonId{get;private set;} public Person Person{get;private set;}=null!; public bool Active{get;private set;} public void SetActive(bool value)=>Active=value; }
public sealed class Referee { private Referee(){} public Referee(Person person){Person=person;PersonId=person.PersonId;Active=true;} public int RefereeId{get;private set;} public int PersonId{get;private set;} public Person Person{get;private set;}=null!; public bool Active{get;private set;} public void SetActive(bool value)=>Active=value; }

using LigaVolley.Domain.Common;using LigaVolley.Domain.People;
namespace LigaVolley.Domain.Tests;
public sealed class PeopleTests
{
 [Fact]public void Person_normalizes_identity_and_starts_active(){var p=new Person(" ci "," 1.234 "," Ana "," Pérez ",null,null,null,null);Assert.Equal("CI",p.DocumentType);Assert.Equal("1.234",p.DocumentNumber);Assert.True(p.Active);Assert.Null(p.Player);}
 [Fact]public void Person_requires_complete_document(){Assert.Throws<DomainValidationException>(()=>new Person("CI",null,"Ana","Pérez",null,null,null,null));}
 [Fact]public void Person_rejects_future_birth_date(){Assert.Throws<DomainValidationException>(()=>new Person(null,null,"Ana","Pérez",DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),null,null,null));}
 [Fact]public void Additional_document_validates_dates_and_allows_history(){var p=new Person(null,null,"Ana","Pérez",null,null,null,null);Assert.Throws<DomainValidationException>(()=>new PersonAdditionalDocument(p,PersonAdditionalDocumentType.HealthCard,null,new(2027,1,1),new(2026,1,1),null));var a=new PersonAdditionalDocument(p,PersonAdditionalDocumentType.HealthCard,"1",null,null,null);var b=new PersonAdditionalDocument(p,PersonAdditionalDocumentType.HealthCard,"2",null,null,null);Assert.NotSame(a,b);}
 [Fact]public void A_person_can_have_all_independent_profiles(){var p=new Person(null,null,"Ana","Pérez",null,null,null,null);var player=new Player(p);var coach=new Coach(p);var referee=new Referee(p);p.SetActive(false);Assert.True(player.Active);Assert.True(coach.Active);Assert.True(referee.Active);}
}

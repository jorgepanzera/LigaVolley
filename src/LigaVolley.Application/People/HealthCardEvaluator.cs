using LigaVolley.Domain.People;
namespace LigaVolley.Application.People;
public static class HealthCardEvaluator
{
 public static HealthCardDto Evaluate(Person p)
 {
  var cards=p.AdditionalDocuments.Where(x=>x.DocumentType==PersonAdditionalDocumentType.HealthCard&&x.Active).ToArray();
  if(cards.Length==0)return new(HealthCardStatus.Missing,null);
  var today=DateOnly.FromDateTime(DateTime.UtcNow);
  var valid=cards.Where(x=>x.ValidTo>=today&&(x.ValidFrom is null||x.ValidFrom<=today)).OrderByDescending(x=>x.ValidTo).ThenByDescending(x=>x.PersonAdditionalDocumentId).FirstOrDefault();
  if(valid is not null)return new(HealthCardStatus.Valid,Map(valid));
  var expired=cards.Where(x=>x.ValidTo<today).OrderByDescending(x=>x.ValidTo).ThenByDescending(x=>x.PersonAdditionalDocumentId).FirstOrDefault();
  if(expired is not null)return new(HealthCardStatus.Expired,Map(expired));
  var unknown=cards.OrderByDescending(x=>x.ValidTo).ThenByDescending(x=>x.ValidFrom).ThenByDescending(x=>x.PersonAdditionalDocumentId).First();
  return new(HealthCardStatus.ValidityUnknown,Map(unknown));
 }
 private static RelevantDocumentDto Map(PersonAdditionalDocument x)=>new(x.PersonAdditionalDocumentId,x.DocumentNumber,x.ValidFrom,x.ValidTo,x.Active);
}

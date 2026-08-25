using LigaVolley.Domain.Common;
using LigaVolley.Domain.Fixtures;

namespace LigaVolley.Domain.MatchSheets;

public enum LineupPosition { P1 = 1, P2, P3, P4, P5, P6 }
public enum MatchEventType { PrepareSet, SetLineup, StartSet, Point, PointCorrection, Substitution, LiberoEnter, LiberoExit, Timeout, MatchClosed }
public enum MatchEventStatus { Active, Cancelled }

public sealed class MatchLineup
{
    private MatchLineup() { }
    public MatchLineup(MatchSet set, MatchTeam team)
    { MatchSet=set; MatchTeam=team; MatchSetId=set.MatchSetId; MatchTeamId=team.MatchTeamId; }
    public int MatchLineupId { get; private set; }
    public int MatchSetId { get; private set; }
    public MatchSet MatchSet { get; private set; } = null!;
    public int MatchTeamId { get; private set; }
    public MatchTeam MatchTeam { get; private set; } = null!;
    public List<MatchLineupPosition> Positions { get; private set; } = [];
    public void Replace(IReadOnlyList<MatchPlayer> players)
    {
        if (MatchSet.Status != MatchSetStatus.Ready) throw new DomainValidationException("Lineup is locked.");
        if (players.Count != 6 || players.DistinctBy(x=>x.MatchPlayerId).Count()!=6) throw new DomainValidationException("Lineup requires six different players.");
        Positions.Clear();
        for(var i=0;i<6;i++) Positions.Add(new MatchLineupPosition(this,(LineupPosition)(i+1),players[i]));
    }
}

public sealed class MatchLineupPosition
{
    private MatchLineupPosition() { }
    internal MatchLineupPosition(MatchLineup lineup,LineupPosition position,MatchPlayer player)
    { MatchLineup=lineup;Position=position;MatchPlayer=player; }
    public int MatchLineupPositionId{get;private set;}public int MatchLineupId{get;private set;}public MatchLineup MatchLineup{get;private set;}=null!;
    public LineupPosition Position{get;private set;}public int MatchPlayerId{get;private set;}public MatchPlayer MatchPlayer{get;private set;}=null!;
}

public sealed class MatchEvent
{
    private MatchEvent() { }
    internal MatchEvent(MatchSheet sheet,MatchSet? set,Guid uuid,MatchEventType type,long sequence,MatchSide? side,int? playerId,DateTimeOffset now,MatchEvent? related)
    { if(uuid==Guid.Empty)throw new DomainValidationException("EventUuid is required.");MatchSheet=sheet;MatchSet=set;EventUuid=uuid;EventType=type;SequenceNumber=sequence;Side=side;MatchPlayerId=playerId;OccurredAt=now;Status=MatchEventStatus.Active;RelatedEvent=related; }
    public int MatchEventId{get;private set;}public Guid EventUuid{get;private set;}public int MatchSheetId{get;private set;}public MatchSheet MatchSheet{get;private set;}=null!;
    public int? MatchSetId{get;private set;}public MatchSet? MatchSet{get;private set;}public MatchEventType EventType{get;private set;}public long SequenceNumber{get;private set;}
    public MatchSide? Side{get;private set;}public int? MatchPlayerId{get;private set;}public DateTimeOffset OccurredAt{get;private set;}public MatchEventStatus Status{get;private set;}
    public int? MatchSheetSessionId{get;private set;}public MatchSheetSession? MatchSheetSession{get;private set;}public long? LocalSequence{get;private set;}public string? SyncPayloadHash{get;private set;}
    public int? RelatedEventId{get;private set;}public MatchEvent? RelatedEvent{get;private set;}public void Cancel(){if(Status!=MatchEventStatus.Active)throw new DomainValidationException("Event is already cancelled.");Status=MatchEventStatus.Cancelled;}
    public void BindSynchronization(MatchSheetSession session,long localSequence,string payloadHash,DateTimeOffset occurredAt){if(MatchSheetSession is not null)throw new DomainValidationException("Event synchronization metadata already exists.");MatchSheetSession=session;LocalSequence=localSequence;SyncPayloadHash=payloadHash;OccurredAt=occurredAt;}
}

public sealed class MatchSubstitution
{
    private MatchSubstitution() { }
    public MatchSubstitution(Guid uuid,MatchSet set,MatchTeam team,MatchPlayer playerOut,MatchPlayer playerIn,LineupPosition position,DateTimeOffset now)
    { SubstitutionUuid=uuid;MatchSet=set;MatchTeam=team;PlayerOut=playerOut;PlayerIn=playerIn;LineupPosition=position;OccurredAt=now; }
    public int MatchSubstitutionId{get;private set;}public Guid SubstitutionUuid{get;private set;}public int MatchSetId{get;private set;}public MatchSet MatchSet{get;private set;}=null!;
    public int MatchTeamId{get;private set;}public MatchTeam MatchTeam{get;private set;}=null!;public int PlayerOutMatchPlayerId{get;private set;}public MatchPlayer PlayerOut{get;private set;}=null!;
    public int PlayerInMatchPlayerId{get;private set;}public MatchPlayer PlayerIn{get;private set;}=null!;public LineupPosition LineupPosition{get;private set;}public DateTimeOffset OccurredAt{get;private set;}
}

public sealed class MatchLiberoReplacement
{
    private MatchLiberoReplacement() { }
    public MatchLiberoReplacement(Guid uuid,MatchSet set,MatchTeam team,MatchPlayer libero,MatchPlayer replaced,LineupPosition position,DateTimeOffset now)
    { ReplacementUuid=uuid;MatchSet=set;MatchTeam=team;Libero=libero;Replaced=replaced;LineupPosition=position;EnteredAt=now; }
    public int MatchLiberoReplacementId{get;private set;}public Guid ReplacementUuid{get;private set;}public int MatchSetId{get;private set;}public MatchSet MatchSet{get;private set;}=null!;
    public int MatchTeamId{get;private set;}public MatchTeam MatchTeam{get;private set;}=null!;public int LiberoMatchPlayerId{get;private set;}public MatchPlayer Libero{get;private set;}=null!;
    public int ReplacedMatchPlayerId{get;private set;}public MatchPlayer Replaced{get;private set;}=null!;public LineupPosition LineupPosition{get;private set;}public DateTimeOffset EnteredAt{get;private set;}public DateTimeOffset? ExitedAt{get;private set;}
    public void Exit(DateTimeOffset now){if(ExitedAt.HasValue)throw new DomainValidationException("Libero replacement is not active.");ExitedAt=now;}
}

public sealed class MatchTimeout
{
    private MatchTimeout() { }
    public MatchTimeout(Guid uuid,MatchSet set,MatchTeam team,byte timeoutNumber,DateTimeOffset now){if(timeoutNumber is not 1 and not 2)throw new DomainValidationException("TimeoutNumber must be 1 or 2.");TimeoutUuid=uuid;MatchSet=set;MatchTeam=team;TimeoutNumber=timeoutNumber;OccurredAt=now;}
    public int MatchTimeoutId{get;private set;}public Guid TimeoutUuid{get;private set;}public int MatchSetId{get;private set;}public MatchSet MatchSet{get;private set;}=null!;
    public int MatchTeamId{get;private set;}public MatchTeam MatchTeam{get;private set;}=null!;public byte TimeoutNumber{get;private set;}public DateTimeOffset OccurredAt{get;private set;}
}

public sealed record CourtPlayerState(LineupPosition LogicalLineupPosition,LineupPosition PhysicalPosition,int EffectiveMatchPlayerId,bool IsLiberoReplacement);

public static class MatchCourtStateCalculator
{
    public static LineupPosition ToPhysical(LineupPosition logical,byte rotationOffset)=>
        (LineupPosition)((((int)logical-1-rotationOffset)%6+6)%6+1);

    public static IReadOnlyList<CourtPlayerState> Calculate(MatchLineup lineup,byte offset,IEnumerable<MatchSubstitution> substitutions,IEnumerable<MatchLiberoReplacement> replacements)
    {
        var occupants=lineup.Positions.ToDictionary(x=>x.Position,x=>x.MatchPlayerId);
        foreach(var s in substitutions)
        {
            var current=occupants[s.LineupPosition];
            if(current==s.PlayerOutMatchPlayerId)occupants[s.LineupPosition]=s.PlayerInMatchPlayerId;
        }
        var active=replacements.Where(x=>!x.ExitedAt.HasValue).ToDictionary(x=>x.LineupPosition,x=>x.LiberoMatchPlayerId);
        return occupants.OrderBy(x=>x.Key).Select(x=>new CourtPlayerState(x.Key,ToPhysical(x.Key,offset),active.GetValueOrDefault(x.Key,x.Value),active.ContainsKey(x.Key))).ToArray();
    }
    public static int Server(IReadOnlyList<CourtPlayerState> state)=>state.Single(x=>x.PhysicalPosition==LineupPosition.P1).EffectiveMatchPlayerId;
}

public static class MatchSetRebuilder
{
    public static (short Home,short Away,MatchSide Serving,byte HomeOffset,byte AwayOffset) Rebuild(MatchSide initial,IEnumerable<MatchEvent> events)
    {
        short home=0,away=0;byte ho=0,ao=0;var serving=initial;
        foreach(var e in events.Where(x=>x.Status==MatchEventStatus.Active&&x.EventType==MatchEventType.Point).OrderBy(x=>x.SequenceNumber))
        { var side=e.Side!.Value;if(side==MatchSide.Home)home++;else away++;if(serving!=side){if(side==MatchSide.Home)ho=(byte)((ho+1)%6);else ao=(byte)((ao+1)%6);}serving=side; }
        return(home,away,serving,ho,ao);
    }
}

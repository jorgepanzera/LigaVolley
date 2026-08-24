namespace LigaVolley.Domain.Fixtures;

public sealed record GeneratedPairing(short RoundNumber, short MatchNumber, int HomeParticipantId, int AwayParticipantId);

public static class RoundRobinFixtureGenerator
{
    public static IReadOnlyList<GeneratedPairing> Generate(IReadOnlyList<int> participantIds, int randomSeed, bool mirrored)
    {
        if (participantIds.Count < 2) throw new ArgumentException("At least two participants are required.", nameof(participantIds));
        if (participantIds.Distinct().Count() != participantIds.Count) throw new ArgumentException("Participants must be unique.", nameof(participantIds));
        var shuffled = participantIds.ToArray(); Shuffle(shuffled, new Random(randomSeed));
        var slot = shuffled.Select((id,index)=>(id,index)).ToDictionary(x=>x.id,x=>x.index);
        var rotation = shuffled.Select<int,int?>(x=>x).ToList(); if (rotation.Count % 2 != 0) rotation.Add(null);
        var rounds = rotation.Count - 1; var matchesPerRound = rotation.Count / 2; var first = new List<MutablePairing>();
        for (short round=1; round<=rounds; round++)
        {
            for (var i=0;i<matchesPerRound;i++)
            {
                var a=rotation[i]; var b=rotation[rotation.Count-1-i]; if (!a.HasValue || !b.HasValue) continue;
                var aHome=IsHome(slot[a.Value],slot[b.Value],shuffled.Length);
                first.Add(new(round, aHome?a.Value:b.Value, aHome?b.Value:a.Value));
            }
            var last=rotation[^1]; rotation.RemoveAt(rotation.Count-1); rotation.Insert(1,last);
        }
        if (!mirrored) OptimizeStreaks(first, shuffled);
        var result=new List<GeneratedPairing>(); short number=1;
        foreach(var x in first.OrderBy(x=>x.Round)) result.Add(new(x.Round,number++,x.Home,x.Away));
        if (mirrored) foreach(var x in first.OrderBy(x=>x.Round)) result.Add(new((short)(x.Round+rounds),number++,x.Away,x.Home));
        return result;
    }

    private static bool IsHome(int a,int b,int n)
    {
        var diff=(b-a+n)%n; var half=n/2;
        if (n%2!=0) return diff<=half;
        if (diff<half) return true; if(diff>half)return false;
        var low=Math.Min(a,b); var selected=low%2==0?low:(low+half)%n; return a==selected;
    }
    private static void Shuffle<T>(T[] values,Random random) { for(var i=values.Length-1;i>0;i--){var j=random.Next(i+1);(values[i],values[j])=(values[j],values[i]);} }
    private static void OptimizeStreaks(List<MutablePairing> matches,int[] teams)
    {
        var current=Score(matches,teams); var changed=true;
        while(changed)
        {
            changed=false;
            for(var ai=0;ai<teams.Length&&!changed;ai++) for(var bi=ai+1;bi<teams.Length&&!changed;bi++) for(var ci=bi+1;ci<teams.Length&&!changed;ci++)
            {
                var edges=new[]{Find(matches,teams[ai],teams[bi]),Find(matches,teams[bi],teams[ci]),Find(matches,teams[ci],teams[ai])};
                if(edges.Any(x=>x is null)||!IsDirectedCycle(edges!,teams[ai],teams[bi],teams[ci]))continue;
                foreach(var edge in edges) edge!.Flip(); var candidate=Score(matches,teams);
                if(candidate.CompareTo(current)<0){current=candidate;changed=true;} else foreach(var edge in edges) edge!.Flip();
            }
        }
    }
    private static MutablePairing? Find(List<MutablePairing> matches,int a,int b)=>matches.SingleOrDefault(x=>(x.Home==a&&x.Away==b)||(x.Home==b&&x.Away==a));
    private static bool IsDirectedCycle(MutablePairing?[] e,int a,int b,int c)=> (e[0]!.Home==a&&e[1]!.Home==b&&e[2]!.Home==c)||(e[0]!.Home==b&&e[1]!.Home==c&&e[2]!.Home==a);
    private static FixtureScore Score(List<MutablePairing> matches,int[] teams)
    {
        var max=0;var repeats=0;
        foreach(var team in teams){var sequence=matches.OrderBy(x=>x.Round).Where(x=>x.Home==team||x.Away==team).Select(x=>x.Home==team).ToArray();var streak=0;bool? last=null;foreach(var home in sequence){if(last==home){streak++;repeats++;}else{streak=1;last=home;}max=Math.Max(max,streak);}}
        return new(max,repeats);
    }
    private sealed class MutablePairing(short round,int home,int away){public short Round{get;}=round;public int Home{get;private set;}=home;public int Away{get;private set;}=away;public void Flip()=>(Home,Away)=(Away,Home);}
    private readonly record struct FixtureScore(int MaxStreak,int Repeats):IComparable<FixtureScore>{public int CompareTo(FixtureScore other){var x=MaxStreak.CompareTo(other.MaxStreak);return x!=0?x:Repeats.CompareTo(other.Repeats);}}
}

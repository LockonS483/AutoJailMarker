namespace AutoJailMarker.Classes;

public record struct PartyIndex(string Name, ulong ObjectId, int Index)
{
    public readonly string Name = Name;
    public readonly ulong ObjectId = ObjectId;
    public readonly int Index = Index;
}
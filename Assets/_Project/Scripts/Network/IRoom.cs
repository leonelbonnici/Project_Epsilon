// Any "room" in an area: arena, puzzle, walkthrough, etc.
// The area framework only cares about completion — not what kind of room it is.
public interface IRoom
{
    string RoomId { get; }
    bool IsCompleted { get; }
    event System.Action<IRoom> RoomCompleted;
}
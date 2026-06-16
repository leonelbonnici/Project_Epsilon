public interface IPersistable
{
    string PersistenceId { get; }
    string CaptureState();
    void RestoreState(string state);
}
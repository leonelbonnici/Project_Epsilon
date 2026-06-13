// Anything a player can press the interact key on: pickups, switches, NPCs, signs, levers.
// Implementors decide what happens when ServerOnInteract is called.
public interface IInteractable
{
    void ServerOnInteract(NetworkPlayMakerBridge interactor);
}
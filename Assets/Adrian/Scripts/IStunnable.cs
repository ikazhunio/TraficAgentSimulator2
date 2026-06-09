/// <summary>
/// Contrato que deben implementar todos los NPCs que pueden ser stunneados.
/// Permite que StunManager funcione con NPCController y PedestrianController.
/// </summary>
public interface IStunnable
{
    void NotifyStunApplied();
}

public enum Team { Player, Enemy }

public interface IDamageable
{
    Team Team { get; }
    void ServerApplyDamage(float amount);
}
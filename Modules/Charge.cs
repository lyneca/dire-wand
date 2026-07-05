using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Charge : WandSkill
{
    public float explosionRadius = 3;
    public float explosionForce;

    public void ChargeTarget()
    {
        switch (wand.target)
        {
            case Item item:
                item.OnNextCollision(_ => Explode(item));
                break;
            case Creature creature:
                creature.OnKillEvent += OnKill;
                break;
        }
    }

    private void OnKill(CollisionInstance collisionInstance, EventTime eventTime)
    {
        if (eventTime is not EventTime.OnEnd ||
            collisionInstance.targetColliderGroup.collisionHandler?.Entity is not Creature creature)
            return;
        
        creature.OnKillEvent -= OnKill;
        Explode(creature);
    }

    public void Explode(Item item)
    {
        item.breakable?.Break();
    }

    public void Explode(Creature creature)
    {
        Explode(creature.ragdoll.targetPart.transform.position);
    }

    public void Explode(Vector3 position)
    {
        var entities = ThunderEntity.InRadiusNaive(position, explosionRadius);
        
        for (var i = 0; i < entities.Count; i++)
        {
            switch (entities[i])
            {
                case Item item:
                    item.breakable?.Break();
                    item.AddExplosionForce(explosionForce, position, explosionRadius, 1, ForceMode.Impulse);
                    break;
                case Creature { isPlayer: true } player:
                    player.AddExplosionForce(explosionForce, position, explosionRadius, 1, ForceMode.Impulse);
                    break;
                case Creature creature:
                    break;
            }
        }
    }
}
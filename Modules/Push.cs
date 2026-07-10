using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Push : WandSkill {
    public float creatureForce = 15f;
    public float itemForce = 10f;
    public override void Register() {
        base.Register();
        wand.OnTargetEntity(step => step
            .ThenRepeatable(wand.MainHand.Moving(Direction.Forward).Palm(Direction.Forward))
            .Do(() => ShoveEntity(wand.target))
            .Then(wand.Still()));
    }

    public void ShoveEntity(ThunderEntity entity) {
        if (entity == null) {
            wand.Reset();
            return;
        }

        MarkCasted();

        wand.PlaySound(SoundType.Foll, entity.RootTransform);

        var direction = (entity.Center - wand.tip.position).normalized;
        if (Creature.AimAssist(entity.Center, direction, 10, 50, out var target, Filter.LiveNPCs))
        {
            direction = (target.position - entity.Center).normalized;
        }
        else
        {
            direction += Vector3.up * 0.1f; 
        }
        if (entity is Creature creature) {
            creature.TryPush(Creature.PushType.Magic, direction * creatureForce, 4);
            if (!creature.isKilled)
                creature.ragdoll.SetState(Ragdoll.State.Destabilized);
            creature.AddForce(direction * creatureForce, ForceMode.VelocityChange);
        } else if (entity is Item otherItem) {
            otherItem.physicBody.AddForce(direction * itemForce, ForceMode.VelocityChange);
            otherItem.Throw(1, Item.FlyDetection.Forced);
            otherItem.lastHandler = wand.holdingHand;
        }

        wand.module.shoveEffectData.Spawn(entity.transform).Play();
    }
}
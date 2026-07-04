using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Flipendo : WandSkill {
    public float creatureForce = 15f;
    public float itemForce = 10f;
    public override void OnInit() {
        base.OnInit();
        wand.OnTargetEntity(step => step
            .ThenRepeatable(wand.Flick(wand.OutwardsDirection, wand.module.gestureVelocityLarge))
            .Do(() => ShoveEntity(wand.target)));
    }

    public void ShoveEntity(ThunderEntity entity) {
        if (entity == null) {
            wand.Reset();
            return;
        }

        MarkCasted();

        wand.PlaySound(SoundType.Foll, entity.RootTransform);

        var direction = (entity.Center - wand.tip.position).normalized;
        if (Creature.AimAssist(entity.Center, direction, 10, 30, out var target, Filter.LiveNPCs))
        {
            direction = target.position - entity.Center;
        }
        else
        {
            direction += Vector3.up * 0.5f; 
        }
        // float floatMult = entity.Has<Floating>() ? 1.5f : 1;
        entity.Inflict("WandFloating", this, 3f);
        if (entity is Creature creature) {
            creature.TryPush(Creature.PushType.Magic, direction * creatureForce, 4);
            if (!creature.isKilled)
                creature.ragdoll.SetState(Ragdoll.State.Destabilized);
            creature.AddForce(direction * creatureForce, ForceMode.VelocityChange);
        } else if (entity is Item otherItem) {
            otherItem.physicBody.AddForce(direction * itemForce, ForceMode.VelocityChange);
        }

        wand.module.shoveEffectData.Spawn(entity.transform).Play();
    }
}
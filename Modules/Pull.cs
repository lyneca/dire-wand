using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Pull : WandSkill {
    public float creatureForce = 15f;
    public float itemForce = 10f;
    public override void Register() {
        base.Register();
        // wand.OnTargetEntity(step => step
        //     .ThenRepeatable("Button Held", () => wand.Buttoning)
        //     .Do(() => PullEntity(wand.target)));
    }

    public void PullEntity(ThunderEntity entity) {
        if (entity == null) {
            wand.Reset();
            return;
        }

        MarkCasted();

        wand.PlaySound(SoundType.Foll, entity.RootTransform);

        float floatMult = entity.RootPhysicBody.useGravity ? 1f : 1.5f;
        var direction = (wand.tip.position - entity.Center).normalized + Vector3.up * 0.5f;
        switch (entity)
        {
            case Creature creature:
            {
                creature.Poke();
                creature.TryPush(Creature.PushType.Magic, direction * creatureForce, 4);
                if (!creature.isKilled)
                    creature.ragdoll.SetState(Ragdoll.State.Destabilized);
                creature.AddForce(direction * (creatureForce * floatMult), ForceMode.VelocityChange);
                break;
            }
            case Item otherItem:
                otherItem.physicBody.AddForce(direction * (itemForce * floatMult), ForceMode.VelocityChange);
                break;
        }

        wand.module.shoveEffectData.Spawn(entity.transform).Play();
    }
}

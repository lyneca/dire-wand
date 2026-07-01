using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Pull : WandModule {
    public float creatureForce = 15f;
    public float itemForce = 10f;
    public override void OnInit() {
        base.OnInit();
        wand.OnTargetEntity(step => step.Then(wand.Offhand.Palm(Direction.Down).Gripping.Moving(Direction.Backward)).Do(() => PullEntity(wand.target)));
    }

    public void PullEntity(Entity entity) {
        if (entity == null) {
            wand.Reset();
            return;
        }

        MarkCasted();

        wand.PlaySound(SoundType.Foll, entity.Transform);

        float floatMult = entity.Has<Floating>() ? 1.5f : 1;
        var direction = (wand.tip.position - entity.Center()).normalized + Vector3.up * 0.5f;
        if (entity.creature is Creature creature) {
            creature.TryPush(Creature.PushType.Magic, direction * creatureForce, 4);
            if (!creature.isKilled)
                creature.ragdoll.SetState(Ragdoll.State.Destabilized);
            creature.AddForce(direction * (creatureForce * floatMult), ForceMode.VelocityChange);
        } else if (entity.item is Item otherItem) {
            otherItem.physicBody.AddForce(direction * (itemForce * floatMult), ForceMode.VelocityChange);
        }

        wand.module.shoveEffectData.Spawn(entity.transform).Play();
    }
}

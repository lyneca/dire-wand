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

        var direction = (wand.tip.position - entity.Center()).normalized + Vector3.up * 0.5f;
        if (entity.creature is Creature creature) {
            creature.TryPush(Creature.PushType.Magic, direction * creatureForce, 4);
            creature.ragdoll.SetState(Ragdoll.State.Destabilized);
            creature.AddForce(direction * creatureForce, ForceMode.VelocityChange);
        } else if (entity.item is Item otherItem) {
            otherItem.rb.AddForce(direction * itemForce, ForceMode.VelocityChange);
        }

        wand.module.shoveEffectData.Spawn(entity.transform).Play();
    }
}

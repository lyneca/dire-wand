using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Flipendo : WandModule {
    public float creatureForce = 15f;
    public float itemForce = 10f;
    public override void OnInit() {
        base.OnInit();
        wand.button
            .Then(wand.Brandish())
            .Do(() => {
                var entity = wand.TargetEntity(wand.module.shoveArgs);
                wand.RunAfter(() => ShoveEntity(entity), 0.3f);
            }, "Shove Entity");
    }

    public void ShoveEntity(Entity entity) {
        if (entity == null) {
            wand.Reset();
            return;
        }
        
        MarkCasted();
            
        wand.PlaySound(SoundType.Foll, entity.Transform);

        var direction = (entity.Center() - wand.tip.position).normalized + Vector3.up * 0.5f;
        if (entity.creature is Creature creature) {
            creature.TryPush(Creature.PushType.Magic, direction * creatureForce, 4);
            creature.ragdoll.SetState(Ragdoll.State.Destabilized);
            creature.AddForce(direction * creatureForce, ForceMode.VelocityChange);
        } else if (entity.item is Item otherItem) {
            otherItem.physicBody.AddForce(direction * itemForce, ForceMode.VelocityChange);
        }

        wand.module.shoveEffectData.Spawn(entity.transform).Play();
    }
}
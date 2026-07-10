using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Polymorph : WandSkill {
    public override void Register() {
        base.Register();

        wand.profane
            .Then(wand.Swirl(SwirlDirection.Clockwise))
            .Do(PolymorphEntity, "Polymorph");
    }

    public void PolymorphEntity() {
        if (wand.target is not Creature creature) {
            wand.Reset();
            return;
        }
        MarkCasted();
            
        wand.PlaySound(SoundType.Hagh, wand.TargetTransform);

        creature.handLeft?.TryRelease();
        creature.handRight?.TryRelease();
        var position = creature.GetTorso().transform.position;
        var rotation = Quaternion.LookRotation(creature.GetHead().transform.forward);
        creature.Despawn();
        wand.module.polymorphEffectData.Spawn(position, rotation).Play();
        Catalog.GetData<CreatureData>("Chicken").SpawnAsync(position, 0);
    }

}
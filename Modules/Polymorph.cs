using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Polymorph : WandModule {
    public override void OnInit() {
        base.OnInit();

        wand.targetedEnemy
            .Then(wand.Swirl(SwirlDirection.Clockwise))
            .Do(PolymorphEntity, "Polymorph");
    }

    public void PolymorphEntity() {
        if (wand.target?.creature == null) {
            wand.Reset();
            return;
        }
        MarkCasted();
            
        wand.PlaySound(SoundType.Hagh, wand.target.Transform);

        wand.target.creature.handLeft?.TryRelease();
        wand.target.creature.handRight?.TryRelease();
        var position = wand.target.creature.GetTorso().transform.position;
        var rotation = Quaternion.LookRotation(wand.target.creature.GetHead().transform.forward);
        wand.target.creature.Despawn();
        wand.module.polymorphEffectData.Spawn(position, rotation).Play();
        Catalog.GetData<CreatureData>("Chicken").SpawnAsync(position, 0);
    }

}
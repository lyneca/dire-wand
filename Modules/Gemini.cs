using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Gemini : WandSkill {
    public override void Register() {
        base.Register();
        wand.targetedItem
            .ThenRepeatable(wand.Swirl(SwirlDirection.Either))
            .Do(CloneItem, "Clone Item")
            .Then(wand.Still());
    }

    public void CloneItem() {
        if (wand.target is not Item item) {
            wand.Reset();
            return;
        }
        
        MarkCasted();
            
        wand.PlaySound(SoundType.Quough, item.transform);

        item.Clone(wand.module.cloneEffectData);
        wand.canRestart = true;
    }
}
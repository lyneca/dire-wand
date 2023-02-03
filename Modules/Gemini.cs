using ThunderRoad;
using UnityEngine;

namespace Wand {
    public class Gemini : WandModule {
        public override void OnInit() {
            base.OnInit();
            wand.targetedItem
                .Then(wand.Point(ViewDir.Down), wand.Swirl(SwirlDirection.Either))
                .Do(CloneItem, "Clone Item");
        }

        public void CloneItem() {
            if (wand.target.handler.item is not Item item) {
                wand.Reset();
                return;
            }
            
            wand.PlaySound(SoundType.Quough, item.transform);

            item.Clone(wand.module.cloneEffectData);
            wand.canRestart = true;
        }
    }
}
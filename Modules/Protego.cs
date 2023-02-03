using System.Linq;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand {
    public class Protego : WandModule {
        public string shieldEffectId = "WandShield";
        private EffectData shieldEffectData;
        public override void OnInit() {
            base.OnInit();
            shieldEffectData = Catalog.GetData<EffectData>(shieldEffectId);
            wand.button
                .Then(() => wand.holdingHand.Velocity().IsFacing(-wand.otherHand.Velocity())
                            && wand.holdingHand.Velocity().IsFacing(wand.holdingHand.transform.position - wand.otherHand.transform.position)
                            && wand.otherHand.Velocity().IsFacing(wand.otherHand.transform.position - wand.holdingHand.transform.position)
                            && wand.holdingHand.Velocity().magnitude > wand.module.gestureVelocityNormal
                            && wand.otherHand.Velocity().magnitude > wand.module.gestureVelocityNormal)
                .Do(Shield);
        }

        public void Shield() {
            shieldEffectData.Spawn(Player.currentCreature.GetTorso().transform.position, Quaternion.identity).Play();
            var creatures = Utils.CreaturesInRadius(Player.currentCreature.transform.position, 4, false, true).ToList();
            for (var i = 0; i < creatures.Count; i++) {
                var creature = creatures[i];
                creature.TryPush(Creature.PushType.Hit,
                    creature.GetTorso().transform.position - Player.currentCreature.GetTorso().transform.position, 3);
            }

            var items = Item.allThrowed.ToList();

            for (var i = 0; i < items.Count; i++) {
                var item = items[i];
                if (Vector3.Distance(item.transform.position, Player.local.head.transform.position) > 4
                    || item.mainHandler != null) continue;
                item.rb.velocity
                    = item.rb.HomingThrow(
                        Vector3.Reflect(item.rb.velocity,
                            (item.transform.position - Player.local.head.transform.position).normalized), 30);
                item.ResetRagdollCollision();
                item.SetColliderAndMeshLayer(GameManager.GetLayer(LayerName.MovingItem));
                item.StopFlying();
                item.Throw(1, Item.FlyDetection.Forced);
            }
        }
    }
}

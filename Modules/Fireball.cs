using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand {
    public class Fireball : WandModule {
        public ItemData fireballData;
        public DamagerData damagerData;
        public EffectData fireballEffectData;
        public override void OnInit() {
            base.OnInit();
            fireballData = Catalog.GetData<ItemData>("DynamicProjectile");
            damagerData = Catalog.GetData<DamagerData>("Fireball");
            fireballEffectData = Catalog.GetData<EffectData>("SpellFireball");
            wand.button.Then(() => wand.tipViewVelocity.z > wand.module.gestureVelocityLarge
                                   && !wand.localTipVelocity.MostlyZ(), "Slash Sideways")
                .Do(ThrowFireball, "Throw Fireball");
        }

        public void ThrowFireball() {
            wand.PlaySound(SoundType.Foll);
            wand.module.castEffectData.Spawn(wand.tip).Play();
            fireballData.SpawnAsync(projectile => {
                projectile.transform.SetPositionAndRotation(wand.tip.position, wand.tip.rotation);
                for (var index = 0; index < projectile.collisionHandlers.Count; index++) {
                    var collisionHandler = projectile.collisionHandlers[index];
                    for (var i = 0; i < collisionHandler.damagers.Count; i++) {
                        var damager = collisionHandler.damagers[i];
                        damager.Load(damagerData, collisionHandler);
                    }
                }

                projectile.rb.useGravity = false;

                ItemMagicProjectile component = projectile.GetComponent<ItemMagicProjectile>();
                component.guidance = GuidanceMode.NonGuided;
                component.homing = true;
                component.targetCreature = Utils.TargetCreature(wand.tip.position,
                    Vector3.Slerp(wand.tipVelocity.normalized, Player.local.head.transform.forward, 0.5f), 20, 30);
                component.speed = 15;
                component.item.lastHandler = item.lastHandler;
                component.allowDeflect = false;
                component.imbueEnergyTransfered = 10;
                component.Fire(
                    Vector3.Slerp(wand.tipVelocity.normalized, Player.local.head.transform.forward, 0.5f)
                    * (wand.tipVelocity.magnitude * 5), fireballEffectData, item);
            });
            wand.canRestart = true;
        }

    }
}

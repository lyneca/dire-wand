using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Fireball : WandModule {
    public static ItemData fireballData;
    public static DamagerData damagerData;
    public static EffectData fireballEffectData;
    public int numCasts = 3;

    public override void OnInit() {
        base.OnInit();
        fireballData = Catalog.GetData<ItemData>("DynamicProjectile");
        damagerData = Catalog.GetData<DamagerData>("Fireball");
        fireballEffectData = Catalog.GetData<EffectData>("SpellFireball");

        var step = wand.button
            .Then(wand.Swirl(SwirlDirection.Clockwise));

        for (var i = 0; i < numCasts; i++) {
            step = step.Then(wand.Brandish())
                .Do(() => {
                    ThrowFireballStatic(wand, wand.tip.forward * 20);
                    MarkCasted();
                }, "Throw Fireball");
        }
    }

    public static void ThrowFireballStatic(WandBehaviour wand, Vector3 direction) {
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

            var component = projectile.GetComponent<ItemMagicProjectile>();
            component.guidance = GuidanceMode.NonGuided;
            component.homing = true;
            component.targetCreature = Utils.TargetCreature(wand.tip.position, direction.normalized, 20, 30);
            component.speed = 15;
            component.item.lastHandler = wand.item.lastHandler;
            component.allowDeflect = false;
            component.imbueEnergyTransfered = 10;
            component.Fire(direction, fireballEffectData, wand.item);
        });
        wand.canRestart = true;
    }
}
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Fireball : WandSkill {
    public static ItemData fireballData;
    public static DamagerData damagerData;
    public static EffectData fireballEffectData;
    public int numCasts = 3;

    public override void Register()
    {
        base.Register();
        fireballData = Catalog.GetData<ItemData>("DynamicProjectile");
        damagerData = Catalog.GetData<DamagerData>("Fireball");
        fireballEffectData = Catalog.GetData<EffectData>("SpellFireball");

        var fireball = wand.button
            .Then(wand.Swirl(SwirlDirection.Clockwise));

        fireball.Then("Slash Sideways", () => wand.tipViewVelocity.z > wand.module.gestureVelocityLarge && !wand.localTipVelocity.MostlyZ())
            .Repeatable()
            .Do(() =>
            {
                ThrowFireballStatic(wand);
                MarkCasted();
            }, "Throw Fireball");
        //.ThenResetTo(fireball);
    }

    public static void ThrowFireballStatic(WandBehaviour wand) {
        wand.PlaySound(SoundType.Foll);
        wand.module.castEffectData.Spawn(wand.tip).Play();
        var direction = Vector3.Slerp(wand.tipVelocity.normalized, Player.local.head.transform.forward, 0.5f)
                        * (wand.tipVelocity.magnitude * 5);
        fireballData.SpawnAsync(projectile => {
            projectile.transform.SetPositionAndRotation(wand.tip.position, wand.tip.rotation);
            for (var index = 0; index < projectile.collisionHandlers.Count; index++) {
                var collisionHandler = projectile.collisionHandlers[index];
                for (var i = 0; i < collisionHandler.damagers.Count; i++) {
                    var damager = collisionHandler.damagers[i];
                    damager.Load(damagerData, collisionHandler);
                }
            }

            projectile.physicBody.useGravity = false;

            var component = projectile.GetComponent<ItemMagicProjectile>();
            component.guidance = GuidanceMode.NonGuided;
            component.targetCreature = null;
            component.speed = 15;
            component.item.lastHandler = wand.item.lastHandler;
            component.allowDeflect = false;
            component.imbueEnergyTransfered = 0;
            component.Fire(direction, fireballEffectData, wand.item, homing: true);
            component.directedHoming = true;
        });
        // wand.canRestart = true;
    }
}
using System;
using System.Collections;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Hover : WandSkill {
    public EffectData slamEffect;
    public float liftForceAmount = 3f;

    public override void Register() {
        base.Register();
        slamEffect = Catalog.GetData<EffectData>("WandSlam");

        wand.OnTargetEntity(state =>
        {
            state
                .Then(wand.Flick(AxisDirection.Up, wand.module.gestureVelocityLarge))
                .Repeatable()
                .Do(HoverEntity, "Hover Entity");
            state
                .ThenRepeatable(wand.Flick(AxisDirection.Down, wand.module.gestureVelocityLarge))
                .Repeatable()
                .Do(SlamEntity, "Slam Entity");
        });

    }

    public void HoverEntity() {
        MarkCasted();
        CollisionHandler handler = null;
        if (wand.target is Creature creature)
        {
            creature.ragdoll.SetState(Ragdoll.State.Destabilized);
            handler = creature.ragdoll.rootPart.collisionHandler;
        }

        wand.target.AddForce(Vector3.up * liftForceAmount, ForceMode.VelocityChange, handler);
        wand.target.Inflict("WandFloating", this, 10);
    }

    public void SlamEntity() {
        wand.target.Clear("WandFloating");
        Catalog.GetData<EffectData>("WandDescend").Spawn(wand.target.Center, Quaternion.identity).Play();
        wand.target.AddForce(Vector3.down * 24, ForceMode.VelocityChange);
        var target = wand.target;
        wand.target.OnNextCollision(instance => {
            wand.module.SpawnShockwave(instance.contactPoint + instance.contactNormal * 0.1f, instance.contactNormal);
            Utils.Explosion(instance.contactPoint, instance.impactVelocity.magnitude.Clamp(0, 20) / 2, 3.5f, true, affectPlayer: true);
            slamEffect.Spawn(instance.contactPoint, Quaternion.LookRotation(Vector3.right, instance.contactNormal)).Play();
            target.Inflict("WandFloating", (this, "shockwave"), 2);
        }, out _);
    }
}
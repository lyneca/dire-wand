using System;
using System.Collections;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Hover : WandModule {
    public EffectData slamEffect;
    public override void OnInit() {
        base.OnInit();
        slamEffect = Catalog.GetData<EffectData>("WandSlam");

        wand.OnTargetEntity(state => {
            var hover = wand.Offhand.Moving(Direction.Up).Palm(Direction.Up).Gripping;
            var slam = wand.Offhand.Moving(Direction.Down).Palm(Direction.Down).Gripping;

            state
                .ThenRepeatable(hover)
                .Do(HoverEntity, "Hover Entity");
            state
                .ThenRepeatable(slam)
                .Do(SlamEntity, "Slam Entity");
        });

    }

    public void HoverEntity() {
        MarkCasted();
        wand.target.Inflict<Floating>(this);
    }

    public void SlamEntity() {
        wand.target.Clear<Floating>();
        Catalog.GetData<EffectData>("WandDescend").Spawn(wand.target.WorldCenter, Quaternion.identity).Play();
        wand.target.Rigidbody().AddForce(Vector3.down * (8 * (wand.target.isCreature ? 30 : 3)),
            ForceMode.VelocityChange);
        var target = wand.target;
        wand.target.OnNextCollision(instance => {
            if (instance.impactVelocity.magnitude < 4) return;
            wand.module.SpawnShockwave(instance.contactPoint + instance.contactNormal * 0.1f, instance.contactNormal);
            Utils.Explosion(instance.contactPoint, instance.impactVelocity.magnitude.Clamp(0, 20) / 2, 3.5f, true, affectPlayer: true);
            slamEffect.Spawn(instance.contactPoint, Quaternion.identity).Play();
            target.Inflict<Floating>((this, "shockwave"), 2);
        }, 3);
    }
}
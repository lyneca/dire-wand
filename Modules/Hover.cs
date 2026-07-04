using System;
using System.Collections;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Hover : WandSkill {
    public EffectData slamEffect;
    public override void OnInit() {
        base.OnInit();
        slamEffect = Catalog.GetData<EffectData>("WandSlam");

        wand.OnTargetEntity(state => {
            var hover = wand.Offhand.Moving(Direction.Up).Palm(Direction.Up).Gripping;
            var slam = wand.Offhand.Moving(Direction.Down).Palm(Direction.Down).Gripping;

            state
                .ThenRepeatable(wand.Flick(AxisDirection.Up, wand.module.gestureVelocityLarge))
                .Do(HoverEntity, "Hover Entity");
            state
                .ThenRepeatable(wand.Flick(AxisDirection.Down, wand.module.gestureVelocityLarge))
                .Do(SlamEntity, "Slam Entity");
        });

    }

    public void HoverEntity() {
        MarkCasted();
        wand.target.Inflict("WandFloating", this, 10);
    }

    public void SlamEntity() {
        wand.target.Clear("WandFloating");
        Catalog.GetData<EffectData>("WandDescend").Spawn(wand.target.Center, Quaternion.identity).Play();
        wand.target.AddForce(Vector3.down * 24, ForceMode.VelocityChange);
        var target = wand.target;
        wand.target.OnNextCollision(instance => {
            if (instance.impactVelocity.magnitude < 4) return;
            wand.module.SpawnShockwave(instance.contactPoint + instance.contactNormal * 0.1f, instance.contactNormal);
            Utils.Explosion(instance.contactPoint, instance.impactVelocity.magnitude.Clamp(0, 20) / 2, 3.5f, true, affectPlayer: true);
            slamEffect.Spawn(instance.contactPoint, Quaternion.identity).Play();
            target.Inflict("WandFloating", (this, "shockwave"), 2);
        }, 3);
    }
}
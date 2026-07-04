using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Basic : WandSkill {
    public Color? startColor;
    public Color? endColor;
    public float damage = 10;
    private Gradient gradient;
    private EffectData fireEffectData;
    private EffectData hitEffectData;

    public override void OnInit() {
        base.OnInit();

        fireEffectData = Catalog.GetData<EffectData>("WandBasicCast");
        hitEffectData = Catalog.GetData<EffectData>("WandBasicHit");

        gradient = Utils.FadeInOutGradient(startColor ?? Utils.HexColor(191, 3, 0, 3.4f), endColor ?? Utils.HexColor(191, 5, 5, 4.6f));
        var colorStart = Utils.HexColor(191, 3, 0, 3.4f);
        var colorEnd = Utils.HexColor(191, 5, 5, 4.6f);
        Debug.Log($"Start: {colorStart.r}, {colorStart.g}, {colorStart.b}, {colorStart.a}");
        Debug.Log($"End: {colorEnd.r}, {colorEnd.g}, {colorEnd.b}, {colorEnd.a}");
        
        wand.button.Then(() => wand.tipViewVelocity.z > wand.module.gestureVelocityLarge
                               && !wand.localTipVelocity.MostlyZ(), "Slash Sideways")
            .Do(Fire, "Basic Cast");
    }

    public void Fire() {
        var direction = Vector3.Slerp(wand.tipVelocity.normalized, Player.local.head.transform.forward, 0.5f)
                        * (wand.tipVelocity.magnitude * 5);
        var target = wand.TargetEntity(new Ray(wand.tip.position, direction), WandBehaviour.LiveCreaturesAndItems, doEffect: false);
        if (target == null) return;
        
        MarkCasted();
        
        wand.item.Haptic(0.5f);
        var effect = fireEffectData.Spawn(wand.transform);
        effect.SetMainGradient(gradient);
        effect.SetSource(wand.tip);
        effect.SetTarget(wand.TargetTransform);
        effect.Play();
        var toTarget = target.Center - Player.currentCreature.transform.position;
        target.RunAfter(() => {
            var hitEffect = hitEffectData.Spawn(target.Center, Quaternion.LookRotation(-toTarget));
            hitEffect.SetMainGradient(gradient);
            hitEffect.Play();
            wand.item.Haptic(1);
            if (target is Creature creature) {
                if (!creature.isKilled) {
                    creature.Damage(new CollisionInstance(new DamageStruct(DamageType.Energy, damage) {
                        hitRagdollPart = creature.ragdoll.rootPart,
                        pushLevel = 1
                    }) {
                        casterHand = wand.item.mainHandler.caster,
                        contactPoint = creature.ragdoll.rootPart.transform.position,
                        intensity = 1,
                        impactVelocity = toTarget.normalized * 15
                    });
                }

                if (target.GetStatusOfType<Floating>() is not null) {
                    target.AddForce(toTarget.normalized * 60f, ForceMode.VelocityChange);
                    target.RootPhysicBody.AddTorque(Vector3.Cross(toTarget.normalized, Vector3.up) * 80f, ForceMode.VelocityChange);
                } else if (creature.ragdoll.state is Ragdoll.State.Destabilized or Ragdoll.State.Inert) {
                    target.AddForce(toTarget.normalized * 8f, ForceMode.VelocityChange);
                }
            } else {
                if (target is Item {breakable: Breakable breakable}) {
                    breakable.Break();
                }
                if (target.GetStatusOfType<Floating>() is not null) {
                    target.AddForce(toTarget.normalized * 10f, ForceMode.VelocityChange);
                    target.RootPhysicBody.AddTorque(Vector3.Cross(toTarget.normalized, Vector3.up) * 20f, ForceMode.VelocityChange);
                } else {
                    target.AddForce(toTarget.normalized * 3f, ForceMode.VelocityChange);
                }
            }
        }, 0.2f);
    }
}

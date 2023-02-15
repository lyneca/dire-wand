using System.Linq;
using System.Net.Security;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wand; 

public class Lift : WandModule {
    protected Rigidbody jointPoint;
    protected Joint joint;
    public string liftEffectId = "WandLift";
    public EffectData liftEffectData;

    public override void OnInit() {
        base.OnInit();
        liftEffectData = Catalog.GetData<EffectData>(liftEffectId);
        wand.targetedItem
            .Then(wand.Flick(AxisDirection.Up, wand.module.gestureVelocityNormal))
            .Do(LiftEntity, "Lift Item")
            .Then("Tap button", () => wand.Buttoning)
            .Repeatable()
            .Do(() => Cantrip.Boop(wand.target.item, wand), "Boop")
            .Then("Release button", () => !wand.Buttoning)
            .Do(() => Cantrip.UnBoop(wand.target.item, wand), "Un-boop");
        wand.targetedEnemy
            .Then(wand.Flick(AxisDirection.Up, wand.module.gestureVelocityLarge))
            .Do(LiftEntity, "Lift Creature");
        jointPoint = new GameObject().AddComponent<Rigidbody>();
        jointPoint.useGravity = false;
        jointPoint.isKinematic = true;
    }

    public override void OnUpdate() {
        base.OnUpdate();
        jointPoint.transform.position = wand.tip.transform.position
                                        + wand.tip.transform.forward * 2
                                        + wand.tipVelocity.normalized
                                        * Mathf.Clamp(wand.tipVelocity.magnitude, 0.0f, 8f)
                                        / 5f;
        jointPoint.transform.rotation = Quaternion.Slerp(jointPoint.transform.rotation,
            wand.tip.transform.rotation * Quaternion.AngleAxis(180, Vector3.forward),
            Time.deltaTime * 5);

        if (wand.target?.item is Item targetItem
            && wand.otherHand.Casting()
            && wand.otherHand.caster?.spellInstance is SpellCastCharge { imbueEnabled: true } spell
            && (wand.otherHand.grip.position - wand.tip.position).sqrMagnitude < 0.15f * 0.15f) {
            for (var i = 0; i < item.colliderGroups.Count; i++) {
                targetItem.colliderGroups[i].imbue
                    .Transfer(spell, spell.imbueRate * spell.currentCharge * Time.deltaTime);
            }
        }

        if (joint) {
            if (wand.target?.handler?.item?.isFlying == false)
                wand.target.handler.item.Throw(1, Item.FlyDetection.Forced);
            wand.target?.handler?.item?.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
        }
    }

    public override void OnReset() {
        base.OnReset();
        wand.PlaySound(SoundType.Ragh);
        Object.Destroy(joint);
    }

    public void LiftEntity() {
        if (wand.target == null) {
            wand.Reset();
            return;
        }
        
        MarkCasted();

        var line = Catalog.GetData<EffectData>("WandLineLoop").Spawn(wand.tip);
        line.SetSource(wand.tip);
        line.SetTarget(wand.target.Transform);
        line.SetMainGradient(wand.module.targetArgs.gradient);
        line.Play();
            
        wand.PlaySound(SoundType.Legh, wand.target.Transform);
            
        wand.target.item?.gameObject.GetComponent<FreezeModifier>()?.Clear();

        wand.target.Release();
        wand.target.Grab(true,
            obj => wand.item.Haptic(obj.Rigidbody().velocity.magnitude.RemapClamp(0, 20f, 0, 0.5f)),
            _ => line.Despawn());

        float modifier = Mathf.Sqrt(wand.target.Rigidbody().mass);
        jointPoint.transform.position = wand.target.Center();
        joint = Utils.CreateSimpleJoint(jointPoint, wand.target.Rigidbody(), 1000 * modifier, 150 * modifier,
            100000f * modifier,
            targetRotation: wand.target.item?.flyDirRef?.transform.rotation
                            ?? wand.target.item?.colliderGroups.FirstOrDefault()?.imbueShoot?.transform.rotation
                            ?? wand.target.Rigidbody().transform.rotation);
        joint.connectedAnchor = wand.target.Rigidbody().centerOfMass;
        wand.target.throwOnRelease = true;
        liftEffectData.Spawn(wand.target.Transform).Play();
    }
}
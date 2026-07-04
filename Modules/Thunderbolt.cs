using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;
using Microsoft.CSharp.RuntimeBinder;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Wand; 

public class Thunderbolt : WandSkill {
    private EffectData boltEffectData;
    private EffectData chargeEffectData;

    private EffectInstance chargeEffect;

    private Transform chargePoint;
    public float startTime;
    private EffectInstance spiral;

    public bool charging;
    private Transform targetPoint;
    private EffectInstance sparks;

    public override void OnInit() {
        base.OnInit();
        boltEffectData = Catalog.GetData<EffectData>("WandThunderbolt");
        chargeEffectData = Catalog.GetData<EffectData>("WandStorm");
        
        wand.button
            .Then("Facing up", () => wand.tipRay.direction.IsFacing(Vector3.up, 20))
            .Do(Sparks)
            .Then(wand.Swirl(SwirlDirection.Clockwise, 2))
            .Do(LightningCharge)
            .Then(() => wand.tipVelocity.y < -wand.module.gestureVelocityNormal)
            .Do(LightningStrike);
    }

    public bool ChargePos(out Vector3 position) {
        Utils.FindRoof(Player.local.head.transform.position, 8, out var roof);
        var ray = new Ray(wand.tip.position,
            Vector3.Angle(wand.tipRay.direction, Vector3.up) > 75
                ? Quaternion.AngleAxis(75, Vector3.Cross(Vector3.up, wand.tipRay.direction)) * Vector3.up
                : wand.tipRay.direction);
        

        if (new UnityEngine.Plane(Vector3.down, roof).Raycast(ray, out float enter)) {
            position = ray.GetPoint(enter) - Vector3.up * 0.4f;
            // if (wand.debug)
            //     Viz.Plane("roof").Center(position).Size(3).Normal(Vector3.down).Lines.Color(Color.red);
            
            if (!Utils.FindFloor(position, 5, out var floor)) return true;
            
            // if (wand.debug)
            //     Viz.Plane("floor").Center(floor).Normal(Vector3.down).Size(2).Lines.Color(Color.blue);
            if (Utils.FindRoof(floor, 8, out var floorRoof)) {
                position = floorRoof - Vector3.up * 0.4f;
            } else {
                position = floor + Vector3.up * 8;
            }
            // if (wand.debug)
            //     Viz.Plane("position").Center(position).Normal(Vector3.down).Size(1).Lines.Color(Color.green);

            return true;
        }

        position = roof;
        return false;
    }

    public void Sparks() {
        sparks?.End();
        sparks?.Despawn();
        sparks = Catalog.GetData<EffectData>("WandStormSparks").Spawn(wand.tip);
        sparks.Play();
    }

    public void LightningCharge() {
        charging = true;
        items = new HashSet<Item>();
        chargePoint ??= new GameObject().transform;
        targetPoint ??= new GameObject().transform;
        ChargePos(out var position);
        chargePoint.transform.position = position;

        startTime = Time.time;
        
        spiral?.Despawn();
        spiral = Catalog.GetData<EffectData>("WandLightningSpiral").Spawn(targetPoint);
        spiral.Play();
        
        chargeEffect?.Despawn();
        chargeEffect = chargeEffectData.Spawn(chargePoint);
        chargeEffect.SetIntensity(0);
        chargeEffect.Play();
    }

    private HashSet<Item> items;
    private float lastZap;

    public override void OnUpdate() {
        base.OnUpdate();
        if (!charging) return;

        chargePoint.transform.position = ChargePos(out var position)
            ? Vector3.Lerp(chargePoint.transform.position, position, Time.deltaTime * 1f)
            : chargePoint.transform.position;
        Utils.FindFloor(chargePoint.position, 50, out var floor);
        targetPoint.transform.position = floor;
        chargeEffect.SetIntensity((Time.time - startTime).RemapClamp01(0, 4));

        var newItems = Utils.AllItemsInRadius(targetPoint.position, 3, true).ToHashSet();
        foreach (var eachItem in items.Where(item => !newItems.Contains(item))) {
            eachItem.Remove("Floating", this);
        }
        
        if (Time.time - lastZap > 0.2f) {
            lastZap = Time.time + Random.Range(0f, 0.3f);
            var itemToZap = items.Where(item => item.colliderGroups.Any(group => group.isMetal)).RandomChoice();
            if (itemToZap != null) {
                var group = itemToZap.colliderGroups.FirstOrDefault(group => group.isMetal);
                if (group?.imbue != null) {
                    group.imbue.Transfer(Catalog.GetData<SpellCastLightning>("Lightning"), 50);
                    var bolt = Catalog.GetData<EffectData>("SpellLightningBolt").Spawn(Vector3.zero, Quaternion.identity);
                    bolt.SetSource(targetPoint);
                    bolt.SetTarget(group.transform);
                    bolt.Play();
                    bolt.onEffectFinished += effect => effect.Despawn();
                }
            }
        }


        items = newItems;
        foreach (var eachItem in items) {
            eachItem.Inflict("Floating", this, playEffect: false);
        }
    }

    public void LightningStrike() {
        MarkCasted();
        charging = false;
        chargeEffect.onEffectFinished += instance => instance.Despawn();
        chargeEffect.End();
        spiral.onEffectFinished += instance => instance.Despawn();
        spiral.End();
        Utils.FindFloor(chargePoint.position, 50, out var targetPos);
        targetPoint.transform.position = targetPos;
        sparks.End();
        sparks.Despawn();
        
        foreach (var eachItem in items) {
            eachItem.Remove("Floating", this);
        }
        
        items = Utils.AllItemsInRadius(targetPoint.position, 4, true).ToHashSet();

        foreach (var toPush in items) {
            toPush.physicBody.rigidBody.AddExplosionForce(4, targetPos, 5, 1, ForceMode.VelocityChange);
        }

        float angle = Mathf.Infinity;
        ThunderEntity target = null;
        foreach (var creature in Utils.CreaturesInRadius(targetPos,
                     (Vector3.Distance(targetPos, chargePoint.position) / 2).Clamp(5, Mathf.Infinity), false, true)) {
            var entity = creature.gameObject.GetOrAddComponent<ThunderEntity>();
            if (Vector3.Angle(wand.tipVelocity, entity.Center - chargePoint.position) < angle) target = entity;
        }

        foreach (var creature in Utils.CreaturesInRadius(target?.Center ?? targetPos, 4)) {
            var entity = creature.gameObject.GetOrAddComponent<ThunderEntity>();
            float distance = Vector3.Distance(target?.Center ?? targetPos, entity.Center);
            if (!creature.isKilled) {
                creature.Damage(
                    new CollisionInstance(new DamageStruct(DamageType.Energy, 60 * distance.RemapClamp01(4, 0))));
            }

            creature.TryElectrocute(1, 5, true, false,
                Catalog.GetData<SpellCastLightning>("Lightning").imbueHitRagdollEffectData);
        }

        Utils.FindFloor(target?.Center ?? targetPos, 1, out var floor);
        Catalog.GetData<EffectData>("WandThunderboltHit")
            .Spawn(target != null ? floor : targetPoint.position,
                Quaternion.LookRotation(Vector3.up)).Play();

        var boltEffect = boltEffectData.Spawn(Vector3.Lerp(chargePoint.position, targetPos, 0.5f), Quaternion.identity);
        boltEffect.SetSource(chargePoint);
        boltEffect.SetTarget(target?.RootTransform ?? targetPoint);
        boltEffect.Play();
        if (target is Creature hitCreature) {
            hitCreature.Kill();
            hitCreature.ragdoll.SliceAll();
            for (var i = 0; i < hitCreature.ragdoll.parts.Count; i++) {
                hitCreature.ragdoll.parts[i].physicBody.AddForce((Vector3.up * 2 + Random.onUnitSphere * 3) * 3,
                    ForceMode.VelocityChange);
            }
        }
    }

    public override void OnReset() {
        base.OnReset();
        sparks?.End();
        sparks?.Despawn();
        sparks = null;
        
        if (spiral != null) {
            spiral.End();
            spiral.onEffectFinished += effect => effect.Despawn();
            spiral = null;
        }

        if (chargeEffect != null) {
            chargeEffect.End();
            chargeEffect.onEffectFinished += effect => effect.Despawn();
            chargeEffect = null;
        }
        charging = false;
    }
}
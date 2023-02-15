using ExtensionMethods;
using Microsoft.CSharp.RuntimeBinder;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Thunderbolt : WandModule {
    private EffectData boltEffectData;
    private EffectData chargeEffectData;

    private EffectInstance chargeEffect;

    private Transform chargePoint;
    public float startTime;
    private EffectInstance spiral;

    public bool charging;
    private Transform targetPoint;

    public override void OnInit() {
        base.OnInit();
        boltEffectData = Catalog.GetData<EffectData>("WandThunderbolt");
        chargeEffectData = Catalog.GetData<EffectData>("WandStorm");
        wand.button
            .Then("Facing up", () => wand.tipRay.direction.IsFacing(Vector3.up, 20))
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

        if (new Plane(Vector3.down, roof).Raycast(ray, out float enter)) {
            position = ray.GetPoint(enter) - Vector3.up * 0.1f;
            return true;
        }

        position = roof;
        return false;
    }


    public void LightningCharge() {
        charging = true;
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

    public override void OnUpdate() {
        base.OnUpdate();
        if (!charging) return;
        
        var floor = Utils.FindFloor(chargePoint.position, 50);

        chargePoint.transform.position = ChargePos(out var position)
            ? Vector3.Lerp(chargePoint.transform.position, position, Time.deltaTime * 1f)
            : chargePoint.transform.position;
        targetPoint.transform.position = Utils.FindFloor(chargePoint.position, 50);
        chargeEffect.SetIntensity((Time.time - startTime).RemapClamp01(0, 4));
    }

    public void LightningStrike() {
        charging = false;
        chargeEffect.Stop();
        spiral.Stop();
        var targetPos = Utils.FindFloor(chargePoint.position, 50);
        targetPoint.transform.position = targetPos;

        float angle = Mathf.Infinity;
        Entity target = null;
        foreach (var creature in Utils.CreaturesInRadius(targetPos,
                     (Vector3.Distance(targetPos, chargePoint.position) / 2).Clamp(5, Mathf.Infinity), false, true)) {
            var entity = creature.gameObject.GetOrAddComponent<Entity>();
            if (Vector3.Angle(wand.tipVelocity, entity.WorldCenter - chargePoint.position) < angle) target = entity;
        }

        foreach (var creature in Utils.CreaturesInRadius(target?.WorldCenter ?? targetPos, 4)) {
            var entity = creature.gameObject.GetOrAddComponent<Entity>();
            float distance = Vector3.Distance(target?.WorldCenter ?? targetPos, entity.WorldCenter);
            if (!creature.isKilled) {
                creature.Damage(
                    new CollisionInstance(new DamageStruct(DamageType.Energy, 60 * distance.RemapClamp01(4, 0))));
            }

            creature.TryElectrocute(1, 5, true, false,
                Catalog.GetData<SpellCastLightning>("Lightning").imbueHitRagdollEffectData);
        }
        
        Catalog.GetData<EffectData>("WandThunderboltHit")
            .Spawn(target != null ? Utils.FindFloor(target.WorldCenter, 1) : targetPoint.position,
                Quaternion.LookRotation(Vector3.up)).Play();

        var boltEffect = boltEffectData.Spawn(Vector3.Lerp(chargePoint.position, targetPos, 0.5f), Quaternion.identity);
        boltEffect.SetSource(chargePoint);
        boltEffect.SetTarget(target?.Transform ?? targetPoint);
        boltEffect.Play();
        if (target) {
            target.creature.Kill();
            target.creature.ragdoll.SliceAll();
            for (var i = 0; i < target.creature.ragdoll.parts.Count; i++) {
                target.creature.ragdoll.parts[i].rb.AddForce((Vector3.up * 2 + Random.onUnitSphere * 3) * 3,
                    ForceMode.VelocityChange);
            }
        }
    }

    public override void OnReset() {
        base.OnReset();
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;
using ThunderRoad;
using ThunderRoad.DebugViz;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wand; 

public class ProfaneSlash : WandSkill {
    public Damager damager;

    public override void Register() {
        base.Register();
        wand.profane
            .Then(() => wand.Buttoning)
            .Do(() => (wand.target as Creature)?.ragdoll.forcePhysic.Add(this))
            .ThenRepeatable("Slash Sideways", 0.01f,
                () => wand.tipViewVelocity.magnitude > wand.module.gestureVelocityLarge
                      && !wand.tipViewVelocity.MostlyZ() && !wand.localTipVelocity.MostlyZ())
            .Do(Slice)
            .Then(() => wand.tipViewVelocity.magnitude < wand.module.gestureVelocitySmall);
        damager = new GameObject().AddComponent<Damager>();
        damager.data = Catalog.GetData<DamagerData>("AxeSlashMassive");
    }

    public override void OnReset()
    {
        base.OnReset();
        (wand.target as Creature)?.ragdoll.forcePhysic.Remove(this);
    }

    public void Slice()
    {
        if (wand.target is not Creature creature) return;

        var part = new List<RagdollPart> { creature.ragdoll.targetPart, creature.ragdoll.targetPart.parentPart }.RandomChoice();

        var center = creature.ragdoll.targetPart.transform.position;
        var startSlicePos = center - (center - wand.tip.position).normalized + Random.insideUnitSphere;
        var collider = part.colliderGroup.colliders.RandomChoice();

        var point = collider.ClosestPoint(startSlicePos);

        damager.transform.position = point;
        damager.transform.rotation = Quaternion.LookRotation(point - startSlicePos, wand.tipVelocity);
        var damageStruct = new DamageStruct(DamageType.Slash, 2)
        {
            damager = damager,
            damageType = DamageType.Slash
        };

        var collision = new CollisionInstance
        {
            damageStruct = damageStruct,
            targetMaterial = Catalog.GetData<MaterialData>("Blade"),
            sourceMaterial = MaterialData.GetMaterial(collider),
            contactPoint = point,
            contactNormal = startSlicePos - point,
            targetCollider = collider,
            targetColliderGroup = collider.GetComponentInParent<ColliderGroup>(),
            impactVelocity = point - startSlicePos,
            intensity = 1
        };

        if (collision.SpawnEffect(Catalog.GetData<MaterialData>("Blade"),
                MaterialData.GetMaterial(collider), false, out EffectInstance effect))
        {
            effect.source = creature;
            effect.Play();
        }
        else
        {
            Debug.Log("Couldn't spawn effect");
        }
        creature.Damage(
            new CollisionInstance(new DamageStruct(DamageType.Energy, 10) { damager = damager }));
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wand; 

public class Sectumsempra : WandModule {
    public Args effectArgs = new Args() {
        gradient = Utils.FadeInOutGradient(
            Utils.HexColor(150, 10, 10, 6),
            Utils.HexColor(0, 0, 0, 6))
    };

    public Damager damager;

    public override void OnInit() {
        base.OnInit();
        wand.trigger
            .Then(wand.Flick(AxisDirection.Right),
                wand.Flick(AxisDirection.Left),
                wand.Brandish())
            .Do(() => {
                Entity entity = wand.TargetCreature(effectArgs);
                wand.RunAfter(() => BleedEnemy(entity.creature), 0.3f);
            }, "Sectumsempra");
        damager = wand.objectPool.Get().AddComponent<Damager>();
        damager.data = Catalog.GetData<DamagerData>("SwordSlash2H");
    }

    public void BleedEnemy(Creature creature) {
        MarkCasted();
        wand.StartCoroutine(Bleed(creature));
    }

    public IEnumerator Bleed(Creature creature) {
        creature.ragdoll.AddPhysicToggleModifier(this);
        while (!creature.isKilled) {
            try {
                Slice(creature);
            } catch (Exception) { }

            yield return new WaitForSeconds(0.5f);
        }
    }

    public void Slice(Creature creature) {
        Vector3 center = creature.GetTorso().transform.position;
        Vector3 startSlicePos = center + Random.insideUnitSphere * 1.5f;
        var hits = Physics.SphereCastAll(startSlicePos, 0.4f, center - startSlicePos, 3,
            LayerMask.GetMask(LayerName.Ragdoll.ToString(), LayerName.NPC.ToString()),
            QueryTriggerInteraction.Ignore);
        for (var index = 0; index < hits.Length; index++) {
            var hit = hits[index];
            if (hit.collider.GetComponentInParent<Creature>() == creature) {
                damager.transform.position = hit.point;
                damager.transform.rotation = Quaternion.LookRotation(-hit.normal, Random.onUnitSphere);
                var damageStruct = new DamageStruct(DamageType.Slash, 2) {
                    damager = damager,
                    damageType = DamageType.Slash,
                };

                var collision = new CollisionInstance {
                    damageStruct = damageStruct,
                    targetMaterial = Catalog.GetData<MaterialData>("Blade"),
                    sourceMaterial = MaterialData.GetMaterial(hit.collider),
                    contactPoint = hit.point,
                    contactNormal = hit.normal,
                    targetCollider = hit.collider,
                    targetColliderGroup = hit.collider.GetComponentInParent<ColliderGroup>(),
                    impactVelocity = hit.point - startSlicePos,
                    intensity = 1
                };
                if (collision.SpawnEffect(Catalog.GetData<MaterialData>("Blade"),
                        MaterialData.GetMaterial(hit.collider), false, out EffectInstance effect)) {
                    effect.source = creature;
                    effect.Play();
                } else {
                    Debug.Log("Couldn't spawn effect");
                }

                //collision.NewHit(null, hit.collider, null, hit.collider.GetComponentInParent<ColliderGroup>(),
                //    hit.point - startSlicePos,
                //    hit.point, hit.normal, 1, Catalog.GetData<MaterialData>("Blade"),
                //    MaterialData.GetMaterial(hit.collider));
                creature.Damage(
                    new CollisionInstance(new DamageStruct(DamageType.Energy, 10) { damager = damager }));
                return;
            }
        }
    }
}
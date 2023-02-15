using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Detonate : WandModule {
    public static ItemData projectileData;
    public static DamagerData damagerData;
    public static EffectData fireballEffectData;
    public override void OnInit() {
        base.OnInit();
        projectileData = Catalog.GetData<ItemData>("DynamicProjectile");
        damagerData = Catalog.GetData<DamagerData>("Fireball");
        fireballEffectData = Catalog.GetData<EffectData>("SpellFireball");
        wand.button
            .Then(wand.Swirl(SwirlDirection.CounterClockwise))
            .Then(wand.Brandish())
            .Do(() => FireExplosive(true));
    }

    public void FireExplosive(bool drag) {
        MarkCasted();
        projectileData.SpawnAsync(projectile => {
            projectile.transform.SetPositionAndRotation(wand.tip.position, wand.tip.rotation);
            for (var index = 0; index < projectile.collisionHandlers.Count; index++) {
                var collisionHandler = projectile.collisionHandlers[index];
                for (var i = 0; i < collisionHandler.damagers.Count; i++) {
                    var damager = collisionHandler.damagers[i];
                    damager.Load(damagerData, collisionHandler);
                }
            }

            projectile.rb.useGravity = false;

            var component = projectile.GetComponent<ItemMagicProjectile>();
            component.OnProjectileCollisionEvent += Explosion;
            component.item.OnDespawnEvent += _ => component.OnProjectileCollisionEvent -= Explosion;
            component.guidance = GuidanceMode.NonGuided;
            component.homing = false;
            component.item.lastHandler = wand.item.lastHandler;
            component.allowDeflect = false;
            component.imbueEnergyTransfered = 10;
            component.Fire(wand.tipRay.direction * 40, fireballEffectData, wand.item);
            component.transform.localScale = Vector3.one * 0.2f;
            foreach (var effect in component.GetField<EffectInstance>("effectInstance").effects) {
                if (effect is not EffectMesh mesh || mesh.currentMainGradient == null) continue;
                var gradient = new Gradient();
                gradient.SetKeys(
                    mesh.currentMainGradient.colorKeys.Select(key => new GradientColorKey(key.color * 5, key.time))
                        .ToArray(), mesh.currentMainGradient.alphaKeys);
                mesh.SetMainGradient(gradient);
            }
            if (drag)
                component.StartCoroutine(FlightRoutine(component));
        });
    }

    public IEnumerator FlightRoutine(ItemMagicProjectile component) {
        yield return new WaitForSeconds(0.25f);
        if (!component.alive) yield break;
        yield return Utils.LoopOver(value => {
            component.item.rb.drag = 30 * value;
            component.transform.localScale = Vector3.one * value.Remap(0, 1, 1, 0.0001f);
        }, 0.5f);
        if (!component.alive) yield break;
        component.item.mainCollisionHandler.SetRigidbody(1, 0f, 0);
        component.transform.localScale = Vector3.one;
        yield return new WaitForSeconds(0.25f);
        if (!component.alive) yield break;
        Explosion(component.transform.position, Vector3.up);
        component.Despawn();
    }

    public void Explosion(ItemMagicProjectile projectile, CollisionInstance collision) {
        Explosion(collision.contactPoint, collision.contactNormal);
        projectile.OnProjectileCollisionEvent -= Explosion;
    }

    public void Explosion(Vector3 position, Vector3 normal, float radius = 10) {
        Catalog.GetData<EffectData>("MeteorExplosion")
            .Spawn(position, Quaternion.LookRotation(Vector3.forward, normal))
            .Play();
        var rigidbodySet = new HashSet<Rigidbody>();
        var hitCreatures = new HashSet<Creature>();
        foreach (var collider in Physics.OverlapSphere(position, radius,
                     Utils.GetMask(LayerName.Default, LayerName.ItemAndRagdollOnly, LayerName.MovingItem,
                         LayerName.DroppedItem, LayerName.Ragdoll, LayerName.PlayerLocomotion, LayerName.BodyLocomotion,
                         LayerName.NPC),
                     QueryTriggerInteraction.Ignore)) {
            if (collider.attachedRigidbody && !rigidbodySet.Contains(collider.attachedRigidbody)) {
                float explosionForce = 20;
                Creature componentInParent = collider.attachedRigidbody.GetComponentInParent<Creature>();
                if (componentInParent != null
                    && !componentInParent.isKilled
                    && !componentInParent.isPlayer
                    && !hitCreatures.Contains(componentInParent)) {
                    componentInParent.ragdoll.SetState(Ragdoll.State.Destabilized);
                    hitCreatures.Add(componentInParent);
                }

                if (collider.attachedRigidbody.GetComponentInParent<Player>() != null)
                    explosionForce = 15;
                rigidbodySet.Add(collider.attachedRigidbody);
                collider.attachedRigidbody.AddExplosionForce(explosionForce, position, radius, 1f,
                    ForceMode.VelocityChange);
            }
        }
    }
}
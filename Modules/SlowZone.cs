using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Wand {
    public class SlowZone : WandModule {
        public string bubbleEffectId = "WandBubble";
        private EffectData bubbleEffectData;
        public string bubbleEnterEffectId = "WandBubbleEnter";
        private EffectData bubbleEnterEffectData;
        public AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float radius = 5;
        private HashSet<SlowCreatureModifier> creatures;
        private HashSet<Rigidbody> rigidbodies;

        public override WandModule Clone() {
            var clone = base.Clone() as SlowZone;
            clone.creatures = new HashSet<SlowCreatureModifier>();
            clone.rigidbodies = new HashSet<Rigidbody>();
            return clone;
        }

        public override void OnInit() {
            base.OnInit();
            EventManager.onItemDespawn += item => item.ClearPhysicModifiers();
            bubbleEffectData = Catalog.GetData<EffectData>(bubbleEffectId);
            bubbleEnterEffectData = Catalog.GetData<EffectData>(bubbleEnterEffectId);
            wand.button
                .Then(() => wand.item.isThrowed)
                .Then(() => wand.item.mainCollisionHandler.isColliding)
                .Do(Collision);
        }

        public void Collision() { wand.StartCoroutine(SlowRoutine(wand.item.mainCollisionHandler.collisions[0])); }

        public IEnumerator SlowRoutine(CollisionInstance collision) {
            var trigger = new GameObject().AddComponent<Trigger>();
            trigger.transform.position = wand.transform.position;
            trigger.SetRadius(0);
            trigger.SetActive(true);
            creatures = new HashSet<SlowCreatureModifier>();
            rigidbodies = new HashSet<Rigidbody>();
            trigger.SetCallback(Slow);
            trigger.SetLayer(GameManager.GetLayer(LayerName.ItemAndRagdollOnly));
            var effect = bubbleEffectData.Spawn(collision.contactPoint, Quaternion.identity);
            SetSizeCurve(AnimationCurve.EaseInOut(0, 0, 1, radius * 2), effect);

            float currentRadius = 0;
            var tracker = new StateTracker()
                .Toggle(
                    () => Vector3.Distance(Player.local.head.transform.position, collision.contactPoint)
                          < currentRadius,
                    () => {
                        bubbleEnterEffectData.Spawn(item.transform).Play();
                        GameManager.audioMixerSnapshotUnderwater.TransitionTo(0.0f);
                    },
                    () => GameManager.audioMixerSnapshotDefault.TransitionTo(0.0f));

            foreach (var effectMesh in effect.effects.OfType<EffectMesh>())
                effectMesh.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            effect.SetIntensity(0);
            effect.Play();
            wand.item.rb.isKinematic = true;
            wand.item.disallowDespawn = true;
            var returnBehaviour = wand.item.gameObject.GetComponent<ItemAlwaysReturnsInInventory>();
            if (returnBehaviour) {
                returnBehaviour.SetField("active", false);
            }

            yield return Utils.LoopOver(time => {
                float amount = radiusCurve.Evaluate(time);
                effect.SetIntensity(amount);
                currentRadius = amount;
                wand.transform.SetPositionAndRotation(
                    Vector3.Lerp(wand.transform.position, collision.contactPoint + collision.contactNormal * 0.4f,
                        amount),
                    Quaternion.Slerp(wand.transform.rotation, Quaternion.LookRotation(-collision.contactNormal),
                        amount));
                tracker.Update();
                trigger.SetRadius(amount * radius);
            }, 1);

            float startTime = Time.time;
            currentRadius = radius;
            while (true) {
                wand.transform.rotation = Quaternion.Slerp(wand.transform.rotation,
                    Quaternion.LookRotation(-collision.contactNormal,
                        Quaternion.AngleAxis((Time.time - startTime).Remap(0, 1, 0, 360), -collision.contactNormal)
                        * Vector3.Cross(-collision.contactNormal, Vector3.forward)), Time.deltaTime * 10);
                tracker.Update();

                if (wand.item.mainHandler != null || wand.item.isTelekinesisGrabbed) {
                    break;
                }

                yield return 0;
            }


            GameManager.local.StartCoroutine(Utils.LoopOver(time => effect.SetIntensity(radiusCurve.Evaluate(1 - time)),
                0.3f, () => effect.End()));
            GameManager.audioMixerSnapshotDefault.TransitionTo(0.0f);

            wand.item.rb.isKinematic = false;
            if (returnBehaviour) {
                returnBehaviour.SetField("active", true);
            }

            foreach (var creature in creatures) {
                if (creature == null) continue;
                creature.gameObject.GetComponent<SlowCreatureModifier>().RemoveHandler(this);
            }

            Object.Destroy(trigger.gameObject);

            foreach (var rigidbody in rigidbodies) {
                rigidbody?.GetComponent<RigidbodyModifier>()?.RemoveModifier(this);
            }
        }
        
        public void SetSizeCurve(AnimationCurve curve, EffectInstance effectInstance) {
            foreach (var effect in effectInstance.effects.NotNull()) {
                if (effect is EffectMesh mesh) {
                    mesh.curveMeshSize = curve;
                } else if (effect is EffectParticle particle) {
                    if (particle.scaleCurve != null)
                        particle.scaleCurve = curve;
                } else if (effect is EffectVfx vfx) {
                    vfx.scaleCurve = curve;
                }
            }
        }

        public void Slow(Collider collider, bool enter) {
            if (collider.attachedRigidbody is not Rigidbody rb) return;
            if (rb.GetComponentInParent<Player>()
                || rb.GetComponentInParent<Item>() is Item item
                && (item.mainHandler?.creature?.isPlayer == true || item == wand.item))
                return;

            if (enter) {
                if (rigidbodies.Contains(rb)) return;
                rigidbodies.Add(rb);
                Creature hitCreature = null;
                if (rb.GetComponent<Creature>() is Creature creature) {
                    hitCreature = creature;
                } else if (rb.GetComponent<RagdollPart>() is RagdollPart part && part == part.ragdoll.rootPart) {
                    hitCreature = part.ragdoll.creature;
                }

                if (hitCreature != null) {
                    var modifier = hitCreature.gameObject.GetOrAddComponent<SlowCreatureModifier>();
                    modifier.AddHandler(this);
                    creatures.Add(modifier);
                }

                rb.AddModifier(this, 3, 0, 10);
            } else {
                if (!rigidbodies.Contains(rb)) return;
                rigidbodies.Remove(rb);
                if (rb.GetComponent<Creature>() is Creature creature) {
                    var modifier = creature.gameObject.GetOrAddComponent<SlowCreatureModifier>();
                    modifier.RemoveHandler(this);
                    creatures.Remove(modifier);
                }

                rb.RemoveModifier(this);
            }
        }
    }

    public class SlowCreatureModifier : CreatureModifier {
        public override void OnApply() {
            base.OnApply();
            creature.ragdoll.AddPhysicToggleModifier(this);
            creature.animator.speed = 0.1f;
            creature.locomotion.SetSpeedModifier(this, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f);
        }

        public override void OnRemove() {
            base.OnRemove();
            creature.ragdoll.RemovePhysicToggleModifier(this);
            creature.animator.speed = 1;
            creature.locomotion.RemoveSpeedModifier(this);
        }
    }
}
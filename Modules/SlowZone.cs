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

namespace Wand; 

public class SlowZone : WandSkill {
    public string bubbleEffectId = "WandBubble";
    private EffectData bubbleEffectData;
    public string bubbleEnterEffectId = "WandBubbleEnter";
    private EffectData bubbleEnterEffectData;
    public AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float radius = 5;
    private HashSet<Creature> creatures;
    private HashSet<Rigidbody> rigidbodies;

    public override CatalogData Clone() {
        var clone = base.Clone() as SlowZone;
        clone!.creatures = [];
        clone.rigidbodies = [];
        return clone;
    }

    public override void OnInit() {
        base.OnInit();
        Item.OnItemDespawn += item => item.ClearPhysicModifiers();
        bubbleEffectData = Catalog.GetData<EffectData>(bubbleEffectId);
        bubbleEnterEffectData = Catalog.GetData<EffectData>(bubbleEnterEffectId);
        
        wand.trigger
            .Then(() => wand.item.isThrowed)
            .Then(() => wand.item.mainCollisionHandler.isColliding)
            .Do(Collision);
    }

    public void Collision() { wand.StartCoroutine(SlowRoutine(wand.item.mainCollisionHandler.collisions[0])); }

    public IEnumerator SlowRoutine(CollisionInstance collision) {
        MarkCasted();
        var trigger = new GameObject().AddComponent<Trigger>();
        trigger.transform.position = wand.transform.position;
        trigger.SetRadius(0);
        trigger.SetActive(true);
        creatures = new HashSet<Creature>();
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
                    bubbleEnterEffectData.Spawn(wandItem.transform).Play();
                    SnapshotTool.DoSnapshotTransition(ThunderRoadSettings.audioMixerSnapshotUnderwater, 0);
                },
                () => SnapshotTool.DoSnapshotTransition(ThunderRoadSettings.audioMixerSnapshotDefault, 0));

        foreach (var effectMesh in effect.effects.OfType<EffectMesh>())
            effectMesh.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

        effect.SetIntensity(0);
        effect.Play();
        wand.item.physicBody.isKinematic = true;
        wand.item.DisallowDespawn = true;
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
        SnapshotTool.DoSnapshotTransition(ThunderRoadSettings.audioMixerSnapshotDefault, 0);

        wand.item.physicBody.isKinematic = false;
        if (returnBehaviour) {
            returnBehaviour.SetField("active", true);
        }

        foreach (var creature in creatures) {
            if (creature == null) continue;
            creature.Remove("Slowed", this);
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
            || rb.GetComponentInParent<Item>() is Item rbItem
            && (rbItem.mainHandler?.creature?.isPlayer == true || rbItem == wand.item))
            return;

        if (enter) {
            if (rigidbodies.Contains(rb)) return;
            rigidbodies.Add(rb);
            Creature hitCreature = null;
            if (rb.GetComponentInParent<Creature>() is Creature creature) {
                hitCreature = creature;
            } else if (rb.GetComponent<RagdollPart>() is RagdollPart part && part == part.ragdoll.rootPart) {
                hitCreature = part.ragdoll.creature;
            }

            if (hitCreature != null) {
                hitCreature.Inflict("Slowed", this);
                creatures.Add(hitCreature);
            }

            rb.AddModifier(this, 3, 0, 10);
        } else {
            if (!rigidbodies.Contains(rb)) return;
            rigidbodies.Remove(rb);
            if (rb.GetComponent<Creature>() is Creature creature) {
                creature.Remove("Slowed", this);
                creatures.Remove(creature);
            }

            rb.RemoveModifier(this);
        }
    }
}
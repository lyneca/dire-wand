using System;
using System.Collections;
using System.Linq;
using ExtensionMethods;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using ThunderRoad.Skill.SpellPower;
using UnityEngine;
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

    public override CatalogData Clone() {
        var clone = base.Clone() as SlowZone;
        return clone;
    }

    public override void Register() {
        base.Register();
        Item.OnItemDespawn += item => item.ClearPhysicModifiers();
        bubbleEffectData = Catalog.GetData<EffectData>(bubbleEffectId);
        bubbleEnterEffectData = Catalog.GetData<EffectData>(bubbleEnterEffectId);
        effectParent = new GameObject("Bubble Effect Parent");
        
        wand.trigger
            .Then(() => wand.item.isThrowed)
            .Do(AwaitCollision);
        wand.item.OnGrabEvent -= OnGrab;
        wand.item.OnGrabEvent += OnGrab;
        wand.item.OnTelekinesisGrabEvent += OnTkGrab;
    }

    private void OnTkGrab(Handle handle, SpellTelekinesis teleGrabber)
    {
        cancel?.Invoke();
        cancel = null;
    }

    private void OnGrab(Handle handle, RagdollHand ragdollHand)
    {
        cancel?.Invoke();
        cancel = null;
    }

    public Action cancel;
    private GameObject effectParent;

    public void AwaitCollision()
    {
        wand.item.OnNextCollision(Collision, out cancel);
    }

    public void Collision(CollisionInstance collision) => wand.StartCoroutine(SlowRoutine(collision));

    public IEnumerator SlowRoutine(CollisionInstance collision) {
        MarkCasted();
        var sphere = new GameObject().AddComponent<SphereCollider>();
        var zone = sphere.gameObject.AddComponent<Zone>();
        zone.blockPhysicsCulling = true;
        zone.statusOnCreature = zone.statusOnItem = true;
        zone.statusOnPlayer = false;
        zone.constantStatus = false;
        zone.playStatusEffects = false;
        zone.itemEnterEvent.AddListener(obj => (obj as Item)?.GetOrAddComponent<VelocityStorer>()?.Activate(1.5f));
        zone.itemExitEvent.AddListener(obj => (obj as Item)?.GetOrAddComponent<VelocityStorer>()?.Deactivate());
        zone.statusIDs = ["BubbleGravity", "Slowed"];
        sphere.transform.position = collision.contactPoint;
        sphere.radius = 0;
        zone.playerEnterEvent.AddListener(_ =>
        {
            bubbleEnterEffectData.Spawn(wandItem.transform).Play();
            SnapshotTool.DoSnapshotTransition(ThunderRoadSettings.audioMixerSnapshotUnderwater, 0);
        });
        zone.playerExitEvent.AddListener(_
            => SnapshotTool.DoSnapshotTransition(ThunderRoadSettings.audioMixerSnapshotDefault, 0));
        effectParent.transform.SetPositionAndRotation(collision.contactPoint, Quaternion.identity);
        var effect = bubbleEffectData.Spawn(effectParent.transform);
        // effect.SetPosition(collision.contactPoint);
        SetSizeCurve(AnimationCurve.EaseInOut(0, 0, 1, radius * 2), effect);

        foreach (var effectMesh in effect.effects.OfType<EffectMesh>())
        {
            effectMesh.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            // effectMesh.transform.position = collision.contactPoint;
        }

        effect.SetIntensity(0);
        effect.Play();
        wand.item.physicBody.isKinematic = true;
        wand.item.DisallowDespawn = true;

        yield return Utils.LoopOver(time => {
            float amount = radiusCurve.Evaluate(time);
            effect.SetIntensity(amount);
            wand.transform.SetPositionAndRotation(
                Vector3.Lerp(wand.transform.position, collision.contactPoint + collision.contactNormal * 0.4f,
                    amount),
                Quaternion.Slerp(wand.transform.rotation, Quaternion.LookRotation(-collision.contactNormal),
                    amount));
            zone.SetRadius(amount * radius);
        }, 1);

        float startTime = Time.time;
        while (true) {
            wand.transform.rotation = Quaternion.Slerp(wand.transform.rotation,
                Quaternion.LookRotation(-collision.contactNormal,
                    Quaternion.AngleAxis((Time.time - startTime).Remap(0, 1, 0, 360), -collision.contactNormal)
                    * Vector3.Cross(-collision.contactNormal, Vector3.forward)), Time.deltaTime * 10);

            if (wand.item.mainHandler != null || wand.item.isTelekinesisGrabbed) {
                break;
            }

            yield return 0;
        }


        GameManager.local.StartCoroutine(Utils.LoopOver(time => effect.SetIntensity(radiusCurve.Evaluate(1 - time)),
            0.3f, () => effect.End()));
        SnapshotTool.DoSnapshotTransition(ThunderRoadSettings.audioMixerSnapshotDefault, 0);

        wand.item.physicBody.isKinematic = false;
        Object.Destroy(zone.gameObject);
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

    // public void Slow(Collider collider, bool enter) {
    //     if (collider.attachedRigidbody is not Rigidbody rb) return;
    //     if (rb.GetComponentInParent<Player>()
    //         || rb.GetComponentInParent<Item>() is Item rbItem
    //         && (rbItem.mainHandler?.creature?.isPlayer == true || rbItem == wand.item))
    //         return;

    //     if (enter) {
    //         if (rigidbodies.Contains(rb)) return;
    //         rigidbodies.Add(rb);
    //         Creature hitCreature = null;
    //         if (rb.GetComponentInParent<Creature>() is Creature creature) {
    //             hitCreature = creature;
    //         } else if (rb.GetComponent<RagdollPart>() is RagdollPart part && part == part.ragdoll.rootPart) {
    //             hitCreature = part.ragdoll.creature;
    //         }

    //         if (hitCreature != null) {
    //             hitCreature.Inflict("Slowed", this);
    //             creatures.Add(hitCreature);
    //         }

    //         // rb.AddModifier(this, 3, 0, 10);
    //     } else {
    //         if (!rigidbodies.Contains(rb)) return;
    //         rigidbodies.Remove(rb);
    //         if (rb.GetComponent<Creature>() is Creature creature) {
    //             creature.Remove("Slowed", this);
    //             creatures.Remove(creature);
    //         }

    //         // rb.RemoveModifier(this);
    //     }
    // }
}
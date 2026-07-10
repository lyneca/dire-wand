using System.Collections;
using System.Collections.Generic;
using ExtensionMethods;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Wand;

public class Firebreathing : WandSkill {
    public float dps = 30;
    public static EffectData revealFire;
    private Dictionary<Creature, RaycastHit> hits;
    public SkillInfernoStaff skill;

    public override void Register() {
        base.Register();
        revealFire = Catalog.GetData<EffectData>("RevealFire");
        skill = Catalog.GetData<SkillInfernoStaff>("InfernoStaff");
        wand.button
            .Then(() => Vector3.Distance(wand.tip.position, Player.currentCreature.mouthRelay.transform.position)
                        < 0.1f)
            .Do(Flamethrower);
    }

    public void Flamethrower() {
        hits = new Dictionary<Creature, RaycastHit>();
        MarkCasted();
        wand.StartCoroutine(FlamethrowerRoutine(Player.currentCreature.mouthRelay.transform));
    }

    public IEnumerator FlamethrowerRoutine(Transform parent) {
        var speak = Player.currentCreature.brain.instance.GetModule<BrainModuleSpeak>();
        var flamethrower = new GameObject().GetOrAddComponent<Flamethrower>();
        flamethrower.transform.SetParent(parent);
        flamethrower.transform.SetPositionAndRotation(parent.transform.position - Vector3.up * 0.03f, Player.local.head.transform.rotation);
        flamethrower.Fire(skill, Player.currentCreature, null, true);
        speak.GetField<Transform>("jawAnimBone").localRotation = Quaternion.Lerp(speak.jawOrgLocalRotation,
            speak.jawOrgLocalRotation * Quaternion.Euler(Player.currentCreature.jawMaxRotation), 0.5f);
        var lastPos = parent.position + parent.forward;
        float speed = 0;
        while (wand.active
               && Vector3.Distance(wand.tip.position, parent.position) < 0.2f) {
            // var currentPos = parent.position + parent.forward;
            // speed = Mathf.Lerp(speed,
            //     (lastPos - currentPos).magnitude.RemapClamp01(0, LiveValue.Get<float>("flame.speed", 0.02f)),
            //     Time.deltaTime * 20);
            // lastPos = currentPos;
            // CastFlames();
            yield return 0;
        }

        speak.GetField<Transform>("jawAnimBone").localRotation = Quaternion.Lerp(speak.jawOrgLocalRotation,
            speak.jawOrgLocalRotation * Quaternion.Euler(Player.currentCreature.jawMaxRotation), 0);
        flamethrower.Fire(skill, Player.currentCreature, null, false);
    }

    public void ApplyFireEffects(RaycastHit hit, ColliderGroup group) {
        if (!group) return;

        if (group.collisionHandler?.isItem == true) {
            // group.imbue?.Transfer(Catalog.GetData<SpellCastCharge>("Fire"), 0.5f);
            group.collisionHandler?.physicBody?.AddForce(
                (group.transform.position - Player.local.head.transform.position).normalized * 10, ForceMode.Force);
        } else if (group.collisionHandler?.isRagdollPart == true) {
            ApplyBurn(hit, group);
            group.collisionHandler?.physicBody?.AddForce(
                (group.transform.position - Player.local.head.transform.position).normalized * 15,
                ForceMode.Acceleration);
        }
    }

    public void ApplyBurn(RaycastHit hit, ColliderGroup target) {
        var hitVector = hit.point - Player.local.head.transform.position;
        var part = target.collisionHandler.ragdollPart;
        var creature = part.ragdoll.creature;
        if (creature.isPlayer) return;

        var collisionInstance = new CollisionInstance(new DamageStruct(DamageType.Energy, dps * Time.deltaTime)
            { hitRagdollPart = part }) {
            targetColliderGroup = part.colliderGroup,
            contactPoint = hit.point,
            contactNormal = hit.normal,
            casterHand = wand.holdingHand.caster
        };
        var effect = revealFire.Spawn(hit.point, Quaternion.LookRotation(hit.normal, Utils.RandomVector()),
            part.transform, collisionInstance);
        effect.SetIntensity(Random.Range(1f, 2f));
        effect.Play();

        if (creature.isKilled) return;
        creature.TryPush(Creature.PushType.Magic, hitVector, 1);
        creature.Damage(collisionInstance);
    }

    public void CastFlames() {
        hits.Clear();
        foreach (var hit in Utils.ConeCastAll(Player.currentCreature.mouthRelay.transform.position, 0.05f,
                     Player.local.head.transform.forward, 5, 15)) {
            if (hit.collider.GetComponentInParent<ColliderGroup>() is not ColliderGroup group) continue;
            if (group.collisionHandler.ragdollPart?.ragdoll.creature is Creature creature) {
                if (!hits.ContainsKey(creature)) {
                    hits[creature] = default;
                    continue;
                }

                if (Vector3.Angle(hits[creature].point, Player.local.head.transform.forward)
                    < Vector3.Angle(hit.point, Player.local.head.transform.forward)) {
                    hits[creature] = hit;
                }
            } else {
                ApplyFireEffects(hit, group);
            }
        }

        foreach (var hit in hits.Values) {
            wand.RunAfter(() => ApplyFireEffects(hit, hit.collider?.GetComponentInParent<ColliderGroup>()),
                0.5f * Vector3.Distance(hit.point, Player.currentCreature.mouthRelay.transform.position));
        }
    }
}
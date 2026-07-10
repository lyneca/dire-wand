using System.Collections;
using ExtensionMethods;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Wand; 

public class Lightning : WandSkill {
    public override void Register() {
        base.Register();
        wand.button.Then(() => wand.localTipVelocity.MostlyZ()
                               && wand.localTipVelocity.z > wand.module.gestureVelocityNormal, "Stab Forwards")
            .Do(LightningBolt, "Lightning Bolt");
    }

    public void LightningBolt() {
        MarkCasted();
        wand.StartCoroutine(LightningBoltRoutine());
    }

    public IEnumerator LightningBoltRoutine() {
        var spell = Catalog.GetData<SpellCastLightning>("Lightning");
        var arcStaffEffectData = Catalog.GetData<EffectData>("SpellLightningArcStaffLoop");
        var skill = Catalog.GetData<SkillArcwire>("Arcwire");
        var prevNode = LightningTrailNode.New(wand.tip.position, skill, null, Player.currentCreature);
        var nextNode
            = LightningTrailNode.New(wand.tip.position, skill, null, Player.currentCreature, wand.tip, prevNode);
        var arcStaffEffect = arcStaffEffectData.Spawn(wand.tip);
        float arcWhooshIntensity = 0;
        arcStaffEffect.Play();
        Vector3 force = wand.tip.forward
                        * (Mathf.Clamp(wandItem.physicBody.GetPointVelocity(wand.tip.position).magnitude, 0.0f, 8) * 2);
        wandItem.Haptic(0.5f);
        prevNode.rb.AddForce(force, ForceMode.VelocityChange);

        while (wandItem.mainHandler?.playerHand?.controlHand.alternateUsePressed == true && nextNode != null) {
            if (prevNode == null || (nextNode.transform.position - prevNode.transform.position).magnitude
                > 0.3f) {
                nextNode.transform.SetParent(null);
                prevNode = nextNode;
                nextNode = LightningTrailNode.New(wand.tip.position, skill, null, Player.currentCreature, wand.tip, prevNode);
                force = wand.tip.forward
                        * (Mathf.Clamp(wandItem.physicBody.GetPointVelocity(wand.tip.position).magnitude, 0, 8) * 2);
                prevNode.rb.AddForce(force, ForceMode.VelocityChange);
            }

            if (!wandItem.physicBody.IsSleeping()) {
                Vector3 pointVelocity = wandItem.physicBody.GetPointVelocity(wand.tip.position);
                arcWhooshIntensity = Mathf.Lerp(arcWhooshIntensity,
                    Mathf.InverseLerp(5, 12, pointVelocity.magnitude), 0.1f);
                arcStaffEffect.SetSpeed(arcWhooshIntensity);
                if (wandItem) arcStaffEffect.source = wandItem;
                if (arcWhooshIntensity > 0 && !arcStaffEffect.isPlaying) {
                    arcStaffEffect.Play();
                }
            } else if (arcWhooshIntensity > 0) {
                arcWhooshIntensity = Mathf.Lerp(arcWhooshIntensity, 0, 0.1f);
            }

            yield return 0;
        }

        arcStaffEffect.End();
        if (!nextNode) yield break;
        nextNode.transform.SetParent(null);
        nextNode.EndTrail();
        wandItem.Haptic(0.5f);
        nextNode.rb.AddForce(
            wand.tip.forward
            * (Mathf.Clamp(wandItem.physicBody.GetPointVelocity(wand.tip.position).magnitude, 0.0f, 8) * 2),
            ForceMode.VelocityChange);
    }

}
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Resurrect : WandSkill
{
    public override void Register()
    {
        base.Register();
        wand.profane.Then(() => wand.target is Creature { isKilled: true }).Then(wand.Point(ViewDir.Down))
            .Then(wand.Swirl(SwirlDirection.Either)).Do(ResurrectEnemy);
    }

    public void ResurrectEnemy()
    {
        if (wand.target is not Creature creature) return;
        creature.ResurrectMaxHealth();
        creature.SetFaction(Catalog.gameData.GetFactionIDFromName("Player"));
        creature.brain.currentTarget = null;

        if (!creature.ragdoll.headPart.isSliced)
        {
            creature.SetVariable("OrgSwingLimit",
                (creature.ragdoll.headPart.orgCharacterJointData.swingLimitSpring.spring,
                    creature.ragdoll.headPart.orgCharacterJointData.swingLimitSpring.damper));
            creature.SetVariable("OrgTwistLimit",
                (creature.ragdoll.headPart.orgCharacterJointData.twistLimitSpring.spring,
                    creature.ragdoll.headPart.orgCharacterJointData.twistLimitSpring.damper));

            creature.ragdoll.headPart.orgCharacterJointData.swingLimitSpring = new SoftJointLimitSpring
                { damper = 0, spring = 0 };
            creature.ragdoll.headPart.orgCharacterJointData.twistLimitSpring = new SoftJointLimitSpring
                { damper = 0, spring = 0 };
            creature.ragdoll.headPart.DisableCharJointLimit();
            creature.ragdoll.headPart.CreateCharJoint(false);
        }

        if (creature.TryGetVariable("LastHeldItem", out Item item)
            && item is { isCulled: false, despawning: false, IsFree: true })
        {
            var startPos = item.transform.position;
            var startRot = item.transform.rotation;
            var handle = item.GetMainHandle(Side.Right);
            var orientation = handle.GetDefaultOrientation(Side.Right);
            item.FullyUnpenetrate();
            item.DisallowDespawn = true;
            item.physicBody.isKinematic = true;
            item.LoopOver(time =>
            {
                if (!item || !creature) return;
                item.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos,
                        creature.handRight.grip.position + (item.transform.position - orientation.transform.position),
                        Mathf.Pow(time, 2)),
                    Quaternion.Slerp(startRot,
                        creature.handRight.grip.rotation * (Quaternion.Inverse(item.transform.rotation)
                                                            * orientation.transform.rotation), Mathf.Pow(time, 2)));
            }, 1, () =>
            {
                if (!item || !creature) return;
                item.physicBody.isKinematic = false;
                item.DisallowDespawn = false;
                creature.handRight.Grab(handle, true);
            });
        }

        creature.OnDespawnEvent -= OnCreatureDespawn;
        creature.OnDespawnEvent += OnCreatureDespawn;
        
        if (wand.TryGetSkill("Basic", out Basic basic))
        {
            basic.OnHitEntity -= OnBasicHit;
            basic.OnHitEntity += OnBasicHit;
        }
        return;

        void OnBasicHit(WandBehaviour wandBehaviour, Basic basicSkill, ThunderEntity entity)
        {
            if (entity is Creature hitEntity)
                creature.brain.currentTarget = hitEntity;
        }

        void OnCreatureDespawn(EventTime eventTime)
        {
            if (eventTime is EventTime.OnStart) return;
            
            if (wand.TryGetSkill("Basic", out Basic basicSkill))
            {
                basicSkill.OnHitEntity -= OnBasicHit;
                basicSkill.OnHitEntity += OnBasicHit;
            }
            basicSkill.OnHitEntity -= OnBasicHit;
            creature.OnDespawnEvent -= OnCreatureDespawn;
            
            if (creature.TryGetVariable("OrgSwingLimit", out (float spring, float damper) swing))
                creature.ragdoll.headPart.orgCharacterJointData.swingLimitSpring = new SoftJointLimitSpring
                    { damper = swing.damper, spring = swing.spring };

            if (creature.TryGetVariable("OrgTwistLimit", out (float spring, float damper) twist))
                creature.ragdoll.headPart.orgCharacterJointData.twistLimitSpring = new SoftJointLimitSpring
                    { damper = twist.damper, spring = twist.spring };
        }
    }
}
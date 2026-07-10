using System.Linq;
using System.Net.Security;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wand; 

public class Lift : WandSkill {
    protected Rigidbody jointPoint;
    protected Joint joint;
    public string liftEffectId = "WandLift";
    public EffectData liftEffectData;
    private float distanceToCatch = 2f;
    [ModOption("Pull Force"), ModOptionSlider, ModOptionFloatValues(0, 200, 10)]
    public static float pullForce = 50f;

    public enum State
    {
        None,
        Pulling,
        Lifting
    }

    public override void Register() {
        base.Register();
        liftEffectData = Catalog.GetData<EffectData>(liftEffectId);
        wand.OnTargetEntity(step =>
        {
            var pulling = step
                .Then("Button held", () => wand.Buttoning)
                .Do(StartPull);
            pulling.Then(() => !wand.Buttoning).Do(() => state = State.None).ThenResetTo(step);
            pulling.Then(() => wand.Buttoning && wand.target?.Center.DistanceSqr(wand.tip.position) is float distance
                                              && distance < distanceToCatch * distanceToCatch)
                .Do(LiftEntity, "Lift Entity");
        });
        jointPoint = new GameObject().AddComponent<Rigidbody>();
        jointPoint.useGravity = false;
        jointPoint.isKinematic = true;
    }

    public void StartPull()
    {
        state = State.Pulling;
        switch (wand.target)
        {
            case Item item:
                break;
            case Creature creature:
                creature.ragdoll.forcePhysic.Add(this);
                creature.ragdoll.SetState(Ragdoll.State.Destabilized);
                break;
        }
    }

    public override void OnUpdate() {
        base.OnUpdate();
        jointPoint.transform.position = wand.tip.transform.position
                                        + wand.tip.transform.forward * 2
                                        + wand.tipVelocity.normalized
                                        * Mathf.Clamp(wand.tipVelocity.magnitude, 0.0f, 8f)
                                        / 5f;
        jointPoint.transform.rotation = Quaternion.Slerp(jointPoint.transform.rotation,
            wand.tip.transform.rotation * Quaternion.AngleAxis(180, Vector3.forward),
            Time.deltaTime * 5);

        if (wand.target is Item targetItem
            && wand.otherHand.Casting()
            && wand.otherHand.caster?.spellInstance is SpellCastCharge { imbueEnabled: true } spell
            && (wand.otherHand.grip.position - wand.tip.position).sqrMagnitude < 0.15f * 0.15f) {
            for (var i = 0; i < wandItem.colliderGroups.Count; i++) {
                targetItem.colliderGroups[i].imbue
                    ?.Transfer(spell, spell.imbueRate * spell.currentCharge * Time.deltaTime);
            }
        }

        if (wand.target && state == State.Pulling)
        {
            CollisionHandler handler = null;
            if (wand.target is Creature creature)
            {
                handler = creature.ragdoll.rootPart.collisionHandler;
            }
            wand.target.AddForce((wand.tip.position - wand.target.Center).normalized * pullForce, ForceMode.Acceleration, handler);
        }

        if (!joint) return;
        if (wand.target is not Item item) return;
        if (!item.isFlying)
            item.Throw(1, Item.FlyDetection.Forced);
        item.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
    }

    public override void OnReset() {
        base.OnReset();
        wand.PlaySound(SoundType.Ragh);
        state = State.None;
        Object.Destroy(joint);
        switch (wand.target)
        {
            case Creature creature:
                creature.ragdoll.forcePhysic.Remove(this);
                creature.brain.RemoveNoStandUpModifier(this);
                break;
        }
    }

    public State state;
    public void LiftEntity() {
        if (wand.target == null) {
            wand.Reset();
            return;
        }

        state = State.Lifting;
        
        MarkCasted();

        // var line = Catalog.GetData<EffectData>("WandLineLoop").Spawn(wand.tip);
        // line.SetSource(wand.tip);
        // line.SetTarget(wand.target.Transform);
        // line.SetMainGradient(wand.module.targetArgs.gradient);
        // line.Play();
            
        wand.PlaySound(SoundType.Legh, wand.TargetTransform);
        
        CollisionHandler handler = null;
        if (wand.target is Creature { isKilled: false } creature)
        {
            handler = creature.ragdoll.rootPart.collisionHandler;
            creature.ragdoll.forcePhysic.Add(this);
            creature.ragdoll.SetState(Ragdoll.State.Destabilized);
            creature.brain.AddNoStandUpModifier(this);
        }
            
        wand.target.gameObject.GetComponent<FreezeModifier>()?.Clear();

        wand.target.ClearByType<Floating>();
        wand.target.Inflict("WandFloating", this);
        wand.target.AddForce(Vector3.up * 2, ForceMode.VelocityChange, handler);
        wand.OnReset(() => {
            // wand.target.DropBreakables(wand, true);
            wand.target.Remove("WandFloating", this);
        });
        wand.UntilReset(() => wand.item.Haptic(wand.target.RootPhysicBody.velocity.magnitude.RemapClamp(0, 20f, 0, 0.5f)));

        float modifier = Mathf.Sqrt(wand.target.RootPhysicBody.mass);
        jointPoint.transform.position = wand.target.Center;
        // wand.target.PickupBreakables();
        joint = Utils.CreateSimpleJoint(jointPoint, wand.target.RootPhysicBody.rigidBody, 1000 * modifier,
            150 * modifier,
            100000f * modifier,
            targetRotation: wand.target switch
            {
                Item item => item.flyDirRef?.transform.rotation
                             ?? item.colliderGroups.FirstOrDefault()?.imbueShoot?.transform.rotation,
                _ => wand.target.RootPhysicBody.transform.rotation
            });
        joint.connectedAnchor = wand.target.RootPhysicBody.centerOfMass;
        wand.throwTarget = true;
        liftEffectData.Spawn(wand.TargetTransform).Play();
    }
}
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wand; 

public class Lift : WandModule {
    protected Rigidbody jointPoint;
    protected Joint joint;
    public string liftEffectId = "WandLift";
    public EffectData liftEffectData;
        
    public override void OnInit() {
        base.OnInit();
        liftEffectData = Catalog.GetData<EffectData>(liftEffectId);
        wand.targetedItem
            .Then(wand.Flick(AxisDirection.Up, wand.module.gestureVelocityLarge))
            .Do(LiftEntity, "Lift Item");
        wand.targetedEnemy
            .Then(wand.Flick(AxisDirection.Up, wand.module.gestureVelocityLarge))
            .Do(LiftEntity, "Lift Creature");
        jointPoint = new GameObject().AddComponent<Rigidbody>();
        jointPoint.useGravity = false;
        jointPoint.isKinematic = true;
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
        if (joint) {
            if (wand.target?.handler?.item?.isFlying == false)
                wand.target.handler.item.Throw(1, Item.FlyDetection.Forced);
            wand.target?.handler?.item?.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
        }
    }

    public override void OnReset() {
        base.OnReset();
        wand.PlaySound(SoundType.Ragh);
        Object.Destroy(joint);
    }

    public void LiftEntity() {
        if (wand.target == null) {
            wand.Reset();
            return;
        }
            
        wand.PlaySound(SoundType.Legh, wand.target.transform);
            
        wand.target.item?.gameObject.GetComponent<FreezeModifier>()?.Clear();

        wand.target.Release();
        wand.target.Grab();

        float modifier = Mathf.Sqrt(wand.target.Rigidbody().mass);
        jointPoint.transform.position = wand.target.Center();
        joint = Utils.CreateSimpleJoint(jointPoint, wand.target.Rigidbody(), 1000 * modifier, 150 * modifier,
            100000f * modifier,
            targetRotation: wand.target.item?.flyDirRef?.transform.rotation
                            ?? wand.target.Rigidbody().transform.rotation);
        joint.connectedAnchor = wand.target.Rigidbody().centerOfMass;
        wand.target.throwOnRelease = true;
        liftEffectData.Spawn(wand.target.transform).Play();
    }
}
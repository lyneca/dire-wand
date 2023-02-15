using System.Collections;
using ThunderRoad;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Wand; 

public class Grapple : WandModule {
    public string grappleLineEffectId = "GrappleLine";
    public EffectData grappleLineEffectData;
    public override void OnInit() {
        base.OnInit();
        grappleLineEffectData = Catalog.GetData<EffectData>(grappleLineEffectId);
        wand.trigger.Then(wand.Flick(AxisDirection.Up), wand.Point(ViewDir.Up))
            .Do(() => wand.PlaySound(SoundType.Hagh))
            .Then(wand.Flick(AxisDirection.Down), wand.Still())
            .Do(StartGrapple, "Grapple");
    }
        
    public void StartGrapple() {
        MarkCasted();
        wand.PlaySound(SoundType.Quough);
        if (Physics.SphereCast(wand.tipRay, 0.3f, out RaycastHit hit, 40, LayerMask.GetMask("Default"),
                QueryTriggerInteraction.Ignore)) {
            wand.StartCoroutine(GrappleRoutine(hit));
        }
    }

    public IEnumerator GrappleRoutine(RaycastHit hit) {
        var targetObj = wand.objectPool.Get();
        var grappleLine = grappleLineEffectData.Spawn(wand.transform);
        targetObj.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
        grappleLine.SetSource(wand.tip);
        grappleLine.SetTarget(targetObj.transform);
        grappleLine.Play();
        Player.local.locomotion.SetPhysicModifier(this, 0);
        while (wand.active) {
            Player.local.locomotion.rb.velocity *= 0.97f;
            yield return 0;
        }

        Player.local.locomotion.RemovePhysicModifier(this);

        Vector3 velocity = wand.tipVelocity;
        grappleLine.End();
        wand.objectPool.Release(targetObj);

        Vector3 force = Vector3.Slerp(velocity.normalized * -1, (hit.point - wand.tip.position).normalized, 0.5f)
                        * (velocity.magnitude * 2f);
        Player.local.locomotion.rb.AddForce(force, ForceMode.VelocityChange);
    }

}
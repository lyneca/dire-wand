using System.Collections;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Lumos : WandModule {
    protected Transform lightParent;
    protected MeshRenderer lightRenderer;
    protected Light light;
    protected bool lightThrown = false;
    protected float actualLightIntensity;
    protected float targetLightIntensity;
    public float actualLightRange = 10;
    protected float targetLightRange = 10;
    protected Rigidbody lightRb;
    public Color lightColor = Utils.HexColor(77, 123, 191, 0);
    public Color sphereColor = Utils.HexColor(77, 123, 191, 5.2f);
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public override void OnInit() {
        wand.button.Then(() => lightThrown
                               && wand.tipViewVelocity.z < -wand.module.gestureVelocityLarge
                               && !wand.localTipVelocity.MostlyZ(), "Flick Back")
            .Do(() => ReturnLight(), "Return Light");
        wand.button.Then(wand.Twist(90, SwirlDirection.CounterClockwise))
            .And("Palm on tip",
                () => (wand.otherHand.grip.position - wand.tip.position).sqrMagnitude < 0.1f * 0.1f)
            .Do(ControlLight, "Light Control (Dim)");
        wand.button.Then(wand.Twist(90, SwirlDirection.Clockwise))
            .And("Palm on tip", () => (wand.otherHand.grip.position - wand.tip.position).sqrMagnitude < 0.1f * 0.1f)
            .Do(ControlLight, "Light Control (Bright)")
            .Then(() => !lightThrown
                        && targetLightIntensity == 1
                        && wand.tipViewVelocity.z > wand.module.gestureVelocityLarge
                        && !wand.localTipVelocity.MostlyZ(), "Throw")
            .Do(ThrowLight, "Throw Light");

        lightRb = new GameObject().AddComponent<Rigidbody>();
        lightRb.useGravity = false;
        lightRb.mass = 1;
        lightRb.drag = 3;
            
        lightParent = new GameObject("LightParent").transform;
        lightParent.transform.SetParent(wand.tip);
        lightParent.transform.SetPositionAndRotation(wand.tip.position, wand.tip.rotation);

        lightRenderer = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshRenderer>();
        lightRenderer.material = wand.module.lightMat;
        Object.Destroy(lightRenderer.GetComponent<Collider>());
        lightRenderer.transform.SetParent(lightParent);
        lightRenderer.transform.SetPositionAndRotation(
            lightParent.transform.position,
            lightParent.transform.rotation);
            
        light = new GameObject("LightParent").AddComponent<Light>();
        light.transform.SetParent(lightRenderer.transform);
        light.transform.SetPositionAndRotation(lightRenderer.transform.position, lightRenderer.transform.rotation);
            
        light.color = lightColor;
        light.range = 0;
        light.type = LightType.Point;
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0;
        light.intensity = 0;
    }

    public override void OnUpdate() {
        actualLightIntensity = Mathf.Lerp(actualLightIntensity, targetLightIntensity, Time.deltaTime * 20);
        actualLightRange = Mathf.Lerp(actualLightRange, targetLightRange, Time.deltaTime * 20);

        if (light) {
            light.intensity = actualLightIntensity;
            light.range = actualLightRange;
        }

        if (lightRenderer) {
            lightRenderer.material.SetColor(BaseColor, sphereColor * actualLightIntensity);
            lightRenderer.transform.localScale = Vector3.one * (actualLightIntensity * 0.01f);
        }
    }
    public override void OnReset() {}

    public void ControlLight() {
        MarkCasted();
        wand.PlaySound(SoundType.Hagh);
        wand.StartCoroutine(LightControlRoutine());
    }

    public void SetLight(float intensity, bool skipReturn = false) {
        targetLightIntensity = intensity;
        if (!skipReturn && lightThrown && targetLightIntensity == 0) {
            ReturnLight(true);
        }
    }

    public IEnumerator LightControlRoutine() {
        float startIntensity = targetLightIntensity;
        while (wand.active) {
            SetLight((startIntensity * 50 - wand.angleTurned).RemapClamp01(0, 50));
            yield return 0;
        }
    }

    public void ThrowLight() {
        wand.module.castEffectData.Spawn(wand.tip).Play();
        lightRb.transform.position = lightRenderer.transform.position;
        lightRb.velocity = Vector3.zero;
        lightRenderer.transform.SetParent(lightRb.transform);
        lightRb.AddForce(
            Vector3.Slerp(wand.tipVelocity.normalized, Player.local.head.transform.forward, 0.5f)
            * (wand.tipVelocity.magnitude * 7), ForceMode.VelocityChange);
        lightThrown = true;
        targetLightRange = 40;
        targetLightIntensity = 1;
        wand.canRestart = true;
        wand.Reset();
    }

    public void ReturnLight(bool instant = false) {
        if (instant) {
            lightRenderer.transform.SetParent(lightParent);
            lightRenderer.transform.SetPositionAndRotation(lightParent.position, lightParent.rotation);
            lightThrown = false;
            targetLightRange = 10;
        } else wand.StartCoroutine(LightReturnRoutine());
    }

    public IEnumerator LightReturnRoutine() {
        targetLightRange = 10;
        return Utils.LoopOver(amount =>
                lightRb.transform.position
                    = Vector3.Lerp(lightRb.transform.position, lightParent.transform.position, amount), 0.6f,
            () => {
                lightRenderer.transform.SetParent(lightParent);
                lightRenderer.transform.SetPositionAndRotation(lightParent.position, lightParent.rotation);
                lightThrown = false;
                wand.canRestart = true;
            }
        );
    }
}
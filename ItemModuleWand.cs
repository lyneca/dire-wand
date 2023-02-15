using System;
using SequenceTracker;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ExtensionMethods;
using GestureEngine;
using Newtonsoft.Json;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace Wand;

using NamedConditionSet = Tuple<string, Func<bool>[]>;
using NamedCondition = Tuple<string, Func<bool>>;
public class Args {
    public Gradient gradient;
}

public enum SwirlDirection {
    CounterClockwise,
    Clockwise,
    Either
}

// Link doors back to their hinges
public class HingeLinker : MonoBehaviour {
    public HingeDrive linkedDrive;
}

[HarmonyPatch(typeof(HingeDrive), "Init")]
public static class HingeDrivePatch {
    public static void Postfix(HingeDrive __instance) {
        __instance.hingesHolder.gameObject.AddComponent<HingeLinker>().linkedDrive = __instance;
    }
}

public class Entity : MonoBehaviour {
    public Creature creature;
    public CollisionHandler handler;
    public Item item;
    public bool isCreature = false;
    public bool throwOnRelease;

    public Action<Entity> whileGrabbed = null;
    public Action<Entity> onUnGrab = null;

    public Transform Transform => creature?.GetTorso().transform ?? item.transform;

    public Vector3 WorldCenter => creature?.GetTorso().transform.position
                                  ?? item.transform.TransformPoint(item.GetLocalCenter());
    public bool shouldRelease = true;
    public UnityEvent<Entity> onReleaseEvent;
    public bool grabbed;

    public void Awake() {
        onReleaseEvent = new UnityEvent<Entity>();
        if (GetComponent<Creature>() is Creature creature) {
            this.creature = creature;
            isCreature = true;
            creature.OnDespawnEvent += time => {
                if (time == EventTime.OnStart)
                    Destroy();
            };
        } else if (GetComponent<Item>() is Item item) {
            this.item = item;
            handler = item.mainCollisionHandler;
            item.OnDespawnEvent += time => {
                if (time == EventTime.OnStart)
                    Destroy();
            };
        }

        pid = new RBPID(Rigidbody(), forceMode: ForceMode.Acceleration, maxForce: isCreature ? 30000 : 5000)
            .Position(isCreature ? 100 : 50, 0, 5);
    }

    public void Destroy() {
        Release();
        Destroy(this);
    }

    public Rigidbody Rigidbody() {
        if (creature) {
            return creature.ragdoll.state == Ragdoll.State.NoPhysic
                ? creature.locomotion.rb
                : creature.GetTorso().rb;
        }

        return handler?.rb;
    }

    public void SetPhysicModifier(object obj, float? gravity = null, float mass = 1, float drag = -1, float angularDrag = -1) {
        handler?.SetPhysicModifier(obj, gravity, mass, drag, angularDrag);
        creature?.ragdoll.SetPhysicModifier(obj, gravity, mass, drag, angularDrag);
    }

    public void RemovePhysicModifier(object obj) {
        handler?.RemovePhysicModifier(obj);
        creature?.ragdoll.RemovePhysicModifier(obj);
    }

    public Vector3 Center() {
        if (creature) {
            return creature.GetTorso().transform.position;
        }

        return Rigidbody()?.worldCenterOfMass ?? item.transform.position;
    }

    public void Grab(bool shouldRelease = true, Action<Entity> whileGrabbed = null, Action<Entity> onUnGrab = null) {
        grabbed = true;
        this.whileGrabbed = whileGrabbed;
        this.onUnGrab = onUnGrab;
        if (creature) {
            creature.brain.AddNoStandUpModifier(this);
            creature.ragdoll.SetPhysicModifier(this, 0);
            creature.ragdoll.AddPhysicToggleModifier(this);
            if (!creature.isKilled)
                creature.ragdoll.SetState(Ragdoll.State.Destabilized);
        } else if (handler) {
            handler.item.Depenetrate();
            handler.SetPhysicModifier(this, 0);
            handler.item.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
            handler.item.rb.collisionDetectionMode = Catalog.gameData.collisionDetection.telekinesis;
            handler.item.forceThrown = true;
            handler.item.Throw();
        }

        this.shouldRelease = shouldRelease;
        throwOnRelease = false;
    }

    public void Update() {
        if (grabbed && whileGrabbed != null) whileGrabbed.Invoke(this);
    }

    public void Release() {
        if (grabbed) {
            onUnGrab?.Invoke(this);
            onUnGrab = null;
        }
        grabbed = false;
        onReleaseEvent.Invoke(this);
        if (creature) {
            creature.brain.RemoveNoStandUpModifier(this);
            creature.ragdoll.RemovePhysicModifier(this);
            creature.ragdoll.RemovePhysicToggleModifier(this);
        } else if (handler) {
            handler.RemovePhysicModifier(this);
            handler.item.forceThrown = false;
            handler.item.Throw(1, Item.FlyDetection.Forced);
        }
    }
    
    RBPID pid;

    public Coroutine PullTowards(Vector3 position) {
        return StartCoroutine(PullTowardsRoutine(position));
    }
    
    public void UpdatePull(Vector3 position) {
        pid.UpdateVelocity(position);
    }

    protected IEnumerator PullTowardsRoutine(Vector3 position, float maxDuration = 10) {
        yield return Utils.LoopOver(_ => {
            if (Vector3.Distance(Transform.position, position) > 1)
                UpdatePull(position);
        }, maxDuration);
    }
}



public class ItemModuleWand : ItemModule {
    public float gestureVelocityNormal = 3f;
    public float gestureVelocitySmall = 2f;
    public float gestureVelocityLarge = 4f;
    public float disarmHandForceMultiplier = 6;
    public Color primaryColor = new Color(0.2f, 0.4f, 1) * 5;
    public Color secondaryColor = new Color(1f, 0.4f, 0.2f) * 5;
    public GameObject gestureNodePrefab;
    public float forceAmount = 40f;
    public ObjectPool<GestureNode> gestureNodePool;
    public string targetLineEffectId = "TargetLine";
    public EffectData targetLineEffectData;
    public string cloneEffectId = "WandClone";
    public EffectData cloneEffectData;
    public string castEffectId = "WandCast";
    public EffectData castEffectData;
    public string targetEffectId = "WandTarget";
    public EffectData targetEffectData;
    public string shoveEffectId = "WandShove";
    public EffectData shoveEffectData;
    public string whooshEffectId = "WandWhoosh";
    public EffectData whooshEffectData;
    public string polymorphEffectId = "WandPolymorph";
    public EffectData polymorphEffectData;
    public string freezeEffectId = "WandFreeze";
    public EffectData freezeEffectData;
    public string flamethrowerEffectId = "WandPointCloud";
    public EffectData flamethrowerEffectData;
    public string shockwaveMatAddress = "Lyneca.Wand.ShockwaveMat";
    public string explosionEffectId = "WandExplosion";
    public EffectData explosionEffectData;
    public Material shockwaveMat;
    public ObjectPool<Shockwave> shockwavePool;
    public float shockwaveDuration = 0.6f;
    public Material lightMat;
    public Material wandTrailMat;

    public float creatureTargetAngle = 10;
    public float itemTargetAngle = 15;
    public float targetRange = 80;

    public List<WandModule> spells;
    
    public AnimationCurve shockwaveCurve = new Utils.CurveBuilder()
        .Key(0, 0, 0, 0)
        .Key(0.4f, 0.7f, 0, 0)
        .Key(1, 1, 0, 0)
        .Build();

    public Args targetArgs = new Args {
        gradient = Utils.FadeInOutGradient(
            Utils.HexColor(40, 30, 191, 6),
            Utils.HexColor(191, 0, 0, 6))
    };

    public Args shoveArgs = new Args {
        gradient = Utils.FadeInOutGradient(
            Utils.HexColor(250, 80, 30, 6),
            Utils.HexColor(191, 0, 0, 6))
    };

    public Gradient attackOrderGradient = Utils.FadeInOutGradient(
        Utils.HexColor(250, 20, 0, 6),
        Utils.HexColor(250, 50, 50, 6));

    public Tuple<Color, Color> primaryTrailColor = Tuple.Create(
        Utils.HexColor(40, 30, 191, 8),
        Utils.HexColor(191, 0, 0, 7));

    public Tuple<Color, Color> secondaryTrailColor = Tuple.Create(
        Utils.HexColor(191, 119, 30, 8),
        Utils.HexColor(191, 0, 0, 7));

    public Gradient primaryGradient = Utils
        .Gradient()
        .Alpha(0, 0)
        .Alpha(1, 0.25f)
        .Alpha(1, 0.75f)
        .Alpha(0, 1)
        .Color(Utils.HexColor(40, 30, 191, 3), 0)
        .Color(Utils.HexColor(0, 61, 191, 3.5f), 0.5f)
        .Color(Utils.HexColor(0, 0, 191, 2f), 1)
        .Build();

    public Gradient secondaryGradient = Utils
        .Gradient()
        .Alpha(0, 0)
        .Alpha(1, 0.25f)
        .Alpha(1, 0.75f)
        .Alpha(0, 1)
        .Color(Utils.HexColor(191, 119, 30, 3), 0)
        .Color(Utils.HexColor(191, 72, 23, 3.5f), 0.5f)
        .Color(Utils.HexColor(0, 0, 191, 2f), 1)
        .Build();

    private static readonly int Size = Shader.PropertyToID("Size");
    private static readonly int Warp = Shader.PropertyToID("Warp");
    public float holdDuration = 0.5f;

    public class GestureNode : MonoBehaviour {
        public static AnimationCurve spawnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public void Enable() {
            gameObject.SetActive(true);
            StartCoroutine(SizeRoutine());
        }

        public void Disable() => gameObject.SetActive(false);

        public IEnumerator SizeRoutine() {
            yield return Utils.LoopOver(amount
                    => transform.localScale = Vector3.one * (spawnCurve.Evaluate(amount) * 0.01f),
                0.3f,
                () => { transform.localScale = Vector3.one * 0.01f; });
            yield return Utils.LoopOver(amount
                    => transform.localScale = Vector3.one * (spawnCurve.Evaluate(1 - amount) * 0.01f),
                0.7f,
                () => { transform.localScale = Vector3.zero; });
        }
    }

    public void SpawnShockwave(Vector3 position, Vector3 facingDir, float size = 1) {
        var shockwave = shockwavePool.Get();
        shockwave.transform.localScale = Vector3.one * (0.5f * size);
        shockwave.transform.SetPositionAndRotation(position,
            Quaternion.LookRotation(facingDir) * Quaternion.FromToRotation(Vector3.up, Vector3.forward));
    }

    public class Shockwave : MonoBehaviour {
        protected float startTime;
        protected Material material;
        protected MeshRenderer renderer;
        protected ItemModuleWand module;

        public Shockwave Init(ItemModuleWand module) {
            Destroy(GetComponent<Collider>());
            renderer = GetComponent<MeshRenderer>();
            renderer.material = module.shockwaveMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            this.module = module;
            material = renderer.material;
            material.SetFloat(Warp, 0.35f);
            material.SetFloat(Size, 0);
            gameObject.SetActive(false);
            return this;
        }

        public void Play(ItemModuleWand module) {
            startTime = Time.time;
            gameObject.SetActive(true);
            this.RunAfter(() => module.shockwavePool.Release(this), module.shockwaveDuration + 0.5f);
        }

        public void Update() {
            material.SetFloat(Size, (Time.time - startTime).RemapClamp01(0, module.shockwaveDuration));
            material.SetFloat(Warp,
                (1 - (Time.time - startTime).RemapClamp01(0, module.shockwaveDuration)) * 0.35f);
        }

        public void End() => gameObject.SetActive(false);
    }


    public override void OnItemDataRefresh(ItemData data) {
        base.OnItemDataRefresh(data);
        try {
            var modData = JsonConvert.DeserializeObject<ModData>(
                FileManager.ReadAllText(FileManager.Type.JSONCatalog, FileManager.Source.Mods,
                    FileManager.aaModPath + "/Wand/manifest.json"), Catalog.GetJsonNetSerializerSettings());
            if (modData != null)
                Debug.Log($"Wand version {modData.ModVersion}");
        } catch (JsonException) { }

        new Harmony("com.lyneca.dire-wand").PatchAll();

        Catalog.LoadAssetAsync<GameObject>("Lyneca.Wand.GesturePoint", obj => {
                gestureNodePrefab = obj;
                gestureNodePool = new ObjectPool<GestureNode>(
                    () => Object.Instantiate(gestureNodePrefab).GetOrAddComponent<GestureNode>(),
                    node => node.Enable(),
                    node => node.Disable(),
                    Object.Destroy
                );
            },
            "ItemModuleWand");
        Catalog.LoadAssetAsync<Material>(shockwaveMatAddress, mat => {
            shockwaveMat = mat;
            shockwavePool = new ObjectPool<Shockwave>(
                () => GameObject.CreatePrimitive(PrimitiveType.Plane).AddComponent<Shockwave>().Init(this),
                shockwave => shockwave.Play(this),
                shockwave => shockwave.End(),
                shockwave => Object.Destroy(shockwave.gameObject)
            );
        }, "ItemModuleWand");

        Catalog.LoadAssetAsync<Material>("Lyneca.Wand.TrailMat", mat => wandTrailMat = mat, "ItemModuleWand");
        Catalog.LoadAssetAsync<Material>("Lyneca.Wand.LightMat", mat => lightMat = mat, "ItemModuleWand");

        targetLineEffectData = Catalog.GetData<EffectData>(targetLineEffectId);
        cloneEffectData = Catalog.GetData<EffectData>(cloneEffectId);
        targetLineEffectData = Catalog.GetData<EffectData>(targetLineEffectId);
        castEffectData = Catalog.GetData<EffectData>(castEffectId);
        targetEffectData = Catalog.GetData<EffectData>(targetEffectId);
        shoveEffectData = Catalog.GetData<EffectData>(shoveEffectId);
        whooshEffectData = Catalog.GetData<EffectData>(whooshEffectId);
        polymorphEffectData = Catalog.GetData<EffectData>(polymorphEffectId);
        freezeEffectData = Catalog.GetData<EffectData>(freezeEffectId);
        flamethrowerEffectData = Catalog.GetData<EffectData>(flamethrowerEffectId);
        explosionEffectData = Catalog.GetData<EffectData>(explosionEffectId);
    }

    public override void OnItemLoaded(Item item) {
        base.OnItemLoaded(item);
        var wand = item.gameObject.AddComponent<WandBehaviour>();
        wand.module = this;
        for (var index = 0; index < spells.Count; index++) {
            var spell = spells[index];
            wand.spells.Add(spell.Clone());
        }

        wand.Init();
        wand.InitModules();
        Debug.Log($"Wand modules loaded. Rendered gesture tree:\n{wand.root.DisplayTree()}");
    }
}

public class WandBehaviour : MonoBehaviour {
    public ItemModuleWand module;
    public Item item;
    public Transform tip;
    protected Transform wandBase;
    public Entity target;
    public Vector3 tipVelocity;
    public Vector3 localTipVelocity;
    public Vector3 tipViewVelocity;
    public bool active;
    public Ray tipRay;
    protected Ray tipLookRay;
    public Ray tipPlayerRay;
    public ObjectPool<GameObject> objectPool;

    protected Color targetColor;
    protected Color actualColor;
    public float angleTurned;
    protected Vector3 lastUp;
    public bool canRestart = false;
    protected float lastCast = 0;
    protected Vector3[] rollingPoints;
    protected int numRollingPoints = 50;
    protected int numPointsStored = 0;
    protected int rollingIndex = 0;
    protected LineRenderer debugLine;
    protected TrailRenderer trail;
    protected VisualEffect vfx;
    protected Color black = Color.black;
    protected float lastPointRecorded;
    protected Vector3 midPoint;
    public float swirlAngle;
    protected Vector3 lastPoint;
    public RagdollHand holdingHand;
    public RagdollHand otherHand;
    protected bool activeTrigger;

    public Step root;
    public Step button;
    public Step trigger;
    protected Step targetedEntity;
    public Step targetedEnemy;
    public Step targetedItem;

    public List<WandModule> spells;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int ColorStart = Shader.PropertyToID("ColorStart");
    private static readonly int ColorEnd = Shader.PropertyToID("ColorEnd");
    private Transform lineSource;
    private float swirlBeginTolerance = 0.025f;
    private float swirlEndTolerance = 0.04f;
    private float swirlLearnRate = 0.01f;
    private int swirlLearnIterations = 10;
    public bool debug;
    private bool swirling;
    public float swirlMinRadius = 0.05f;

    public void Init() {
        trail = new GameObject("Trail").AddComponent<TrailRenderer>();
        trail.time = 1;
        trail.widthCurve = Utils.Curve(0.001f, 0.0001f);
        trail.minVertexDistance = 0.01f;
        trail.numCornerVertices = 5;
        trail.numCapVertices = 5;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = module.wandTrailMat;
        trail.transform.SetParent(tip);
        trail.transform.SetPositionAndRotation(tip.position, tip.rotation);
        SetTrail(false);
    }

    public void Awake() {
        objectPool = new ObjectPool<GameObject>(() => new GameObject(),
            obj => obj.SetActive(true),
            obj => {
                obj.transform.SetParent(null);
                obj.transform.DetachChildren();
                var components = obj.GetComponents<Component>();
                for (var index = 0; index < components.Length; index++) {
                    var component = components[index];
                    if (component is Transform)
                        continue;
                    Destroy(component);
                }

                obj.SetActive(false);
            },
            obj => Destroy(obj), false, 10, 20);

        spells = new List<WandModule>();
        rollingPoints = new Vector3[numRollingPoints];
        item = GetComponent<Item>();

        lineSource = new GameObject().transform;

        tip = item.colliderGroups[0].imbueShoot;
        debugLine = tip.gameObject.AddComponent<LineRenderer>();
        debugLine.startWidth = 0.001f;
        debugLine.endWidth = 0.001f;

        wandBase = item.GetCustomReference("WandBase");

        tipRay = new Ray();
        tipLookRay = new Ray();
        tipPlayerRay = new Ray();

        root = Step.Start(() => {
            item.Haptic(0.7f);
            SpawnNode();
        });

        button = root
            .Then(() => Buttoning, "Button Held",
                runOnChange: false);

        trigger = root.Then(() => Triggering, "Trigger Pressed", runOnChange: false);

        targetedEntity = trigger
            .Then(Brandish())
            .Do(() => TargetEntity(module.targetArgs), "Target Entity");

        targetedEnemy = targetedEntity.Then(() => target?.creature != null, "Creature Targeted");
        targetedItem = targetedEntity.Then(() => target?.item != null, "Item Targeted");
            
        item.OnGrabEvent += (_, _) => Reset();
        item.OnHeldActionEvent += (_, _, action) => {
            switch (action) {
                case Interactable.Action.UseStart:
                    Begin(true);
                    break;
                case Interactable.Action.AlternateUseStart:
                    Begin(false);
                    break;
                case Interactable.Action.UseStop:
                    if (activeTrigger)
                        Reset();
                    break;
                case Interactable.Action.AlternateUseStop:
                    if (!activeTrigger)
                        Reset();
                    break;
            }
        };
        item.OnDespawnEvent += time => {
            if (time == EventTime.OnEnd)
                objectPool.Clear();
        };
    }

    public bool Buttoning => item.mainHandler?.Buttoning() ?? false;
    public bool Triggering => item.mainHandler?.Triggering() ?? false;

    public Gesture Offhand
        => new(() => otherHand == null ? Gesture.HandSide.Both : Gesture.ToHandSide(otherHand.side));

    //public NamedCondition OffhandGesture(
    //    ViewDir? pushDirection = null,
    //    ViewDir? palmDirection = null,
    //    ViewDir? pointDirection = null,
    //    ViewDir? thumbDirection = null,
    //    bool? gripping = null,
    //    bool? triggering = null) {
    //    return Tuple.Create(
    //        "Offhand gesture: "
    //        + string.Join(", ", new List<string> {
    //            pushDirection == null ? "" : $"push {pushDirection.ToString().ToLower()}",
    //            palmDirection == null ? "" : $"palm {palmDirection.ToString().ToLower()}",
    //            pointDirection == null ? "" : $"point {pointDirection.ToString().ToLower()}",
    //            thumbDirection == null ? "" : $"thumb {thumbDirection.ToString().ToLower()}",
    //            gripping switch {
    //                null => "",
    //                true => "gripping",
    //                false => "not gripping"
    //            },
    //            triggering switch {
    //                null => "",
    //                true => "triggering",
    //                false => "not triggering"
    //            }
    //        }.Where(elem => elem != "")),
    //        () => (gripping == null || otherHand.Gripping() == gripping)
    //              && (triggering == null || otherHand.Triggering() == triggering)
    //              && (palmDirection == null || otherHand.PalmDir.WorldToViewSpace().InDirection((ViewDir)palmDirection))
    //              && (pointDirection == null
    //                  || otherHand.PointDir.WorldToViewSpace().InDirection((ViewDir)pointDirection))
    //              && (thumbDirection == null
    //                  || otherHand.ThumbDir.WorldToViewSpace().InDirection((ViewDir)thumbDirection))
    //              && (pushDirection == null
    //                  || otherHand.ViewVelocity()
    //                      .InDirection((ViewDir)pushDirection, module.gestureVelocityNormal)));
    //}

    public void InitModules() {
        for (var index = 0; index < spells.Count; index++) {
            var eachModule = spells[index];
            eachModule.Begin(this);
            eachModule.OnInit();
        }
    }

    public void Update() {
        if (Player.local == null) return;
        tipRay.origin = tip.position;
        tipRay.direction = tip.forward;
        tipLookRay.origin = tipRay.origin;
        tipLookRay.direction = Vector3.Slerp(tipLookRay.direction, Player.local.head.transform.forward, 0.5f);
        tipPlayerRay.origin = Player.local.transform.InverseTransformPoint(tipRay.origin);
        tipPlayerRay.direction = Player.local.transform.InverseTransformDirection(tipRay.direction);
        tipVelocity = item.rb.GetPointVelocity(tipLookRay.origin)
                      - Player.local.locomotion.rb.GetPointVelocity(tipLookRay.origin);
        localTipVelocity = tip.transform.InverseTransformVector(tipVelocity);
        tipViewVelocity = Player.local.head.transform.InverseTransformVector(tipVelocity);
        if (item.mainHandler) {
            holdingHand = item.mainHandler;
            otherHand = holdingHand.otherHand;
        }
        
        if (active) {
            angleTurned += Vector3.SignedAngle(lastUp, transform.up, transform.forward);
            lastUp = transform.up;
            if (debug) {
                Viz.Lines("swirlCircle")
                    .SetPoints(GetCircle(PlayerToWorld(midPoint), swirlMinRadius, PlayerToWorld(midPoint) - wandBase.position))
                    .SetLoop(true)
                    .Width(0.002f)
                    .Color(Color.white)
                    .Show();
            }

            if (Time.time - lastPointRecorded > 0.01f) {
                rollingPoints[rollingIndex] = tipPlayerRay.origin;
                lastPointRecorded = Time.time;
                CalculateMidpoint();
                Vector3 swirlMidpoint;
                float radius;
                if (!swirling) {
                    swirling = CheckSwirl(swirlBeginTolerance, out swirlMidpoint, out radius) && radius > swirlMinRadius;
                } else {
                    swirling = CheckSwirl(swirlEndTolerance, out swirlMidpoint, out radius) && radius > swirlMinRadius;
                }
                if (swirling && numPointsStored > 1 && radius > 0.07f) {
                    midPoint = swirlMidpoint;
                    // Viz.Lines("circle").Color(Color.blue);
                    swirlAngle += Vector3.SignedAngle(lastPoint - swirlMidpoint, tipPlayerRay.origin - swirlMidpoint, swirlMidpoint - WandBasePlayer);
                    // Viz.Lines("angle")
                    //     .SetPoints(PlayerToWorld(swirlMidpoint) + Vector3.up * 0.1f, PlayerToWorld(swirlMidpoint),
                    //         PlayerToWorld(swirlMidpoint)
                    //         + Quaternion.AngleAxis(swirlAngle, PlayerToWorld(swirlMidpoint) - wandBase.position)
                    //         * Vector3.up
                    //         * 0.1f)
                    //     .Color(Color.yellow);
                }

                lastPoint = rollingPoints[rollingIndex];
                if (numPointsStored < numRollingPoints) 
                    numPointsStored++;
                rollingIndex++;
                rollingIndex %= numRollingPoints;
            }
        }

        SetTrail(active, activeTrigger ? module.primaryTrailColor : module.secondaryTrailColor,
            activeTrigger ? module.primaryGradient : module.secondaryGradient);
        if (active) {
            root.Update();
        }

        actualColor = Color.Lerp(actualColor, targetColor, Time.deltaTime * 20f);
        
        if (item.renderers[0].materials.Length > 1)
            item.renderers[0].materials[0].SetColor(EmissionColor, actualColor);

        for (var index = 0; index < spells.Count; index++) {
            spells[index].OnUpdate();
        }

        if (root.AtEnd() && canRestart && Time.time - lastCast > 0.5f && tipVelocity.magnitude < 1) {
            Reset();
            Begin(activeTrigger);
        }
    }

    public Vector3 WandBasePlayer => WorldToPlayer(wandBase.position);
    public Vector3 PlayerToWorld(Vector3 vec) => Player.local.transform.TransformPoint(vec);
    public Vector3 WorldToPlayer(Vector3 vec) => Player.local.transform.InverseTransformPoint(vec);

    // All positions are in player-space
    public Vector2[] ProjectSwirlTo2D(out Vector3 axisX, out Vector3 axisY) {
        int numPoints = Mathf.Min(numRollingPoints, numPointsStored);
        var projected = new Vector2[numPoints];
        var normal = midPoint - WandBasePlayer;
        axisX = Vector3.Cross(normal, Vector3.up).normalized;
        axisY = Vector3.Cross(axisX, normal).normalized;
        int index = numPointsStored == numRollingPoints ? rollingIndex : 0;
        for (int i = 0; i < numPoints; i++) {
            var point = rollingPoints[(index + i) % numRollingPoints] - midPoint;
            projected[i] = new Vector2(Vector3.Dot(point, axisX), Vector3.Dot(point, axisY));
        }

        return projected;
    }

    // compute sum of square errors
    public float CalculateError(float x, float y, float r, Vector2[] points) {
        float error = 0;
        var center = new Vector2(x, y);

        for (var i = 0; i < points.Length; i++) {
            float perpDistance = (points[i] - center).magnitude - r;
            error += Mathf.Pow(perpDistance, 2);
        }

        return error;
    }

    // All positions are in player-space
    public bool CheckSwirl(float tolerance, out Vector3 foundMidpoint, out float radius) {
        var points = ProjectSwirlTo2D(out var axisX, out var axisY);
        var midpoint2d = Vector2.zero;
        var normal = PlayerToWorld(midPoint) - wandBase.position;

        var pointViz = Viz
            .Lines("points")
            .Color(Color.white)
            .Width(0.002f);
        if (debug) {
            pointViz.Clear();
        }

        for (var i = 0; i < points.Length; i++) {
            midpoint2d += points[i];
            if (debug)
                pointViz.AddPoint(PlayerToWorld(midPoint + points[i].x * axisX + points[i].y * axisY));
        }

        midpoint2d /= points.Length;
        float averageRadius = 0;
        
        for (var i = 0; i < points.Length; i++) {
            averageRadius += Vector2.Distance(points[i], midpoint2d);
        }

        averageRadius /= points.Length;
        
        float x = midpoint2d.x;
        float y = midpoint2d.y;
        float r = averageRadius;
        foundMidpoint = midPoint + x * axisX + y * axisY;
        radius = r;
        if (debug) {
            for (int i = 0; i < swirlLearnIterations; i++) {
                Viz.Lines("circle", i.ToString()).Hide();
            }
        }

        for (var i = 0; i < swirlLearnIterations; i++) {
            foundMidpoint = midPoint + x * axisX + y * axisY;
            var worldMidpoint = PlayerToWorld(foundMidpoint);
            radius = r;
            
            float error = CalculateError(x, y, r, points);

            if (debug) {
                Viz.Dot("midpoint", worldMidpoint).Color(Color.green);

                Viz.Lines("circle", i.ToString())
                    .Show()
                    .SetPoints(GetCircle(worldMidpoint, radius, normal, 24))
                    .SetLoop(true)
                    .Width(Mathf.Lerp(0.002f, 0.006f, (float)i / swirlLearnIterations))
                    .Color(error < swirlBeginTolerance
                        ? Color.blue
                        : Color.Lerp(Color.green, Color.red,
                            error.Remap01(swirlBeginTolerance, swirlBeginTolerance * 10)));

                Viz.Lines("plane")
                    .SetPoints(GetSquare(worldMidpoint, Player.local.transform.TransformDirection(axisX),
                        Player.local.transform.TransformDirection(axisY)))
                    .SetLoop(true)
                    .Width(0.006f)
                    .Color(Color.blue);
            }

            if (error < tolerance) return true;
            float dx = (CalculateError(x * 1.01f, y, r, points) - error) / (x * 1.01f - x);
            float dy = (CalculateError(x, y * 1.01f, r, points) - error) / (y * 1.01f - y);
            float dr = (CalculateError(x, y, r * 1.01f, points) - error) / (r * 1.01f - r);

            var nextTest = new Vector3(x, y, r) - new Vector3(dx, dy, dr) * swirlLearnRate;
            
            (x, y, r) = (nextTest.x, nextTest.y, nextTest.z);
        }

        return false;
    }

    public Vector3[] GetCircle(Vector3 midpoint, float radius, Vector3 normal, int numSteps = 8) {
        var points = new Vector3[numSteps];
        var up = Vector3.Cross(normal, Vector3.Cross(Vector3.up, normal)).normalized;
        for (int i = 0; i < numSteps; i++) {
            points[i] = midpoint + Quaternion.AngleAxis(360f / numSteps * i, normal) * up * radius;
        }

        return points;
    }

    public Vector3[] GetSquare(Vector3 midpoint, Vector3 x, Vector3 y) {
        x /= 2;
        y /= 2;
        return new[] {
            midpoint + y - x,
            midpoint + y + x,
            midpoint - y + x,
            midpoint - y - x
        };
    }

    public void PlayCastEffect(Gradient gradient) {
        var effect = Catalog.GetData<EffectData>("WandSpiral").Spawn(wandBase);
        effect?.SetMainGradient(gradient);
        effect?.Play();
    }

    public void SpawnNode() {
        var node = module.gestureNodePool.Get();
        this.RunAfter(() => module.gestureNodePool.Release(node), 1);
        node.transform.position = tip.position;
    }

    public NamedConditionSet Flick(AxisDirection direction, float? velocity = null) {
        float minVelocity = velocity ?? module.gestureVelocityNormal;
        Func<bool> gesture = null;
        switch (direction) {
            case AxisDirection.Up:
                gesture = () => tipViewVelocity.y > minVelocity;
                break;
            case AxisDirection.Down:
                gesture = () => tipViewVelocity.y < -minVelocity;
                break;
            case AxisDirection.Left:
                gesture = () => tipViewVelocity.x < -minVelocity;
                break;
            case AxisDirection.Right:
                gesture = () => tipViewVelocity.x > minVelocity;
                break;
        }

        return Tuple.Create($"Flick {direction}", gesture != null ? new[] { gesture } : new Func<bool>[] { });
    }

    public NamedConditionSet Swirl(SwirlDirection direction, int amount = 1) {
        switch (direction) {
            case SwirlDirection.CounterClockwise:
                return Tuple.Create("Swirl CCW", new Func<bool>[] {
                    () => swirlAngle > 180 * (amount * 2 - 1),
                    () => swirlAngle > 180 * amount * 2
                });
            case SwirlDirection.Clockwise:
                return Tuple.Create("Swirl CW", new Func<bool>[] {
                    () => swirlAngle < -180 * (amount * 2 - 1),
                    () => swirlAngle < -180 * amount * 2
                });
            case SwirlDirection.Either:
                return Tuple.Create("Swirl", new Func<bool>[] {
                    () => Mathf.Abs(swirlAngle) > 180 * (amount * 2 - 1),
                    () => Mathf.Abs(swirlAngle) > 180 * amount * 2
                });
        }

        return Tuple.Create(".", new Func<bool>[] { });
    }

    public NamedConditionSet Brandish() {
        return Tuple.Create("Brandish", new Func<bool>[] {
            () => tipViewVelocity.y < -module.gestureVelocityNormal,
            () => tipViewVelocity.magnitude < 1f
        });
    }

    public NamedConditionSet Twist(float degrees, SwirlDirection direction) {
        switch (direction) {
            case SwirlDirection.CounterClockwise:
                return Tuple.Create($"Twist CW to {degrees}", new Func<bool>[] {
                    () => angleTurned > degrees && tipVelocity.magnitude < 1
                });
            case SwirlDirection.Clockwise:
                return Tuple.Create($"Twist CCW to {degrees}", new Func<bool>[] {
                    () => angleTurned < -degrees && tipVelocity.magnitude < 1
                });
            case SwirlDirection.Either:
                return Tuple.Create($"Twist by {degrees}", new Func<bool>[] {
                    () => Mathf.Abs(angleTurned) > Mathf.Abs(degrees) && tipVelocity.magnitude < 1
                });
        }

        return new NamedConditionSet("", new Func<bool>[] { });
    }

    public NamedConditionSet Still() {
        return Tuple.Create("Still", new Func<bool>[] {
            () => tipViewVelocity.magnitude < 1f
        });
    }

    public NamedConditionSet Point(ViewDir direction) {
        return Tuple.Create($"Point {direction}",
            new Func<bool>[]
                { () => tip.forward.InDirection(direction) });
    }

    public void CalculateMidpoint() {
        Vector3 newMidPoint = Vector3.zero;
        int numPoints = Mathf.Min(numRollingPoints, numPointsStored);
        int index = numPointsStored == numRollingPoints ? rollingIndex : 0;
        for (int i = 0; i < numPoints; i++) {
            newMidPoint += rollingPoints[(index + i) % numRollingPoints];
        }

        midPoint = newMidPoint / numPoints;
    }

    public void SetEmission(Color color) => targetColor = color;

    public void Begin(bool trigger) {
        if (active) return;
        activeTrigger = trigger;
        active = true;
        lastUp = transform.up;
        angleTurned = 0;
        swirling = false;
        swirlAngle = 0;
        numPointsStored = 0;
        rollingIndex = 0;
        lastPoint = tip.position;
        midPoint = lastPoint;

        SetEmission(activeTrigger ? module.primaryColor : module.secondaryColor);
    }

    public void Reset() {
        active = false;
        canRestart = false;
        numPointsStored = 0;
        swirling = false;
        rollingIndex = 0;
        SetEmission(black);
        root.Reset();
        for (var index = 0; index < spells.Count; index++) {
            spells[index].OnReset();
        }
        ClearTarget();
    }

    public void SetTrail(bool state, Tuple<Color, Color> trailColors = null, Gradient gradient = null) {
        trail.emitting = !debug && state;

        if (trailColors != null) {
            trail.material?.SetColor(ColorStart, trailColors.Item1);
            trail.material?.SetColor(ColorEnd, trailColors.Item2);
        }
    }

    public Entity TargetEntity(Args args = null) {
        target = GetTargetEntity();

        if (target == null) return null;

        var line = module.targetLineEffectData.Spawn(transform);
            
        PlayCastEffect(args?.gradient ?? module.primaryGradient);

        lineSource.SetPositionAndRotation(tip.position, tip.rotation);
        line.SetSource(lineSource);
        line.SetTarget(target.Transform);
        if (args?.gradient is Gradient gradient) {
            line.SetMainGradient(gradient);
        }

        PlaySound(SoundType.Ket);
        line.Play();
        //module.SpawnShockwave(tip.position, tip.position - targetEntity.transform.position, 0.2f);
        module.castEffectData.Spawn(tip).Play();
        module.targetEffectData.Spawn(target.Transform).Play();

        return target;
    }

    public Entity GetTargetEntity(
        Vector3? direction = null,
        bool creatureOnly = false,
        bool itemOnly = false,
        bool preferCreature = false,
        bool preferLive = false) {
        var boundsSet = new List<Tuple<Bounds,Entity>>();
        var ray = direction == null ? tipRay : new Ray(tip.transform.position, (Vector3)direction);

        float maxDistance = Physics.Raycast(ray, out RaycastHit hit, Utils.GetMask(LayerName.None)) ? hit.distance : Mathf.Infinity;

        //Viz.Lines("wand target line")
        //    .SetPoints(tipRay.origin, tipRay.GetPoint(maxDistance == Mathf.Infinity ? 100 : maxDistance))
        //    .Color(Color.blue)
        //    .Show();

        if (itemOnly != true) {
            for (var index = 0; index < Creature.allActive.Count; index++) {
                var creature = Creature.allActive[index];
                if (creature.isCulled
                    || creature.isPlayer
                    || !creature.initialized
                    || creature.ragdoll.state == Ragdoll.State.Disabled) continue;

                var torso = creature.GetTorso();

                var tipToCreature = torso.transform.position - ray.origin;
                float creatureDistance = tipToCreature.magnitude;

                float angleToCreature = Vector3.Angle(ray.direction, tipToCreature);
                if (creatureDistance > maxDistance || (creatureDistance > 2 && angleToCreature > 20))
                    continue;

                var bounds = new Bounds(torso.transform.position, Vector3.one * 0.3f);
                if (!creature.GetHead().isSliced && !creature.GetHead().parentPart.isSliced) {
                    bounds.Encapsulate(new Bounds(creature.GetHead().transform.position, Vector3.one * 0.3f));
                }


                var entity = creature.gameObject.GetOrAddComponent<Entity>();
                //var box = Viz.Box(entity, "bounds").Size(bounds.size).Color(Color.green);
                //box.transform.SetParent(entity.Transform);
                //box.transform.localPosition = entity.transform.InverseTransformPoint(bounds.center);
    
                boundsSet.Add(Tuple.Create(bounds, entity));
            }
        }

        if (creatureOnly != true && (preferCreature != true || boundsSet.Count == 0)) {
            for (var index = 0; index < Item.allActive.Count; index++) {
                var otherItem = Item.allActive[index];

                if (otherItem.isCulled
                    || otherItem.rb.isKinematic
                    || otherItem.mainHandler != null
                    || otherItem.holder != null
                    || otherItem == item) continue;

                var handToItem = otherItem.transform.TransformPoint(otherItem.GetLocalCenter()) - ray.origin;
                float itemDistance = handToItem.magnitude;
                float angleToItem = Vector3.Angle(ray.direction, handToItem);

                if (itemDistance > maxDistance || (itemDistance > 2 && angleToItem > 20))
                    continue;

                var bounds = new Bounds(otherItem.transform.position, Vector3.zero);
                for (var i = 0; i < otherItem.allColliders.Length; i++) {
                    if (!otherItem.allColliders[i].Item2) continue;
                    var collider = otherItem.allColliders[i].Item1;
                    bounds.Encapsulate(collider.bounds);
                }


                var entity = otherItem.gameObject.GetOrAddComponent<Entity>();
                //var box = Viz.Box(entity, "bounds").Size(bounds.size).Color(Color.green);
                //box.transform.SetParent(entity.Transform);
                //box.transform.localPosition = entity.transform.InverseTransformPoint(bounds.center);

                boundsSet.Add(Tuple.Create(bounds, entity));
            }
        }

        Entity outEntity = null;

        float maxAngle = Mathf.Infinity;
        Tuple<Bounds,Entity> closestIntersect = null;
        
        for (var i = 0; i < boundsSet.Count; i++) {
            var (bounds, entity) = boundsSet[i];

            var pointOnLine = Utils.ClosestPointOnLine(ray.origin, ray.direction, bounds.center);

            var closestPointToLine = bounds.ClosestPoint(pointOnLine);

            // if (preferLive && outEntity is { creature.isKilled: false } && entity is { creature.isKilled: true }) {
            //     continue;
            // }

            //Viz.Lines(entity, "closestPoint").Color(Color.white).SetPoints(closestPointToLine, pointOnLine);
            
            // If it is too far away, skip it
            if (Vector3.Distance(closestPointToLine, ray.origin) > maxDistance) {
                //Viz.Box(entity, "bounds").Color(Color.grey);
                continue;
            }
                
            // If the target ray directly intersects, prioritize it
            if (bounds.IntersectRay(ray, out float distance) && distance < maxDistance) {
                maxDistance = distance;
                closestIntersect = boundsSet[i];
                outEntity = entity;
                //Viz.Box(entity, "bounds").Color(Color.red);
                continue;
            }

            // If we have found any objects that directly intersect, ignore the 'closest guess' algorithm below
            if (closestIntersect != null) {
                //Viz.Box(entity, "bounds").Color(Color.black);
                continue;
            }

            var angle = Vector3.Angle(closestPointToLine - ray.origin, ray.direction);
            
            if (angle < 10 && angle < maxAngle) {
                maxAngle = angle;
                outEntity = entity;
            }
        }

        return outEntity;
    }

    public Entity TargetCreature(Args args = null) {
        var creature = Utils.TargetCreature(tipRay, 15, 40, null, false);

        if (creature) {
            target = creature.gameObject.GetOrAddComponent<Entity>();
        }

        if (target != null) {
            var line = module.targetLineEffectData.Spawn(transform);
            line.SetSource(tip);
            line.SetTarget(target.Transform);
            if (args?.gradient is Gradient gradient) {
                line.SetMainGradient(gradient);
            }

            line.Play();
                
            //module.SpawnShockwave(tip.position, tip.position - targetEntity.transform.position, 0.2f);
            module.castEffectData.Spawn(tip).Play();
            module.targetEffectData.Spawn(target.Transform).Play();
        }

        return target;
    }

    public void ClearTarget() {
        if (target == null) return;
        if (target.Rigidbody() != null && target.throwOnRelease) {
            target.throwOnRelease = false;
            target.item?.Throw(1, Item.FlyDetection.Forced);
            target.Rigidbody()
                .AddForce(target.Rigidbody().HomingThrow(tipVelocity * (target.isCreature ? 15 : 5), 10), ForceMode.VelocityChange);
        }

        if (target.shouldRelease) {
            target.Release();
        }

        target = null;
    }


    public void Flamethrower() { StartCoroutine(FlamethrowerRoutine()); }

    public IEnumerator FlamethrowerRoutine() {
        var flamethrower = module.flamethrowerEffectData.Spawn(tip);
        flamethrower.SetIntensity(1);
        flamethrower.Play();
        while (active) {
            yield return 0;
        }

        flamethrower.End();
    }

    public void PlaySound(SoundType sound, Transform transform = null) {
        //Catalog.GetData<EffectData>("WandGestureSound").Spawn(transform ?? tip.transform).Play(Animator.StringToHash(sound.ToString()));
    }

    public void OnTargetEntity(Action<Step> func) {
        func(targetedItem);
        func(targetedEnemy);
    }
}

public enum SoundType {
    Ragh,
    Legh,
    Docgh,
    Hagh,
    Foll,
    Yoh,
    Quough,
    Ket
}
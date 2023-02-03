using System;
using SequenceTracker;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace Wand {
    using NamedConditionSet = Tuple<string, Func<bool>[]>;
    using NamedCondition = Tuple<string, Func<bool>>;
    public class Args {
        public Gradient gradient;
    }

    public enum SwirlDirection {
        Clockwise,
        CounterClockwise,
        Either
    }

    public class Entity {
        public Creature creature;
        public CollisionHandler handler;
        public Item item;
        public bool isCreature = false;
        public bool throwOnRelease;
        public Transform transform;
        public bool shouldRelease = true;

        public Entity(Creature creature) {
            this.creature = creature;
            isCreature = true;
            transform = creature.GetTorso().transform;
        }

        public Entity(CollisionHandler handler) {
            this.handler = handler;
            item = handler.item;

            transform = item.rb.transform.Find("WandTransformCenter");
            if (!transform) {
                transform = new GameObject().transform;
                transform.position = item.rb.worldCenterOfMass;
                transform.SetParent(item.rb.transform);
            }

        }

        public Entity(Item item) {
            this.item = item;
            handler = item.mainCollisionHandler;
            transform = item.rb.transform.Find("WandTransformCenter");
            if (!transform) {
                transform = new GameObject().transform;
                transform.position = item.rb.worldCenterOfMass;
                transform.SetParent(item.rb.transform);
            }
        }

        public Rigidbody Rigidbody() {
            if (creature) {
                return creature.ragdoll.state == Ragdoll.State.NoPhysic
                    ? creature.locomotion.rb
                    : creature.GetTorso().rb;
            }

            return handler?.rb;
        }

        public Coroutine StartCoroutine(IEnumerator routine) {
            return item?.StartCoroutine(routine) ?? creature?.StartCoroutine(routine);
        }

        public void StopCoroutine(Coroutine routine) {
            item?.StopCoroutine(routine);
            creature?.StopCoroutine(routine);
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

        public void Grab(bool shouldRelease = true) {
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

        public void Release() {
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

        public Coroutine PullTowards(Vector3 position) {
            return StartCoroutine(PullTowardsRoutine(position));
        }

        protected IEnumerator PullTowardsRoutine(Vector3 position, float maxDuration = 10) {
            var pid = new RBPID(Rigidbody(), forceMode: ForceMode.Acceleration, maxForce: isCreature ? 15000 : 5000)
                .Position(50, 0, 5);
            yield return Utils.LoopOver(_ => {
                if (Vector3.Distance(transform.position, position) > 1)
                    pid.UpdateVelocity(position);
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
                Utils.HexColor(191, 0, 0, 6)),
        };

        public Args shoveArgs = new Args {
            gradient = Utils.FadeInOutGradient(
                Utils.HexColor(250, 80, 30, 6),
                Utils.HexColor(191, 0, 0, 6)),
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
        public ObjectPool<GameObject> objectPool;

        protected Color targetColor;
        protected Color actualColor;
        public float angleTurned;
        protected Vector3 lastUp;
        public bool canRestart = false;
        protected float lastCast = 0;
        protected Vector3[] rollingPoints;
        protected Vector3[] orderedRollingPoints;
        protected int numRollingPoints = 50;
        protected int numPointsStored = 0;
        protected int rollingIndex = 0;
        protected LineRenderer debugLine;
        protected TrailRenderer trail;
        protected VisualEffect vfx;
        protected Color black = Color.black;
        protected float lastPointRecorded;
        protected Vector3 midPoint;
        protected float midPointDistance;
        public float swirlAngle;
        protected float midPointDistanceAverageDeviation;
        protected Vector3 lastPoint;
        protected Vector3 playerTipPos;
        public RagdollHand holdingHand;
        public RagdollHand otherHand;
        protected Vector3 holdingHandViewVelocity;
        protected Vector3 otherHandViewVelocity;
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
            orderedRollingPoints = new Vector3[numRollingPoints];
            item = GetComponent<Item>();

            tip = item.colliderGroups[0].imbueShoot;
            debugLine = tip.gameObject.AddComponent<LineRenderer>();
            debugLine.startWidth = 0.001f;
            debugLine.endWidth = 0.001f;

            wandBase = item.GetCustomReference("WandBase");

            tipRay = new Ray();
            tipLookRay = new Ray();

            root = Step.Start(() => {
                item.Haptic(0.7f);
                SpawnNode();
            });

            button = root.Then(() => item.mainHandler?.playerHand?.controlHand.alternateUsePressed == true,
                "Button Pressed", runOnChange: false);

            trigger = root.Then(() => item.mainHandler?.playerHand?.controlHand.usePressed == true,
                "Trigger Pressed", runOnChange: false);

            targetedEntity = trigger
                .Then(Brandish())
                .Do(() => TargetEntity(module.targetArgs), "Target Entity");

            targetedEnemy = targetedEntity.Then(() => target?.creature != null, "Creature Targeted");
            targetedItem = targetedEntity.Then(() => target?.item != null, "Item Targeted");
            
            item.OnGrabEvent += (handle, hand) => Reset();
            item.OnHeldActionEvent += (hand, handle, action) => {
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
            playerTipPos = Player.local.transform.InverseTransformPoint(tipLookRay.origin);
            tipVelocity = item.rb.GetPointVelocity(tipLookRay.origin)
                          - Player.local.locomotion.rb.GetPointVelocity(tipLookRay.origin);
            localTipVelocity = tip.transform.InverseTransformVector(tipVelocity);
            tipViewVelocity = Player.local.head.transform.InverseTransformVector(tipVelocity);
            if (item.mainHandler) {
                holdingHand = item.mainHandler;
                otherHand = holdingHand.otherHand;
                holdingHandViewVelocity
                    = Player.local.head.transform.InverseTransformVector(item.mainHandler.Velocity());
                otherHandViewVelocity
                    = Player.local.head.transform.InverseTransformVector(item.mainHandler.otherHand.Velocity());
            }

            if (active) {
                angleTurned += Vector3.SignedAngle(lastUp, transform.up, transform.forward);
                lastUp = transform.up;
                if (Time.time - lastPointRecorded > 0.01f) {
                    CalculateRollingAverage();
                    rollingPoints[rollingIndex] = playerTipPos;
                    lastPointRecorded = Time.time;
                    if (numPointsStored > 1
                        && midPointDistance > 0.05f
                        && midPointDistance < 0.4f
                        && midPointDistanceAverageDeviation < 0.4f)
                        swirlAngle += Vector3.SignedAngle(lastPoint - midPoint, playerTipPos - midPoint,
                            midPoint - Player.local.transform.InverseTransformPoint(wandBase.position));
                    lastPoint = rollingPoints[rollingIndex];
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
            item.renderers[0].materials[0].SetColor(EmissionColor, actualColor);

            for (var index = 0; index < spells.Count; index++) {
                spells[index].OnUpdate();
            }

            if (root.AtEnd() && canRestart && Time.time - lastCast > 0.5f && tipVelocity.magnitude < 1) {
                Reset();
                Begin(activeTrigger);
            }
        }

        public void PlayCastEffect(Color color) {
            var effect = item.GetCustomReference<VisualEffect>("CastEffect");
            effect.SetVector4("Color", color);
            effect.Play();
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
                case SwirlDirection.Clockwise:
                    return Tuple.Create("Swirl CW", new Func<bool>[] {
                        () => swirlAngle > 180 * amount * 2,
                        () => swirlAngle > 180 * (amount * 2 + 1)
                    });
                case SwirlDirection.CounterClockwise:
                    return Tuple.Create("Swirl CCW", new Func<bool>[] {
                        () => swirlAngle < -180 * amount * 2,
                        () => swirlAngle < -180 * (amount * 2 + 1)
                    });
                case SwirlDirection.Either:
                    return Tuple.Create("Swirl", new Func<bool>[] {
                        () => Mathf.Abs(swirlAngle) > 180 * amount * 2,
                        () => Mathf.Abs(swirlAngle) > 180 * (amount * 2 + 1)
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
                case SwirlDirection.Clockwise:
                    return Tuple.Create($"Twist CW to {degrees}", new Func<bool>[] {
                        () => angleTurned > -degrees && tipVelocity.magnitude < 1
                    });
                case SwirlDirection.CounterClockwise:
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

        public void CalculateRollingAverage() {
            float newMidPointDistance = 0;
            Vector3 newMidPoint = Vector3.zero;
            midPointDistanceAverageDeviation = 0;
            int numPoints = Mathf.Min(numRollingPoints, numPointsStored);
            for (int i = 0; i < numPoints; i++) {
                orderedRollingPoints[i]
                    = Player.local.transform.TransformPoint(rollingPoints[(rollingIndex + i) % numRollingPoints]);
                float distance = Vector3.Distance(midPoint, rollingPoints[i]);
                newMidPointDistance += distance;
                newMidPoint += rollingPoints[i];
                midPointDistanceAverageDeviation += Mathf.Abs(midPointDistance - distance) / midPointDistance;
            }

            midPoint = newMidPoint / numPoints;
            midPointDistance = newMidPointDistance / numPoints;
            midPointDistanceAverageDeviation /= numPoints;
        }

        public void SetEmission(Color color) => targetColor = color;

        public void Begin(bool trigger) {
            if (active) return;
            activeTrigger = trigger;
            active = true;
            lastUp = transform.up;
            angleTurned = 0;

            swirlAngle = 0;
            numPointsStored = 0;
            rollingIndex = 0;
            midPointDistance = 0;
            midPointDistanceAverageDeviation = 0;
            lastPoint = playerTipPos;
            midPoint = lastPoint;

            SetEmission(activeTrigger ? module.primaryColor : module.secondaryColor);
        }

        public void Reset() {
            active = false;
            canRestart = false;
            SetEmission(black);
            root.Reset();
            ClearTarget();
            for (var index = 0; index < spells.Count; index++) {
                spells[index].OnReset();
            }
        }

        public void SetTrail(bool state, Tuple<Color, Color> trailColors = null, Gradient gradient = null) {
            trail.emitting = state;

            if (trailColors != null) {
                trail.material?.SetColor(ColorStart, trailColors.Item1);
                trail.material?.SetColor(ColorEnd, trailColors.Item2);
            }
        }

        public Entity TargetEntity(Args args = null) {
            target = GetTargetEntity();

            if (target == null) return null;

            var line = module.targetLineEffectData.Spawn(transform);
            
            PlayCastEffect(args?.gradient.Evaluate(0.5f) ?? module.primaryColor);

            line.SetSource(tip);
            line.SetTarget(target.transform);
            if (args?.gradient is Gradient gradient) {
                line.SetMainGradient(gradient);
            }

            PlaySound(SoundType.Ket);
            line.Play();
            //module.SpawnShockwave(tip.position, tip.position - targetEntity.transform.position, 0.2f);
            module.castEffectData.Spawn(tip).Play();
            module.targetEffectData.Spawn(target.transform).Play();

            return target;
        }

        public Entity GetTargetEntity() {
            var boundsSet = new List<Tuple<Bounds,Entity>>();

            float maxDistance = Physics.Raycast(tipRay, out RaycastHit hit, Utils.GetMask(LayerName.None)) ? hit.distance : Mathf.Infinity;
            
            for (var index = 0; index < Creature.allActive.Count; index++) {
                var creature = Creature.allActive[index];
                if (creature.isCulled
                    || creature.isPlayer
                    || !creature.initialized
                    || creature.ragdoll.state == Ragdoll.State.Disabled) continue;

                var torso = creature.GetTorso();

                var tipToCreature = torso.transform.position - tipRay.origin;
                float creatureDistance = tipToCreature.magnitude;

                float angleToCreature = Vector3.Angle(tipRay.direction, tipToCreature);
                if (creatureDistance > maxDistance || (creatureDistance > 2 && angleToCreature > 20))
                    continue;
                
                var bounds = new Bounds(torso.transform.position, Vector3.zero);
                for (var i = 0; i < torso.colliderGroup.colliders.Count; i++) {
                    if (!torso.colliderGroup.colliders[i]) continue;
                    var collider = torso.colliderGroup.colliders[i];
                    bounds.Encapsulate(collider.bounds);
                }
                bounds.Expand(0.2f);
                boundsSet.Add(Tuple.Create(bounds, new Entity(creature)));
            }

            for (var index = 0; index < Item.allActive.Count; index++) {
                var otherItem = Item.allActive[index];

                if (otherItem.isCulled
                    || otherItem.rb.isKinematic
                    || otherItem.mainHandler != null
                    || otherItem.holder != null
                    || otherItem == item) continue;
                
                var handToItem = otherItem.transform.TransformPoint(otherItem.GetLocalCenter()) - tipRay.origin;
                float itemDistance = handToItem.magnitude;
                float angleToItem = Vector3.Angle(tipRay.direction, handToItem);

                if (itemDistance > maxDistance || (itemDistance > 2 && angleToItem > 20))
                    continue;

                var bounds = new Bounds(otherItem.transform.position, Vector3.zero);
                for (var i = 0; i < otherItem.allColliders.Length; i++) {
                    if (!otherItem.allColliders[i].Item2) continue;
                    var collider = otherItem.allColliders[i].Item1;
                    bounds.Encapsulate(collider.bounds);
                }
                
                boundsSet.Add(Tuple.Create(bounds, new Entity(otherItem)));
            }

            Entity outEntity = null;
            
            for (var i = 0; i < boundsSet.Count; i++) {
                var (bounds, entity) = boundsSet[i];
                
                if (Vector3.Distance(bounds.center, tipRay.origin) > maxDistance) continue;
                
                bool intersectsDirectly = bounds.IntersectRay(tipRay, out float distance) && distance < maxDistance;

                if (intersectsDirectly
                    || Vector3.Angle(
                        bounds.ClosestPoint(tipRay.GetPoint(Vector3.Distance(bounds.center, tipRay.origin)))
                        - tipRay.origin, tipRay.direction)
                    < 5) {
                    maxDistance = distance;
                    outEntity = entity;
                }
            }

            return outEntity;
        }

        public Entity TargetCreature(Args args = null) {
            var creature = Utils.TargetCreature(tipRay, 15, 40, null, false);

            if (creature) {
                target = new Entity(creature);
            }

            if (target != null) {
                var line = module.targetLineEffectData.Spawn(transform);
                line.SetSource(tip);
                line.SetTarget(target.transform);
                if (args?.gradient is Gradient gradient) {
                    line.SetMainGradient(gradient);
                }

                line.Play();
                
                //module.SpawnShockwave(tip.position, tip.position - targetEntity.transform.position, 0.2f);
                module.castEffectData.Spawn(tip).Play();
                module.targetEffectData.Spawn(target.transform).Play();
            }

            return target;
        }

        public void ClearTarget() {
            if (target == null) return;
            if (target.Rigidbody() != null && target.throwOnRelease) {
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
}
// lyneca
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

using ExtensionMethods;
using System.Collections;
using System.Security.Cryptography;
using UnityEngine.AI;
using UnityEngine.PlayerLoop;
using Action = System.Action;
using Debug = UnityEngine.Debug;
using Object = System.Object;
using Random = UnityEngine.Random;

namespace ExtensionMethods {
    public enum FingerPart {
        Proximal,
        Intermediate,
        Distal
    }

    static class ExtensionMethods {
        /// <summary>Get raw angular velocity of the player hand</summaryt

        public static int Capacity(this Holder holder) => holder.data.maxQuantity;

        /// <summary>
        ///  Get hand local angular velocity
        /// </summary>
        public static Vector3 LocalAngularVelocity(this RagdollHand hand)
            => hand.transform.InverseTransformDirection(hand.rb.angularVelocity);

        public static Task<TOutput> Then<TInput, TOutput>(this Task<TInput> task, Func<TInput, TOutput> func) {
            return task.ContinueWith((input) => func(input.Result));
        }

        public static Task Then(this Task task, Action<Task> func) { return task.ContinueWith(func); }

        public static Task Then<TInput>(this Task<TInput> task, Action<TInput> func) {
            return task.ContinueWith((input) => func(input.Result));
        }

        /// <summary>
        /// Get a component from the gameobject, or create it if it doesn't exist
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
        }

        /// <summary>
        /// Force this WhooshPoint to play its effect
        /// </summary>
        public static void Play(this WhooshPoint point) {
            if ((point.GetField("trigger") is WhooshPoint.Trigger trigger)
                && trigger != WhooshPoint.Trigger.OnGrab
                && point.GetField("effectInstance") != null)
                (point.GetField("effectInstance") as EffectInstance)?.Play();
            Utils.SetField(point, "effectActive", true);
            Utils.SetField(point, "dampenedIntensity", 0);
        }
        public static void Stop(this WhooshPoint point) {
            if ((point.GetField("trigger") is WhooshPoint.Trigger trigger)
                && trigger != WhooshPoint.Trigger.OnGrab
                && point.GetField("effectInstance") != null)
                (point.GetField("effectInstance") as EffectInstance)?.SetIntensity(0);
            Utils.SetField(point, "effectActive", false);
            Utils.SetField(point, "dampenedIntensity", 0);
        }

        /// <summary>
        /// Attempt to point an item's FlyDirRef at a target vector
        /// </summary>
        /// <param name="target">Target vector</param>
        /// <param name="lerpFactor">Lerp factor (if you're calling over multiple frames)</param>
        /// <param name="upDir">Up direction</param>
        public static void PointItemFlyRefAtTarget(
            this Item item,
            Vector3 target,
            float lerpFactor,
            Vector3? upDir = null) {
            Vector3 up = upDir ?? Vector3.up;
            if (item.flyDirRef) {
                item.transform.rotation = Quaternion.Slerp(
                                              item.transform.rotation * item.flyDirRef.localRotation,
                                              Quaternion.LookRotation(target, up),
                                              lerpFactor)
                                          * Quaternion.Inverse(item.flyDirRef.localRotation);
            } else if (item.holderPoint) {
                item.transform.rotation = Quaternion.Slerp(
                                              item.transform.rotation * item.holderPoint.localRotation,
                                              Quaternion.LookRotation(target, up),
                                              lerpFactor)
                                          * Quaternion.Inverse(item.holderPoint.localRotation);
            } else {
                Quaternion pointDir = Quaternion.LookRotation(item.transform.up, up);
                item.transform.rotation
                    = Quaternion.Slerp(item.transform.rotation * pointDir, Quaternion.LookRotation(target, up),
                          lerpFactor)
                      * Quaternion.Inverse(pointDir);
            }
        }

        /// <summary>
        /// Is is this hand gripping?
        /// </summary>
        public static bool IsGripping(this RagdollHand hand) => hand?.playerHand?.controlHand?.gripPressed ?? false;
        public static Vector3 PalmDir(this RagdollHand hand) => hand.PalmDir;
        public static Vector3 PointDir(this RagdollHand hand) => hand.PointDir;
        public static Vector3 ThumbDir(this RagdollHand hand) => hand.ThumbDir;
        public static void HapticTick(this RagdollHand hand, float intensity = 1, float frequency = 10, int count = 1) {
            
            PlayerControl.input.Haptic(hand.side, intensity, frequency);
            if (count > 1) {
                for (int i = 0; i < count - 1; i++) {
                    PlayerControl.local.RunAfter(() => PlayerControl.input.Haptic(hand.side, intensity, frequency), 0.07f * count);
                }
            }
        }

        public static void PlayHapticClipOver(this RagdollHand hand, AnimationCurve curve, float duration) {
            hand.StartCoroutine(HapticPlayer(hand, curve, duration));
        }

        public static IEnumerator HapticPlayer(RagdollHand hand, AnimationCurve curve, float duration) {
            var time = Time.time;
            while (Time.time - time < duration) {
                hand.HapticTick(curve.Evaluate((Time.time - time) / duration));
                yield return 0;
            }
        }

        /// <summary>
        /// Return the minimum entry in an interator using a custom comparable function
        /// </summary>
        public static T MinBy<T>(this IEnumerable<T> enumerable, Func<T, IComparable> comparator) {
            if (!enumerable.Any())
                return default;
            return enumerable.Aggregate((curMin, x)
                => (curMin == null || (comparator(x).CompareTo(comparator(curMin)) < 0)) ? x : curMin);
        }

        /// <summary>
        /// .Select(), but only when the output of the selection function is non-null
        /// </summary>
        public static IEnumerable<TOut> SelectNotNull<TIn, TOut>(this IEnumerable<TIn> enumerable, Func<TIn, TOut> func)
            => enumerable.Where(item => func(item) != null).Select(func);

        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> enumerable)
            => enumerable.Where(item => item != null);

        /// <summary>
        /// Get a point above the player's hand
        /// </summary>
        public static void ForBothColliderGroups(this CollisionInstance hit, Action<ColliderGroup> func) {
            func(hit.targetColliderGroup);
            func(hit.sourceColliderGroup);
        }

        public static float NegPow(this float input, float power) => Mathf.Pow(input, power) * (input / Mathf.Abs(input));
        public static float Sign(this float input) => input > 0 ? 1 : input < 0 ? -1 : 0;
        public static float Pow(this float input, float power) => Mathf.Pow(input, power);
        public static float Sqrt(this float input) => Mathf.Sqrt(input);
        public static float Clamp01(this float input) => Mathf.Clamp01(input);
        public static float Clamp(this float input, float low, float high) => Mathf.Clamp(input, low, high);
        public static float Remap(this float input, float inLow, float inHigh, float outLow, float outHigh)
            => (input - inLow) / (inHigh - inLow) * (outHigh - outLow) + outLow;

        public static float RemapClamp(this float input, float inLow, float inHigh, float outLow, float outHigh)
            => (Mathf.Clamp(input, inLow, inHigh) - inLow) / (inHigh - inLow) * (outHigh - outLow) + outLow;

        public static float Remap01(this float input, float inLow, float inHigh) => (input - inLow) / (inHigh - inLow);

        public static float RemapClamp01(this float input, float inLow, float inHigh)
            => (Mathf.Clamp(input, inLow, inHigh) - inLow) / (inHigh - inLow);

        public static float OneMinus(this float input) => Mathf.Clamp01(1 - input);

        public static float Randomize(this float input, float range) => input * Random.Range(1f - range, 1f + range);

        public static float Curve(this float time, params float[] values) {
            var curve = new AnimationCurve();
            int i = 0;
            foreach (var value in values) {
                curve.AddKey(i / ((float) values.Length - 1), value);
                i++;
            }

            return curve.Evaluate(time);
        }

        public static float MapOverCurve(this float time, params Tuple<float, float>[] points) {
            var curve = new AnimationCurve();
            foreach (var point in points) {
                curve.AddKey(new Keyframe(point.Item1, point.Item2));
            }

            return curve.Evaluate(time);
        }

        public static float MapOverCurve(this float time, params Tuple<float, float, float, float>[] points) {
            var curve = new AnimationCurve();
            foreach (var point in points) {
                curve.AddKey(new Keyframe(point.Item1, point.Item2, point.Item3, point.Item4));
            }

            return curve.Evaluate(time);
        }

        public static Vector3 BezierMap(this float time, Vector3 A, Vector3 B, Vector3 C, Vector3 D) {
            var Q = Vector3.Lerp(A, B, time);
            var R = Vector3.Lerp(B, C, time);
            var S = Vector3.Lerp(C, D, time);
            var P = Vector3.Lerp(Q, R, time);
            var T = Vector3.Lerp(R, S, time);
            return Vector3.Lerp(P, T, time);
        }

        /// <summary>
        /// Vector pointing in the direction of the thumb
        /// </summary>
        /// <summary>
        /// Clamp a number between -1000 and 1000, just in case
        /// </summary>
        public static float SafetyClamp(this float num, float max = 1000) => Mathf.Clamp(num, -max, max);

        /// <summary>
        /// I miss Rust's .abs()
        /// </summary>
        public static float Abs(this float num) => Mathf.Abs(num);

        /// <summary>
        /// float.SafetyClamp() but for vectors
        /// </summary>
        public static Vector3 SafetyClamp(this Vector3 vec, float max) => vec.normalized * vec.magnitude.SafetyClamp(max);

        public static Vector3 XZ(this Vector3 vec) => new Vector3(vec.x, 0, vec.z);

        public static bool Compare(this ViewDir dir, Vector3 vec, float amount) {
            return dir.Sign() switch {
                1 => vec.GetAxis(dir.GetAxis()) > amount,
                0 => vec.GetAxis(dir.GetAxis()).Abs() > amount,
                -1 => vec.GetAxis(dir.GetAxis()) < -amount,
                _ => false
            };
        }

        public static int Sign(this ViewDir dir) {
            return dir switch {
                ViewDir.Right or ViewDir.Up or ViewDir.Forward => 1,
                ViewDir.Left or ViewDir.Down or ViewDir.Back => -1,
                _ => 0
            };
        }

        public static Axis GetAxis(this ViewDir dir) {
            return dir switch {
                ViewDir.X or ViewDir.Left or ViewDir.Right => Axis.X,
                ViewDir.Y or ViewDir.Up or ViewDir.Down => Axis.Y,
                ViewDir.Z or ViewDir.Forward or ViewDir.Back => Axis.Z,
                _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
            };
        }
        
        public static float GetAxis(this Vector3 vec, Axis axis) {
            return axis switch {
                Axis.X => vec.x,
                Axis.Y => vec.y,
                Axis.Z => vec.z,
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            };
        }

        public static bool Mostly(this Vector3 vec, Axis axis) {
            return axis switch {
                Axis.X => vec.MostlyX(),
                Axis.Y => vec.MostlyY(),
                Axis.Z => vec.MostlyZ(),
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            };
        }

        public static bool InDirection(this Vector3 vec, ViewDir direction, float amount = 0) {
            return vec.Mostly(direction.GetAxis()) && direction.Compare(vec, amount);
        }

        /// <summary>
        /// Returns true if the vector's X component is its largest component
        /// </summary>
        public static bool MostlyX(this Vector3 vec) => vec.x.Abs() > vec.y.Abs() && vec.x.Abs() > vec.z.Abs();

        /// <summary>
        /// Returns true if the vector's Y component is its largest component
        /// </summary>
        public static bool MostlyY(this Vector3 vec) => vec.y.Abs() > vec.x.Abs() && vec.y.Abs() > vec.z.Abs();

        /// <summary>
        /// Returns true if the vector's Z component is its largest component
        /// </summary>
        public static bool MostlyZ(this Vector3 vec) => vec.z.Abs() > vec.x.Abs() && vec.z.Abs() > vec.y.Abs();

        public static string Join(this string delimiter, IEnumerable<object> strings) {
            return string.Join(delimiter, strings.Where(str => str != null).ToList());
        }

        public static string Join(this string delimiter, params object[] strings) {
            return string.Join(delimiter, strings.Where(str => str != null).ToList());
        }

        /// <summary>
        /// Get a creature's part from a PartType
        /// </summary>
        public static RagdollPart GetPart(this Creature creature, RagdollPart.Type partType)
            => creature.ragdoll.GetPart(partType);

        /// <summary>
        /// Get a creature's head
        /// </summary>
        public static RagdollPart GetHead(this Creature creature) => creature.ragdoll.headPart;

        /// <summary>
        /// Get a creature's torso
        /// </summary>
        public static RagdollPart GetTorso(this Creature creature) => creature.GetPart(RagdollPart.Type.Torso);

        public static Vector3 GetChest(this Creature creature) => Vector3.Lerp(creature.GetTorso().transform.position,
            creature.GetHead().transform.position, 0.5f);

        public static float HandVelocityInLocalDirection(this RagdollHand hand, Vector3 direction) {
            return Vector3.Dot(hand.Velocity(), hand.transform.TransformDirection(direction));
        }

        public static float HandVelocityInDirection(this RagdollHand hand, Vector3 direction) {
            return Vector3.Dot(hand.Velocity(), direction);
        }

        public static Vector3 Rotated(this Vector3 vector, Quaternion rotation, Vector3 pivot = default) {
            return rotation * (vector - pivot) + pivot;
        }

        public static Side Other(this Side side) { return side == Side.Left ? Side.Right : Side.Left; }

        public static Vector3 Rotated(this Vector3 vector, Vector3 rotation, Vector3 pivot = default) {
            return Rotated(vector, Quaternion.Euler(rotation), pivot);
        }

        public static Vector3 Rotated(this Vector3 vector, float x, float y, float z, Vector3 pivot = default) {
            return Rotated(vector, Quaternion.Euler(x, y, z), pivot);
        }

        public static bool IsFacing(this Vector3 source, Vector3 other, float angle = 50) => Vector3.Angle(source, other) < angle;

        public static void SetPosition(this EffectInstance instance, Vector3 position) {
            instance.effects.ForEach(effect => effect.transform.position = position);
        }

        public static void SetRotation(this EffectInstance instance, Quaternion rotation) {
            instance.effects.ForEach(effect => effect.transform.rotation = rotation);
        }

        public static void SetScale(this EffectInstance instance, Vector3 scale) {
            foreach (var effect in instance.effects) {
                if (effect is EffectMesh mesh) {
                    mesh.transform.localScale = scale;
                    mesh.meshSize = scale;
                }
            }
        }

        public static float Distance(this NavMeshPath path) {
            Vector3 lastPos = default;
            bool started = false;
            float distance = 0;
            foreach (var corner in path.corners) {
                if (!started) {
                    lastPos = corner;
                    started = true;
                } else {
                    distance += Vector3.Distance(corner, lastPos);
                    lastPos = corner;
                }
            }

            return distance;
        }

        public static Vector3 Lerp(this NavMeshPath path, float t, int stepsAway = 0) {
            float amount = path.Distance() * t;
            float travelled = 0;
            switch (path.corners.Length - stepsAway) {
                case 0:
                    return Vector3.zero;
                case 1:
                    return path.corners[0];
                case 2:
                    return Vector3.Lerp(path.corners[0], path.corners[1], t);
            }

            for (int i = 0; i < path.corners.Length - (1 + stepsAway); i++) {
                float distance = Vector3.Distance(path.corners[i], path.corners[i + 1]);
                if (distance + travelled > amount) {
                    return Vector3.Lerp(path.corners[i], path.corners[i + 1],
                        Mathf.InverseLerp(0, distance, amount - travelled));
                } else {
                    travelled += distance;
                }
            }

            return path.corners[path.corners.Length - stepsAway];
        }

        public static Coroutine RunCoroutine(this MonoBehaviour mono, Func<IEnumerator> function, float delay = 0) {
            if (mono.isActiveAndEnabled) {
                return mono.StartCoroutine(RunAfterCoroutine(function, delay));
            }

            return null;
        }

        public static Coroutine RunAfter(this MonoBehaviour mono, System.Action action, float delay = 0) {
            if (mono.isActiveAndEnabled) {
                return mono.StartCoroutine(RunAfterCoroutine(action, delay));
            }

            return null;
        }

        public static Coroutine RunNextFrame(this MonoBehaviour mono, System.Action action) {
            if (mono.isActiveAndEnabled) {
                return mono.StartCoroutine(RunNextFrameCoroutine(action));
            }

            return null;
        }

        public static IEnumerator RunAfterCoroutine(Func<IEnumerator> function, float delay) {
            yield return new WaitForSeconds(delay);
            yield return function();
        }

        public static IEnumerator RunAfterCoroutine(System.Action action, float delay) {
            yield return new WaitForSeconds(delay);
            action();
        }

        public static IEnumerator RunNextFrameCoroutine(System.Action action) {
            yield return 0;
            action();
        }

        public static GameObject AddComponents<T>(this GameObject obj, Action<T> callback) where T : Component {
            callback(obj.AddComponent<T>());
            return obj;
        }

        public static RagdollHand.Finger GetFinger(this RagdollHand hand, Finger finger) {
            switch (finger) {
                case Finger.Thumb:
                    return hand.fingerThumb;
                case Finger.Index:
                    return hand.fingerIndex;
                case Finger.Middle:
                    return hand.fingerMiddle;
                case Finger.Ring:
                    return hand.fingerRing;
                case Finger.Little:
                    return hand.fingerLittle;
            }

            return null;
        }

        public static Transform GetFingerPart(this RagdollHand.Finger finger, FingerPart part) {
            switch (part) {
                case FingerPart.Proximal:
                    return finger.proximal.collider.transform;
                case FingerPart.Intermediate:
                    return finger.intermediate.collider.transform;
                case FingerPart.Distal:
                    return finger.distal.collider.transform;
            }

            return null;
        }

        public static object Call(this object o, string methodName, params object[] args) {
            var mi = o.GetType().GetMethod(methodName, BindingFlags.Instance);
            if (mi != null) {
                return mi.Invoke(o, args);
            }

            return null;
        }

        // This method is ILLEGAL
    public static object CallPrivate(this object o, string methodName, params object[] args) {
        var mi = o.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi != null) {
            return mi.Invoke(o, args);
        }

        return null;
    }

    public static object GetField(this object instance, string fieldName) {
        if (instance == null) return null;
        BindingFlags bindFlags = BindingFlags.Instance
                                 | BindingFlags.Public
                                 | BindingFlags.NonPublic
                                 | BindingFlags.Static;
        FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
        return field.GetValue(instance);
    }
    public static T GetField<T>(this object instance, string fieldName) {
        if (instance == null) return default;
        BindingFlags bindFlags = BindingFlags.Instance
                                 | BindingFlags.Public
                                 | BindingFlags.NonPublic
                                 | BindingFlags.Static;
        FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
        return (T)field.GetValue(instance);
    }

        public static void SetSpring(this ConfigurableJoint joint, float spring) {
            var xDrive = joint.xDrive;
            xDrive.positionSpring = spring;
            joint.xDrive = xDrive;
            var yDrive = joint.yDrive;
            yDrive.positionSpring = spring;
            joint.yDrive = yDrive;
            var zDrive = joint.zDrive;
            zDrive.positionSpring = spring;
            joint.zDrive = zDrive;
        }

        public static void SetDamping(this ConfigurableJoint joint, float damper) {
            var xDrive = joint.xDrive;
            xDrive.positionDamper = damper;
            joint.xDrive = xDrive;
            var yDrive = joint.yDrive;
            yDrive.positionDamper = damper;
            joint.yDrive = yDrive;
            var zDrive = joint.zDrive;
            zDrive.positionDamper = damper;
            joint.zDrive = zDrive;
        }

        public static float GetMassModifier(this Rigidbody rb) {
            if (rb.mass < 1) {
                return rb.mass * 3;
            } else {
                return rb.mass;
            }
        }

        public static float GetMassModifier(this Item item) {
            if (item.rb.mass < 1) {
                return item.rb.mass * 3;
            } else {
                return item.rb.mass;
            }
        }

        public static Item UnSnapOne(this Holder holder, bool silent) {
            Item obj = holder.items.LastOrDefault();
            if (obj)
                holder.UnSnap(obj, silent);
            return obj;
        }

        //public static Vector3 GetBounds(this Item item) {
        //    var filter = item.renderers
        //        .Select(renderer => renderer.gameObject.GetComponent<MeshFilter>()).OrderBy(meshFilter
        //            => (meshFilter.transform.position - item.transform.position + meshFilter.mesh.bounds.extents).magnitude)
        //        .Last();
        //    var localRotation = Quaternion.Inverse(item.transform.rotation) * filter.transform.rotation;
        //    return Quaternion.Inverse(localRotation) * filter.mesh.bounds.extents * filter.transform.localScale;
        //}

        public static Vector3 GetScaleRelativeTo(this Transform transform, Transform target) {
            Vector3 output = Vector3.one;
            var parent = transform;
            while (parent.parent != target && parent.parent != null) {
                output = output.MultiplyComponents(parent.localScale);
                parent = parent.parent;
            }

            return output;
        }


        public static Vector3 MultiplyComponents(this Vector3 a, Vector3 b)
            => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        public static float GetRadius(this Item item) => (item?.renderers?.Any() == true)
            ? item.renderers
                .Select(renderer => renderer.gameObject.GetComponent<MeshFilter>()).Max(meshFilter
                    => meshFilter.transform.GetScaleRelativeTo(item.transform).MultiplyComponents(
                            meshFilter.transform.position - item.transform.position + meshFilter.mesh.bounds.extents)
                        .magnitude).Clamp(0, 1f)
            : 0.5f;

        public static void Depenetrate(this Item item) {
            foreach (var handler in item.collisionHandlers) {
                foreach (var damager in handler.damagers) {
                    damager.UnPenetrateAll();
                }
            }
        }

        public static object GetVFXProperty(this EffectInstance effect, string name) {
            foreach (var fx in effect.effects) {
                if (fx is EffectVfx vfx) {
                    if (vfx.vfx.HasFloat(name)) return vfx.vfx.GetFloat(name);
                    if (vfx.vfx.HasVector3(name)) return vfx.vfx.GetVector3(name);
                    if (vfx.vfx.HasBool(name)) return vfx.vfx.GetBool(name);
                    if (vfx.vfx.HasInt(name)) return vfx.vfx.GetInt(name);
                }
            }

            return null;
        }

        public static void SetVFXProperty<T>(this EffectInstance effect, string name, T data) {
            if (effect == null) return;
            if (data is Vector3 vec3) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx vfx && vfx.vfx.HasVector3(name))) {
                    fx.vfx.SetVector3(name, vec3);
                }
            } else if (data is float flt) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx vfx && vfx.vfx.HasFloat(name))) {
                    fx.vfx.SetFloat(name, flt);
                }
            } else if (data is int integer) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx vfx && vfx.vfx.HasInt(name))) {
                    fx.vfx.SetInt(name, integer);
                }
            } else if (data is bool boolean) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx vfx && vfx.vfx.HasBool(name))) {
                    fx.vfx.SetBool(name, boolean);
                }
            } else if (data is Texture texture) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx vfx && vfx.vfx.HasTexture(name))) {
                    fx.vfx.SetTexture(name, texture);
                }
            }
        }

        public static Quaternion GetFlyDirRefLocalRotation(this Item item)
            => Quaternion.Inverse(item.transform.rotation) * item.flyDirRef.rotation;

        public static void AddModifier(
            this Rigidbody rb,
            object handler,
            int priority,
            float? gravity = null,
            float? drag = null,
            float? mass = null) {
            rb.gameObject.GetOrAddComponent<RigidbodyModifier>().AddModifier(handler, priority, gravity, drag, mass);
        }

        public static void RemoveModifier(
            this Rigidbody rb,
            object handler) {
            rb.gameObject.GetOrAddComponent<RigidbodyModifier>().RemoveModifier(handler);
        }

        public static string ListString<T>(this IEnumerable<T> list)
            => string.Join(", ", list.Select(e => e.ToString()));

        public static T RandomChoice<T>(this IEnumerable<T> list)
            => list.ElementAtOrDefault(Random.Range(0, list.Count() - 1));

        public static int AffectedHandlers(this Creature creature, IEnumerable<CollisionHandler> handlers) => Mathf.Max(creature
            ?.ragdoll.parts.SelectNotNull(part => part.collisionHandler).Intersect(handlers).Count() ?? 1, 1);

        public static int AffectedHandlers(this CollisionHandler handler, IEnumerable<CollisionHandler> handlers)
            => Mathf.Max(handler.ragdollPart?.ragdoll.creature?.ragdoll.parts
                             .SelectNotNull(part => part.collisionHandler)
                             .Intersect(handlers).Count()
                         ?? 1, 1);

        public static void IgnoreCollider(this Ragdoll ragdoll, Collider collider, bool ignore = true) {
            foreach (var part in ragdoll.parts) {
                part.IgnoreCollider(collider, ignore);
            }
        }

        public static void SliceAll(this Ragdoll ragdoll, float forceAway = 0) {
            ragdoll.headPart.parentPart.TrySlice();
            ragdoll.headPart.rb.AddForce(
                (ragdoll.headPart.transform.position - ragdoll.rootPart.transform.position).normalized * forceAway,
                ForceMode.VelocityChange);

            var parts = new List<RagdollPart> {
                ragdoll.creature.handLeft.upperArmPart,
                ragdoll.creature.handRight.upperArmPart,
                ragdoll.creature.footLeft.parentPart.parentPart,
                ragdoll.creature.footRight.parentPart.parentPart
            };

            foreach (var part in parts) {
                part.TrySlice();
                part.rb.AddForce(
                    (part.transform.position - ragdoll.rootPart.transform.position).normalized * forceAway,
                    ForceMode.VelocityChange);
            }
        }

        public static void IgnoreCollider(this RagdollPart part, Collider collider, bool ignore = true) {
            foreach (var itemCollider in part.colliderGroup.colliders) {
                Physics.IgnoreCollision(collider, itemCollider, ignore);
            }
        }

        public static bool Active(this Creature creature) => !creature.isKilled && !creature.isCulled;
        public static void IgnoreCollider(this Item item, Collider collider, bool ignore = true) {
            foreach (var cg in item.colliderGroups) {
                foreach (var itemCollider in cg.colliders) {
                    Physics.IgnoreCollision(collider, itemCollider, ignore);
                }
            }
        }

        public static bool Free(this Item item) => item.mainHandler == null
                                                   && item.holder == null
                                                   && !item.isGripped
                                                   && !item.isTelekinesisGrabbed;
        public static void SafeDespawn(this Item item, float delay) {
            item.RunAfter(() => {
                item.handlers.ToList().ForEach(handler => handler.UnGrab(false));
                item.handles.ToList().ForEach(handle => {
                    handle.SetTouch(false);
                    handle.SetTelekinesis(false);
                });
                item.Despawn();
            }, delay);
        }
        public static IEnumerable<string> Chop(this string str, int chunkSize) {
            for (int i = 0; i < str.Length; i += chunkSize)
                yield return str.Substring(i, chunkSize);
        }

        public static bool IsPlayer(this RagdollPart part) => part?.ragdoll?.creature.isPlayer == true;
        public static bool IsImportant(this RagdollPart part) {
            var type = part.type;
            return type == RagdollPart.Type.Head
                   || type == RagdollPart.Type.Torso
                   || type == RagdollPart.Type.LeftHand
                   || type == RagdollPart.Type.RightHand
                   || type == RagdollPart.Type.LeftFoot
                   || type == RagdollPart.Type.RightFoot;
        }

        public static T Clone<T>(this T obj) {
            var inst = obj
                .GetType()
                .GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            return (T) inst?.Invoke(obj, null);
        }
    }

    public static class Hands {
        public static bool Empty(this RagdollHand hand) {
            return !hand.caster.isFiring
                   && !hand.isGrabbed
                   && !hand.caster.isMerging
                   && !Player.currentCreature.mana.mergeActive
                   && hand.grabbedHandle == null
                   && hand.caster.telekinesis.catchedHandle == null;
        }

        public static void ForBothHands(Action<RagdollHand> action) {
            action(Left);
            action(Right);
        }

        public static bool Both(params Func<RagdollHand, bool>[] predicates)
            => predicates.All(pred => pred(Left) && pred(Right));

        public static bool Either(params Func<RagdollHand, bool>[] predicates)
            => predicates.Any(pred => pred(Left) && pred(Right));
        public static bool Gripping(this RagdollHand hand) => hand.IsGripping();
        public static bool Buttoning(this RagdollHand hand) => hand.playerHand?.controlHand?.alternateUsePressed ?? false;
        public static Ray PointRay(this RagdollHand hand) => new Ray(hand.IndexTip().position, hand.PointDir());
        public static bool Triggering(this RagdollHand hand) => hand?.playerHand?.controlHand?.usePressed ?? false;
        public static Vector3 ArmToHand(this RagdollHand hand)
            => hand.transform.position - hand.lowerArmPart.transform.position;
        public static Vector3 PosAboveBackOfHand(this RagdollHand hand) => hand.transform.position
                                                                           - hand.transform.right * 0.1f
                                                                           + hand.transform.forward * 0.2f;

        public static Vector3 LocalVelocity(this RagdollHand hand)
            => hand.Velocity() - Player.local.locomotion.rb.GetPointVelocity(hand.transform.position);

        public static Vector3 ViewVelocity(this RagdollHand hand) => hand.LocalVelocity().WorldToViewSpace();
        public static Vector3 WorldToViewSpace(this Vector3 vec) => Player.local.head.transform.InverseTransformVector(vec);

        public static Vector3 WorldToViewPlaneSpace(this Vector3 vec)
            => Quaternion.Inverse(Player.local.head.transform.rotation) * vec;
        public static Vector3 LocalToViewSpace(this Vector3 vec) => Player.local.head.transform.TransformVector(vec);

        public static Vector3 LocalToViewPlaneSpace(this Vector3 vec) => Quaternion.LookRotation(
                                                                             Vector3.ProjectOnPlane(
                                                                                 Player.local.head.transform.forward,
                                                                                 Vector3.up),
                                                                             Vector3.up)
                                                                         * vec;
        public static Vector3 ToUnitVector(this ViewDir dir) => dir switch {
            ViewDir.Forward => Vector3.forward,
            ViewDir.Back => Vector3.back,
            ViewDir.Up => Vector3.up,
            ViewDir.Down => Vector3.down,
            ViewDir.Left => Vector3.left,
            ViewDir.Right => Vector3.right,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };

        public static Vector3 ToWorldViewVector(this ViewDir dir) => dir.ToUnitVector().LocalToViewSpace().normalized;
        public static Vector3 ToWorldViewPlaneVector(this ViewDir dir) => dir.ToUnitVector().LocalToViewPlaneSpace().normalized;
        public static Transform IndexTip(this RagdollHand hand) => hand.fingerIndex.distal.collider.transform;
        public static Vector3 Palm(this RagdollHand hand) => hand.transform.position + hand.PointDir() * 0.1f;
        public static Vector3 Velocity(this RagdollHand hand) {
            try {
                return Player.local.transform.rotation * hand.playerHand.controlHand.GetHandVelocity();
            } catch (NullReferenceException) {
                return Vector3.zero;
            }
        }
        public static SpellCastCharge GetSpell(this RagdollHand hand) => hand.caster.spellInstance as SpellCastCharge;
        public static bool Selected<T>(this RagdollHand hand) where T : SpellCastCharge
            => hand.caster.spellInstance is T;
        public static bool Selected(this RagdollHand hand, string id) => hand.caster.spellInstance?.id == id;
        public static bool Casting<T>(this RagdollHand hand) where T : SpellCastCharge
            => hand.caster.spellInstance is T && hand.caster.isFiring;
        public static RagdollHand GetHand(Side side) => Player.currentCreature.GetHand(side);
        public static float Distance() => Vector3.Distance(GetHand(Side.Left).Palm(), GetHand(Side.Right).Palm());
        public static Vector3 Midpoint() => Vector3.Lerp(GetHand(Side.Left).Palm(), GetHand(Side.Right).Palm(), 0.5f);

        public static Vector3 AverageVelocity()
            => Vector3.Slerp(Left.Velocity(), Right.Velocity(), 0.5f);
        public static Vector3 AveragePoint() => Vector3.Slerp(Left.PointDir(), Right.PointDir(), 0.5f);
        public static Vector3 AveragePalm() => Vector3.Slerp(Left.PalmDir(), Right.PalmDir(), 0.5f);
        public static Vector3 AverageThumb() => Vector3.Slerp(Left.ThumbDir(), Right.ThumbDir(), 0.5f);
        public static Vector3 LeftToRight() => Right.Palm() - Left.Palm();
        public static bool All(this RagdollHand hand, params Func<RagdollHand, bool>[] preds) => preds.All(pred => pred(hand));
        public static bool Any(this RagdollHand hand, params Func<RagdollHand, bool>[] preds) => preds.Any(pred => pred(hand));
        public static RagdollHand Right { get => Player.currentCreature.handRight; }
        public static RagdollHand Left { get => Player.currentCreature.handLeft; }
        public static bool FacingPos(this RagdollHand hand, Vector3 position, float angle = 50) => hand.FacingDir(position - hand.Palm(), angle);
        public static bool FacingDir(this RagdollHand hand, Vector3 direction, float angle = 50) => hand.PalmDir().IsFacing(direction, angle);
    }
}

static class Utils {
    public static Vector3 ClosestPointOnLine(Vector3 origin, Vector3 direction, Vector3 point) {
        direction.Normalize(); // this needs to be a unit vector
        var v = point - origin;
        float d = Vector3.Dot(v, direction);
        return origin + direction * d;
    }

    public static Color HexColor(float r, float g, float b, float intensity) {
        intensity = 2f.Pow(intensity);
        return new Color(r / 255f * intensity, g / 255f * intensity, b / 255f * intensity);
    }

    public static Gradient FadeInOutGradient(Color start, Color end) {
        return Gradient()
            .Alpha(0, 0)
            .Alpha(1, 0.25f)
            .Alpha(1, 0.75f)
            .Alpha(0, 1)
            .Color(start, 0)
            .Color(end, 1)
            .Build();
    }

    public class CurveBuilder {
        public List<Keyframe> keys;

        public CurveBuilder() {
            keys = new List<Keyframe>();
        }

        public CurveBuilder Key(float time, float value) {
            keys.Add(new Keyframe(time, value));
            return this;
        }

        public CurveBuilder Key(float time, float value, float inTangent, float outTangent) {
            keys.Add(new Keyframe(time, value, inTangent, outTangent));
            return this;
        }

        public AnimationCurve Build() => new AnimationCurve(keys.ToArray());
    }

    public static GradientBuilder Gradient() => new GradientBuilder();
    public class GradientBuilder {
        protected List<GradientColorKey> colors;
        protected List<GradientAlphaKey> alphas;

        public GradientBuilder() {
            colors = new List<GradientColorKey>();
            alphas = new List<GradientAlphaKey>();
        }
        public GradientBuilder Alpha(float value, float time) {
            alphas.Add(new GradientAlphaKey(value, time));
            return this;
        }
        public GradientBuilder Color(Color color, float time) {
            colors.Add(new GradientColorKey(color, time));
            return this;
        }

        public Gradient Build() {
            var gradient = new Gradient();
            gradient.SetKeys(colors.ToArray(), alphas.ToArray());
            return gradient;
        }
    }
    public static bool FindRoof(Vector3 position, float distance, out Vector3 roof) {
        if (Physics.Raycast(position, Vector3.up, out RaycastHit hit, distance, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore)) {
            roof = hit.point;
            return true;
        }
        roof = position + Vector3.up * distance;
        return false;
    }
    public static Vector3 FindFloor(Vector3 position, float distance) {
        if (Physics.Raycast(position, -Vector3.up, out RaycastHit hit, distance, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore)) {
            return hit.point;
        }
        return position + Vector3.up * distance;
    }

    public static FixedJoint CreateFixedJoint(Rigidbody source, Rigidbody target, Vector3? anchor = null) {
        var joint = source.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = target;
        joint.anchor = anchor ?? source.centerOfMass;
        return joint;
    }
    public static ConfigurableJoint CreateReallyFixedJoint(Rigidbody source, Rigidbody target, Vector3? anchor = null) {
        var joint = source.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = target;
        joint.anchor = anchor ?? source.centerOfMass;
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;
        return joint;
    }

    public static ConfigurableJoint CreateSimpleJoint(Rigidbody source, Rigidbody target, float spring, float damper, float maxForce = Mathf.Infinity, bool rotation = true, Quaternion? targetRotation = null) {
        Quaternion orgRotation = source.transform.rotation;
        source.transform.rotation = targetRotation ?? target.transform.rotation;
        var joint = source.gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.targetRotation = Quaternion.identity;
        joint.anchor = source.centerOfMass;
        joint.connectedAnchor = target.centerOfMass;
        joint.connectedBody = target;

        if (rotation && target.GetComponentInParent<Item>() is Item item) {
            if (item.flyDirRef)
                source.transform.rotation = item.flyDirRef.rotation;
            else if (item.holderPoint)
                source.transform.rotation = item.holderPoint.rotation * Quaternion.AngleAxis(180, Vector3.up);
            else
                source.transform.rotation = Quaternion.LookRotation(item.transform.up, item.transform.forward);
        }

        JointDrive posDrive = new JointDrive {
            positionSpring = spring,
            positionDamper = damper,
            maximumForce = maxForce
        };
        JointDrive rotDrive = new JointDrive {
            positionSpring = 1000,
            positionDamper = 50,
            maximumForce = 10000f
        };
        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.xDrive = posDrive;
        joint.yDrive = posDrive;
        joint.zDrive = posDrive;
        if (rotation) {
            joint.slerpDrive = rotDrive;
        }

        source.transform.rotation = orgRotation;
        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Free;
        return joint;
    }

    public static void Teleport(Item item, Vector3 position, float range) {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, range, -1)) {
            item.transform.position = hit.position + item.GetRadius() * Vector3.up;
        } else {
            item.transform.position = position + item.GetRadius() * Vector3.up;
        }
    }


    public static void SetFreeze(this Creature creature, bool frozen) {
        if (frozen)
            foreach (var part in creature.ragdoll.parts) {
                part.FreezeCharacterJoint();
            }
        else
            foreach (var part in creature.ragdoll.parts) {
                part.UnfreezeCharacterJoint();
            }

    }
    public static void FreezeCharacterJoint(this RagdollPart part) {
        if (part.orgCharacterJointData == null) return;
        var newJointData = part.orgCharacterJointData.Clone();
        newJointData.localRotation = part.transform.localRotation;
        newJointData.localPosition = part.transform.localPosition;
        part.DestroyCharJoint();
        part.characterJoint = newJointData.CreateJoint(part.characterJoint.gameObject, false);
        SoftJointLimit lowTwistLimit = part.characterJoint.lowTwistLimit;
        lowTwistLimit.limit = 0.0f;
        part.characterJoint.lowTwistLimit = lowTwistLimit;
        SoftJointLimit highTwistLimit = part.characterJoint.highTwistLimit;
        highTwistLimit.limit = 0.0f;
        part.characterJoint.highTwistLimit = highTwistLimit;
        SoftJointLimit swing1Limit = part.characterJoint.swing1Limit;
        swing1Limit.limit = 0.0f;
        part.characterJoint.swing1Limit = swing1Limit;
        SoftJointLimit swing2Limit = part.characterJoint.swing2Limit;
        swing2Limit.limit = 0.0f;
        part.characterJoint.swing2Limit = swing2Limit;
    }
    public static void UnfreezeCharacterJoint(this RagdollPart part) {
        if (part.orgCharacterJointData == null) return;
        part.DestroyCharJoint();
        part.characterJoint = part.orgCharacterJointData.CreateJoint(part.characterJoint.gameObject, false);
        part.ResetCharJointLimit();
    }

    public static void TempMove(Transform parent, Transform newParent, Action func, params Transform[] objs) {
        List<Vector3> positions = new List<Vector3>();
        List<Quaternion> rotation = new List<Quaternion>();
        for (int i = 0; i < objs.Length; i++) {
            positions.Add(objs[i]?.position ?? Vector3.zero);
            rotation.Add(objs[i]?.rotation ?? Quaternion.identity);
            if (objs[i]) {
                objs[i].position
                    = newParent.transform.TransformPoint(parent.transform.InverseTransformPoint(objs[i].position));
                objs[i].rotation = newParent.transform.rotation
                                   * (Quaternion.Inverse(parent.transform.rotation) * objs[i].rotation);
            }
        }


        try {
            func();
        } catch (Exception e) {
            Debug.Log($"TempMove caught exception: {e}");
        }

        for (int i = 0; i < objs.Length; i++) {
            if (objs[i]) {
                objs[i].position = positions[i];
                objs[i].rotation = rotation[i];
            }
        }
    }

    public static AnimationCurve Curve(params float[] values) {
        var curve = new AnimationCurve();
        int i = 0;
        foreach (var value in values) {
            curve.AddKey(i / ((float) values.Length - 1), value);
            i++;
        }

        return curve;
    }


    public static void Teleport(Creature creature, Vector3 position, float range, bool safe = true) {
        bool found = NavMesh.SamplePosition(position, out NavMeshHit hit, range, NavMesh.AllAreas);
        if (safe && !found) {
            found = NavMesh.SamplePosition(position - Vector3.up * 8, out hit, range, NavMesh.AllAreas);
            if (!found)
                return;
        }

        var target = found ? hit.position : position;
        Vector3 currentPos = Player.local.locomotion.transform.position;
        if (creature.isPlayer) {
            Player.local.locomotion.transform.position = target;
            Hands.ForBothHands(hand => {
                if (hand.grabbedHandle?.item is Item item && !item.rb.isKinematic) {
                    item.transform.position = target + item.transform.position - currentPos;
                } else {
                    hand.TryRelease();
                }
            });
        } else {
            creature.locomotion.transform.position = target;
        }
    }

    public static void Explosion(Vector3 origin, float force, float radius, bool massCompensation = false, bool disarm = false, bool dismemberIfKill = false, bool affectPlayer = false, float damage = 0, Action<Creature> onHit = null) {
        var seenRigidbodies = new List<Rigidbody>();
        var seenCreatures = new List<Creature> { };
        if (!affectPlayer) {
            seenCreatures.Add(Player.currentCreature);
        }

        object handler = new object();
        foreach (var collider in Physics.OverlapSphere(origin, radius)) {
            if (collider.attachedRigidbody == null)
                continue;
            if (collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerHandAndFoot) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotion) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotionObject))
                continue;
            if (!seenRigidbodies.Contains(collider.attachedRigidbody) && collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC)) {
                seenRigidbodies.Add(collider.attachedRigidbody);
                float modifier = 1;
                if (collider.attachedRigidbody.mass < 1) {
                    modifier *= collider.attachedRigidbody.mass * 2;
                } else {
                    modifier *= collider.attachedRigidbody.mass;
                }

                if (!massCompensation)
                    modifier = 1;
                if (collider.attachedRigidbody.GetComponent<CollisionHandler>()?.isRagdollPart == true) {
                    modifier = 2f;
                }
                modifier *= Random.Range(0.9f, 1.1f);
                collider.attachedRigidbody.AddExplosionForce(force * modifier, origin, radius, 1, ForceMode.Impulse);
            } else if (collider.GetComponentInParent<Creature>() is Creature creature
                       && !seenCreatures.Contains(creature)) {
                seenCreatures.Add(creature);
                if (!creature.isPlayer && !creature.isKilled) {
                    creature.ragdoll.AddPhysicToggleModifier(handler);
                    creature.ragdoll.SetState(Ragdoll.State.Inert);
                    creature.ragdoll.RunAfter(() => creature.ragdoll.RemovePhysicToggleModifier(handler), 2f);
                    creature.TryPush(Creature.PushType.Magic,
                        (creature.ragdoll.rootPart.transform.position - origin).normalized, 3);
                    if (!creature.isPlayer && disarm) {
                        creature.handLeft.TryRelease();
                        creature.handRight.TryRelease();
                    }

                    onHit?.Invoke(creature);
                }

                if (damage > 0) {
                    var hit = new CollisionInstance(new DamageStruct(DamageType.Energy, damage));
                    hit.damageStruct.hitRagdollPart = creature.GetTorso();
                    creature.Damage(hit);
                }

                if (dismemberIfKill && creature.isKilled) {
                    foreach (var ragdollPart in creature.ragdoll.parts
                        .Where(thisPart => thisPart.sliceAllowed)
                        .OrderBy(thisPart => Random.Range(0f, 1f)).Take(Random.Range(0, 2))) {
                        ragdollPart.TrySlice();
                    }
                }
            }
        }
    }

    public static void OnTrigger(this GameObject obj, Action<Collider, EventStep> action) {
        if (obj.GetComponent<Collider>() is Collider collider) {
            collider.isTrigger = true;
            obj.GetOrAddComponent<TriggerTracker>().OnTrigger(action);
        }
    }

    public static void PushForce(Vector3 origin, Vector3 direction, float radius, float distance, Vector3 force, bool massCompensation = false, bool disarm = false) {
        var seenRigidbodies = new List<Rigidbody>();
        var seenCreatures = new List<Creature> { Player.currentCreature };
        foreach (var hit in Physics.SphereCastAll(origin, radius, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            var collider = hit.collider;
            if (collider.attachedRigidbody == null)
                continue;
            if (collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerHandAndFoot) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotion) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotionObject))
                continue;
            if (!seenRigidbodies.Contains(collider.attachedRigidbody)) {
                seenRigidbodies.Add(collider.attachedRigidbody);
                float modifier = 1;
                if (collider.attachedRigidbody.mass < 1) {
                    modifier *= collider.attachedRigidbody.mass * 2;
                } else {
                    modifier *= collider.attachedRigidbody.mass;
                }
                if (!massCompensation)
                    modifier = 1;
                collider.attachedRigidbody.AddForce(force * modifier, ForceMode.Impulse);
            } else if (collider.GetComponentInParent<Creature>() is Creature npc && npc != null && !seenCreatures.Contains(npc)) {
                seenCreatures.Add(npc);
                npc.TryPush(Creature.PushType.Magic, (npc.ragdoll.rootPart.transform.position - origin).normalized, 2);
                if (disarm) {
                    npc.handLeft.TryRelease();
                    npc.handRight.TryRelease();
                }
            }
        }
    }

    public static Creature HomingCreature(Rigidbody rigidbody, Vector3 velocity, float homingAngle, float maxDistance = 10, Creature ignoredCreature = null) {
        var hits = Physics.SphereCastAll(rigidbody.transform.position, 10, velocity, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        var targets = hits.SelectNotNull(hit => hit.collider?.attachedRigidbody?.GetComponentInParent<Creature>())
            .Where(creature => creature != ignoredCreature && creature != Player.currentCreature && creature.state != Creature.State.Dead)
            .Where(creature => Vector3.Angle(velocity, creature.ragdoll.headPart.transform.position - rigidbody.transform.position)
                 < homingAngle + 3 * Vector3.Distance(rigidbody.transform.position, Player.currentCreature.transform.position))
            .OrderBy(creature => Vector3.Angle(velocity, creature.ragdoll.headPart.transform.position - rigidbody.transform.position));
        var closeToAngle = targets.Where(creature => Vector3.Angle(velocity, creature.ragdoll.headPart.transform.position - rigidbody.transform.position) < 5);
        if (closeToAngle.Any()) {
            targets = closeToAngle.OrderBy(creature => Vector3.Distance(rigidbody.transform.position, creature.ragdoll.headPart.transform.position));
        }
        return targets.FirstOrDefault();
    }
    public static Vector3 HomingThrow(this Rigidbody rigidbody, Vector3 velocity, float homingAngle, Creature ignoredCreature = null) {
        var target = HomingCreature(rigidbody, velocity, homingAngle, 10, ignoredCreature);
        if (!target)
            return velocity;
        var extendedPoint = rigidbody.transform.position + velocity.normalized * Vector3.Distance(rigidbody.transform.position, target.ragdoll.GetPart(RagdollPart.Type.Torso).transform.position);
        var targetPart = target.ragdoll.parts.MinBy(part => Vector3.Distance(part.transform.position, extendedPoint));
        var vectorToTarget = targetPart.transform.position - rigidbody.transform.position;
        rigidbody.velocity = Vector3.zero;
        velocity = vectorToTarget.normalized * velocity.magnitude;
        return velocity;
    }

    public static void Clone(this Item item, EffectData effectData = null) {
        Catalog.GetData<ItemData>(item.itemId)?
            .SpawnAsync(newItem => {
                effectData
                    .Spawn(item.transform.position, item.transform.rotation)
                    .Play();
                newItem.transform.position = item.transform.position;
                newItem.transform.rotation = item.transform.rotation;
                if (item.contentCustomData != null) {
                    foreach (var data in item.contentCustomData) {
                        newItem.AddCustomData(data);
                    }
                }

                item.StartCoroutine(CloneRoutine(item, newItem));
            });
    }

    static IEnumerator CloneRoutine(Item a, Item b) {
        a.IgnoreObjectCollision(b);
        b.IgnoreObjectCollision(a);
        var vector = Vector3.ProjectOnPlane(RandomVector(), Vector3.up).normalized
                     * Mathf.Clamp01(a.GetRadius())
                     * 0.5f;
        b.transform.position = a.transform.position + vector;
        b.rb.velocity = a.rb.velocity;
        b.rb.angularVelocity = a.rb.angularVelocity;
        b.rb.AddForceAtPosition((vector + Vector3.up * 0.5f) * 10f,
            a.transform.position + vector * 0.5f, ForceMode.VelocityChange);
        a.rb.AddForceAtPosition((-vector + Vector3.up) * 10f,
            a.transform.position + vector * 0.5f, ForceMode.VelocityChange);
        yield return new WaitForSeconds(0.5f);
        a.ResetObjectCollision();
        b.ResetObjectCollision();
    }

    public static Transform GetPlayerChest() {
        return Player.currentCreature.ragdoll.GetPart(RagdollPart.Type.Torso).transform;
    }

    public static Vector3 UniqueVector(this GameObject obj, float min = -1, float max = 1, int salt = 0) {
        var rand = new System.Random(obj.GetInstanceID() + salt);
        return new Vector3(
            (float)rand.NextDouble() * (max - min) + min,
            (float)rand.NextDouble() * (max - min) + min,
            (float)rand.NextDouble() * (max - min) + min);
    }

    public static float UniqueFloat(this GameObject obj, int salt = 0)
        => (float)new System.Random(obj.GetInstanceID() + salt).NextDouble();

    public static Vector3 RandomVector(float min = -1, float max = 1, int salt = 0) {
        return new Vector3(
            Random.Range(0f, 1f) * (max - min) + min,
            Random.Range(0f, 1f) * (max - min) + min,
            Random.Range(0f, 1f) * (max - min) + min);
    }

    // WARNING: If you can find a way to not use the following two methods, please do - they are INCREDIBLY bad practice
    /// <summary>
    /// Get a private field from an object
    /// </summary>

    /// <summary>
    /// Set a private field from an object
    /// </summary>
    public static void SetField<T, U>(this T instance, string fieldName, U value) {
        BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static;
        FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
        field.SetValue(instance, value);
    }

    /// <summary>
    /// Get a list of live NPCs
    /// </summary>
    public static IEnumerable<Creature> GetAliveNPCs() => Creature.allActive
        .Where(creature => creature != Player.currentCreature
                        && creature.state != Creature.State.Dead);

    public static IEnumerable<Item> AllItemsInRadius(Vector3 position, float radius) => Item.allActive.Where(item
        => (position - item.rb.ClosestPointOnBounds(position)).sqrMagnitude < radius.Pow(2));

    public static IEnumerable<Item> ItemsInRadius(Vector3 position, float radius) {
        return Physics.OverlapSphere(position, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .SelectNotNull(collider => collider.attachedRigidbody?.GetComponent<CollisionHandler>()?.item)
            .Distinct();
    }

    public static IEnumerable<CollisionHandler> HandlersInRadius(Vector3 position, float radius, bool player = false) {
        return Physics.OverlapSphere(position, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .SelectNotNull(collider => collider.attachedRigidbody?.GetComponent<CollisionHandler>())
            .Where(handler => player || handler.ragdollPart?.ragdoll?.creature.isPlayer != true)
            .Distinct();
    }

    public static IEnumerable<Creature> CreaturesInRadius(Vector3 position, float radius, bool player = true, bool live = false) {
        return Creature.allActive.Where(creature
            => (!live || !creature.isKilled)
               && (player || !creature.isPlayer)
               && (creature.GetChest() - position).sqrMagnitude < radius * radius);
    }

    public static Creature ClosestCreatureInRadius(Vector3 position, float radius, bool live = true) {
        float lastRadius = Mathf.Infinity;
        Creature lastCreature = null;
        float thisRadius;
        foreach (var creature in Creature.allActive) {
            if (creature.isCulled || creature.isPlayer || live && creature.isKilled) continue;
            thisRadius = (creature.GetChest() - position).sqrMagnitude;
            if (thisRadius < radius * radius && thisRadius < lastRadius) {
                lastRadius = thisRadius;
                lastCreature = creature;
            }
        }

        return lastCreature;
    }

    public static Vector3 RoughClosestPoint(this Creature creature, Vector3 point) {
        var center = creature.GetTorso().transform.position;
        return new Ray(center, point - center).GetPoint(creature.GetHeight() / 2);
    }
    public static Creature TargetCreature(
        Vector3 position,
        Vector3 direction,
        float distance,
        float angle,
        Creature toIgnore = null,
        bool live = true) {
        float lastAngle = Mathf.Infinity;
        float sqrDistance = distance * distance;
        Creature target = null;
        var ray = new Ray(position, direction);
        foreach (var creature in Creature.allActive) {
            if (live && creature.isKilled
                || creature.isCulled
                || creature.isPlayer
                || !creature.initialized
                || creature == target
                || creature == toIgnore
                || creature.ragdoll.state == Ragdoll.State.Disabled) continue;
            var handToCreature = creature.RoughClosestPoint(ray.GetPoint(Vector3.Distance(creature.transform.position, position))) - position;
            var creatureDistance = handToCreature.sqrMagnitude;

            if (creatureDistance < sqrDistance) {
                var angleToCreature = Vector3.Angle(direction, handToCreature);
                if (angleToCreature < angle && angleToCreature < lastAngle) {
                    lastAngle = angleToCreature;
                    target = creature;
                }
            }
        }

        return target;
    }

    public static Creature TargetCreature(Ray ray, float distance, float angle, Creature toIgnore = null, bool live = true)
        => TargetCreature(ray.origin, ray.direction, distance, angle, toIgnore, live);

    public static RagdollPart TargetPart(
        Vector3 position,
        Vector3 direction,
        float distance,
        float angle,
        Creature toIgnore = null) => TargetPart(new Ray(position, direction), distance, angle, toIgnore);

    public static RagdollPart TargetPart(
        Ray ray,
        float distance,
        float angle,
        Creature toIgnore = null) {
        var creature = TargetCreature(ray, distance, angle, toIgnore);
        var extendedPoint = ray.GetPoint(Vector3.Distance(creature.GetChest(), ray.origin));
        float lastPartDistance = Mathf.Infinity;
        RagdollPart lastPart = null;
        foreach (var part in creature.ragdoll.parts) {
            var thisDistance = (part.transform.position - extendedPoint).sqrMagnitude;
            if (thisDistance < lastPartDistance) {
                lastPartDistance = thisDistance;
                lastPart = part;
            }
        }

        return lastPart;
    }

    public static bool HandlerCone(
        Ray ray,
        float distance,
        float angle,
        out CollisionHandler outHandler,
        out RaycastHit outHit,
        bool live = true) {
        float lastAngle = Mathf.Infinity;
        bool found = false;
        outHandler = null;
        outHit = default;
        foreach (var hit in Physics.SphereCastAll(ray, 5, distance,
            LayerMask.GetMask("Default", "BodyLocomotion", "MovingItem", "DroppedItem"), QueryTriggerInteraction.Ignore)) {
            if (hit.rigidbody?.GetComponent<CollisionHandler>() is CollisionHandler handler) {
                if (handler.ragdollPart is RagdollPart part) {
                    if (part.ragdoll.creature.isCulled
                        || part.ragdoll.creature.isPlayer
                        || part.ragdoll.creature.isKilled && live) {
                        continue;
                    }
                } else if (handler.item is Item item) {
                    if (item.isCulled) continue;
                }

                var thisAngle = Vector3.Angle(ray.direction, hit.point - ray.origin);
                if (thisAngle < angle && thisAngle < lastAngle) {
                    outHandler = handler;
                    outHit = hit;
                    found = true;
                }
            }
        }

        return found;
    }

    public static Item TargetItem(Vector3 position, Vector3 direction, float distance, float angle, bool free = false)
        => TargetItem(new Ray(position, direction), distance, angle, free);
    public static Item TargetItem(Ray ray, float distance, float angle, bool free = false) {
        float lastAngle = Mathf.Infinity;
        float sqrDistance = distance * distance;
        Item target = null;
        foreach (var item in Item.allActive) {
            if (item.isCulled
                || item.isCulled
                || item.rb.isKinematic
                || free
                && (item.mainHandler != null
                    || item.isGripped
                    || item.isTelekinesisGrabbed
                    || item.holder != null)
                || item == target) continue;
            var toItem = item.rb.worldCenterOfMass
                             - ray.origin;
            var itemDistance = toItem.sqrMagnitude;

            if (itemDistance < sqrDistance) {
                var angleToItem = Vector3.Angle(ray.direction, toItem);
                if (angleToItem < angle && angleToItem < lastAngle) {
                    lastAngle = angleToItem;
                    target = item;
                }
            }
        }

        return target;
    }

    public static void AddForce(this Creature creature, Vector3 force, ForceMode mode) {
        foreach (var part in creature.ragdoll.parts) {
            part.rb.AddForce(force, mode);
        }
    }

    public static void UpdateJointStrength(this ConfigurableJoint joint, float spring, float damp) {
        if (joint == null)
            return;
        JointDrive posDrive = new JointDrive();
        posDrive.positionSpring = spring;
        posDrive.positionDamper = damp;
        posDrive.maximumForce = 1000;
        joint.xDrive = posDrive;
        joint.yDrive = posDrive;
        joint.zDrive = posDrive;
    }

    // Original idea from walterellisfun on github: https://github.com/walterellisfun/ConeCast/blob/master/ConeCastExtension.cs
    /// <summary>
    /// Like SphereCastAll but in a cone
    /// </summary>
    /// <param name="origin">Origin position</param>
    /// <param name="maxRadius">Maximum cone radius</param>
    /// <param name="direction">Cone direction</param>
    /// <param name="maxDistance">Maximum cone distance</param>
    /// <param name="coneAngle">Cone angle</param>
    public static RaycastHit[] ConeCastAll(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, float coneAngle) {
        RaycastHit[] sphereCastHits = Physics.SphereCastAll(origin, maxRadius, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        List<RaycastHit> coneCastHitList = new List<RaycastHit>();

        if (sphereCastHits.Length > 0) {
            for (int i = 0; i < sphereCastHits.Length; i++) {
                Vector3 hitPoint = sphereCastHits[i].point;
                Vector3 directionToHit = hitPoint - origin;
                float angleToHit = Vector3.Angle(direction, directionToHit);
                float multiplier = 1f;
                if (directionToHit.magnitude < 2f)
                    multiplier = 4f;
                bool hitRigidbody = sphereCastHits[i].rigidbody is Rigidbody rb
                                    && Vector3.Angle(direction, rb.transform.position - origin) < coneAngle * multiplier;

                if (angleToHit < coneAngle * multiplier || hitRigidbody) {
                    coneCastHitList.Add(sphereCastHits[i]);
                }
            }
        }
        return coneCastHitList.ToArray();
    }

    public static LayerMask GetMask(params LayerName[] layers) {
        if (layers == null)
            throw new ArgumentNullException(nameof(layers));
        var mask = 0;
        for (var i = 0; i < layers.Length; i++) {
            var layerName = layers[i];
            int layer = LayerMask.NameToLayer(layerName.ToString());
            if (layer != -1)
                mask |= 1 << layer;
        }


        return mask;
    }

    public static IEnumerator LoopOver(Action<float> action, float time, System.Action after = null) {
        var startTime = Time.time;
        float elapsed;
        while ((elapsed = Time.time - startTime) <= time) {
            action(elapsed / time);
            yield return 0;
        }

        after?.Invoke();
    }

    /// <summary>
    /// Get a creature's random part (dead or alive)
    /// </summary>
    /*
    Head
    Neck
    Torso
    LeftArm
    RightArm
    LeftHand
    RightHand
    LeftLeg
    RightLeg
    LeftFoot
    RightFoot
    */
    //Free gift for anyone
    public static RagdollPart GetRandomRagdollPart(this Creature creature)
    {
        Array values = Enum.GetValues(typeof(RagdollPart.Type));
        return creature.ragdoll.GetPart((RagdollPart.Type)values.GetValue(Random.Range(0, values.Length)));
    }
}

public class ItemModifier : MonoBehaviour {
    public Item item;
    public HashSet<object> handlers;
    private bool applied;

    public void Awake() {
        item = GetComponent<Item>();
        item.OnDespawnEvent += time => {
            if (time == EventTime.OnStart)
                Clear();
        };
        handlers = new HashSet<object>();
        OnBegin();
    }

    public void AddHandler(object handler) {
        if (!handlers.Contains(handler)) {
            handlers.Add(handler);
            Refresh();
        }
    }

    public void RemoveHandler(object handler) {
        if (handlers.Contains(handler)) {
            handlers.Remove(handler);
            Refresh();
        }
    }

    public void Refresh() {
        if (handlers.Count == 0) {
            if (applied) {
                applied = false;
                OnRemove();
            }
        } else {
            if (!applied) {
                applied = true;
                OnApply();
            }
        }
    }

    public void Clear() {
        handlers.Clear();
        Refresh();
    }

    public virtual void OnBegin() { }
    public virtual void OnRemove() { }
    public virtual void OnApply() { }
}
public class CreatureModifier : MonoBehaviour {
    public Creature creature;
    public HashSet<object> handlers;
    private bool applied;

    public void Awake() {
        creature = GetComponent<Creature>();
        creature.OnDespawnEvent += time => {
            if (time == EventTime.OnStart)
                Clear();
        };
        handlers = new HashSet<object>();
        OnBegin();
    }

    public void AddHandler(object handler) {
        if (!handlers.Contains(handler)) {
            handlers.Add(handler);
            Refresh();
        }
    }

    public void RemoveHandler(object handler) {
        if (handlers.Contains(handler)) {
            handlers.Remove(handler);
            Refresh();
        }
    }

    public void Refresh() {
        if (handlers.Count == 0) {
            if (applied) {
                applied = false;
                OnRemove();
            }
        } else {
            if (!applied) {
                applied = true;
                OnApply();
            }
        }
    }

    public void Clear() {
        handlers.Clear();
        Refresh();
    }

    public virtual void OnBegin() { }
    public virtual void OnRemove() { }
    public virtual void OnApply() { }
}

class RigidbodyModifier : MonoBehaviour {
    struct Modifier {
        public int priority;
        public float? gravity;
        public float? drag;
        public float? mass;

        public Modifier(int priority, float? gravity = null, float? drag = null, float? mass = null) {
            this.priority = priority;
            this.gravity = gravity;
            this.drag = drag;
            this.mass = mass;
        }
    }

    private Dictionary<object, Modifier> modifiers = new Dictionary<object, Modifier>();
    private Rigidbody rb;
    private float orgDrag;
    private float orgAngularDrag;
    private float orgMass;
    private float orgColliderHeight;

    public void Awake() {
        rb = GetComponent<Rigidbody>();
        if (rb.GetComponent<Locomotion>() is Locomotion loco) orgColliderHeight = loco.capsuleCollider.height;
        if (rb.GetComponent<CollisionHandler>()?.item?.data is ItemData data) {
            orgDrag = data.drag;
            orgAngularDrag = data.angularDrag;
            orgMass = data.mass;
        } else if (rb.GetComponent<CollisionHandler>()?.ragdollPart is RagdollPart part) {
            orgDrag = rb.drag;
            orgAngularDrag = rb.angularDrag;
            orgMass = rb.mass;
            part.ragdoll.creature.OnDespawnEvent += time => {
                if (time == EventTime.OnStart) Clear();
            };
        } else {
            orgDrag = rb.drag;
            orgAngularDrag = rb.angularDrag;
            orgMass = rb.mass;
        }
    }
    
    public void Clear() {
        modifiers.Clear();
        Update();
        Destroy(this);
    }

    public void AddModifier(
        object handler,
        int priority,
        float? gravity = null,
        float? drag = null,
        float? mass = null) {
        modifiers[handler] = new Modifier(priority, gravity, drag, mass);
    }

    public void RemoveModifier(object handler) {
        modifiers.Remove(handler);
        if (rb == null) return;
        if (!modifiers.Where(mod => mod.Value.gravity != null).Any()) {
            rb.useGravity = true;
            rb.GetComponent<CollisionHandler>()?.RefreshPhysicModifiers();
            if (rb.GetComponent<Locomotion>() is Locomotion loco) loco.capsuleCollider.height = orgColliderHeight;
        }

        if (!modifiers.Where(mod => mod.Value.drag != null).Any()) {
            rb.drag = orgDrag;
            rb.angularDrag = orgAngularDrag;
        }

        if (!modifiers.Where(mod => mod.Value.mass != null).Any()) {
            rb.mass = orgMass;
        }
    }

    public void Update() {
        if (!rb) return;
        int lastGravPriority = int.MinValue;
        int lastDragPriority = int.MinValue;
        int lastMassPriority = int.MinValue;
        foreach (var modifier in modifiers.Values) {
            if (modifier.gravity is float gravity && modifier.priority > lastGravPriority) {
                lastGravPriority = modifier.priority;
                rb.useGravity = false;
                if (rb.GetComponent<Locomotion>() is Locomotion loco) loco.capsuleCollider.height = 0.9f;
                rb.AddForce(Physics.gravity * gravity);
            }

            if (modifier.drag is float drag && modifier.priority > lastDragPriority) {
                lastDragPriority = modifier.priority;
                rb.drag = (orgDrag == 0 ? 1 : orgDrag) * drag;
                rb.angularDrag = (orgAngularDrag == 0 ? 1 : orgAngularDrag) * drag;
            }

            if (modifier.mass is float mass && modifier.priority > lastMassPriority) {
                lastMassPriority = modifier.priority;
                rb.mass = (orgMass == 0 ? 1 : orgMass) * mass;
            }
        }
    }
}

public enum Axis {
    X, Y, Z
}

public enum ViewDir {
    Forward,
    Back,
    Up,
    Down,
    Left,
    Right,
    X,
    Y,
    Z,
    Invalid
}

public enum EventStep {
    Enter,
    Update,
    Exit
}

public class Zone : MonoBehaviour {
    public float distance;
    public float radius;
    public CapsuleCollider collider;
    private HashSet<CollisionHandler> handlers;
    private HashSet<Creature> creatures;
    private HashSet<Item> items;

    public void Start() {
        handlers = new HashSet<CollisionHandler>();
        creatures = new HashSet<Creature>();
        items = new HashSet<Item>();
        gameObject.layer = GameManager.GetLayer(LayerName.ItemAndRagdollOnly);
        collider = gameObject.AddComponent<CapsuleCollider>();
        collider.center = Vector3.forward * (distance / 2 - 0.3f);
        collider.radius = radius;
        collider.height = distance + 0.3f;
        collider.direction = 2;
        collider.isTrigger = true;
        Begin();
    }

    public virtual void Begin() {}

    public void Update() {
        foreach (var handler in handlers) OnHandlerEvent(handler, EventStep.Update);
        foreach (var item in items) OnItemEvent(item, EventStep.Update);
        foreach (var creature in creatures) CreatureEvent(creature, EventStep.Update);
    }

    public void Despawn() {
        foreach (var handler in handlers) OnHandlerEvent(handler, EventStep.Exit);
        foreach (var item in items) OnItemEvent(item, EventStep.Exit);
        foreach (var creature in creatures) CreatureEvent(creature, EventStep.Exit);
        OnDespawn();
        Destroy(gameObject);
    }

    public virtual void OnDespawn() {}
    

    public void OnTriggerEnter(Collider collider) {
        var handler = collider.attachedRigidbody?.GetComponent<CollisionHandler>();
        if (!handler || collider.attachedRigidbody.isKinematic) return;
            if (!handlers.Contains(handler)) {
                handlers.Add(handler);
        if (handler.ragdollPart?.ragdoll.creature.isPlayer != true) {
                OnHandlerEvent(handler, EventStep.Enter);
        }
            }

        if (handler.item is Item item) {
            if (!items.Contains(item)) {
                OnItemEvent(item, EventStep.Enter);
                items.Add(item);
            }
        }

        if (handler.ragdollPart?.ragdoll.creature is Creature creature) {
            if (!creatures.Contains(creature)) {
                creatures.Add(creature);
                CreatureEvent(creature, EventStep.Enter);
            }
        }
    }

    public bool HasItemHandlers(Item item) => handlers.Any(handler => handler.item == item);
    public bool HasCreatureHandlers(Creature creature)  => handlers.Any(handler => handler.ragdollPart?.ragdoll.creature == creature);
    public void OnTriggerExit(Collider collider) {
        var handler = collider.attachedRigidbody?.GetComponent<CollisionHandler>();
        if (!handler || collider.attachedRigidbody.isKinematic) return;
        if (handlers.Contains(handler)) {
            handlers.Remove(handler);
            if (handler.ragdollPart?.ragdoll.creature.isPlayer != true) {
                OnHandlerEvent(handler, EventStep.Exit);
            }
        }

        if (handler.item is Item item && items.Contains(item) && !HasItemHandlers(item)) {
            items.Remove(item);
            OnItemEvent(item, EventStep.Exit);
        }
        if (handler.ragdollPart?.ragdoll.creature is Creature creature && creatures.Contains(creature) && !HasCreatureHandlers(creature)) {
            creatures.Remove(creature);
            CreatureEvent(creature, EventStep.Exit);
        }
    }

    public void CreatureEvent(Creature creature, EventStep step) {
        OnCreatureEvent(creature, step);
        if (creature.isPlayer) {
            OnPlayerEvent(step);
        } else {
            OnNPCEvent(creature, step);
        }
    }


    public virtual void OnNPCEvent(Creature creature, EventStep step) {}
    public virtual void OnPlayerEvent(EventStep step) {}
    public virtual void OnCreatureEvent(Creature creature, EventStep step) {}
    public virtual void OnItemEvent(Item item, EventStep step) {}
    public virtual void OnHandlerEvent(CollisionHandler handler, EventStep step) {}
}

public class TriggerTracker : MonoBehaviour {
    public List<Action<Collider, EventStep>> actions;
    public void Awake() => actions = new List<Action<Collider, EventStep>>();
    public void OnTrigger(Action<Collider, EventStep> action) => actions.Add(action);

    public void OnTriggerEnter(Collider collider) {
        foreach (var action in actions) action(collider, EventStep.Enter);
    }
    public void OnTriggerExit(Collider collider) {
        foreach (var action in actions) action(collider, EventStep.Exit);
    }
}

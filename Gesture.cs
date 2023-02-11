using System;
using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;
using JetBrains.Annotations;

namespace GestureEngine;

using NamedConditionSet = Tuple<string, Func<bool>[]>;
using NamedCondition = Tuple<string, Func<bool>>;

/// <summary>
/// A struct representing a target position for a hand.
/// </summary>
/// <param name="Position">Target position</param>
/// <param name="Rotation">Target rotation</param>
/// <param name="Gripping">Should the hand be gripping?</param>
/// <param name="Triggering">Should the hand be triggering?</param>
/// <param name="Radius">Maximum tolerance for the position of the hand</param>
public record struct GestureTarget(Vector3 Position, Quaternion Rotation, bool Gripping, bool Triggering, float Radius);

// An excerpt from my Utils file

// public enum Axis {
//     X, Y, Z
// }
// 
// public enum ViewDir {
//     Forward,
//     Back,
//     Up,
//     Down,
//     Left,
//     Right,
//     X,
//     Y,
//     Z
// }
public static class Extensions {
    public static Vector3 ToVector(this Direction direction, Side side, PlayerRig rig) {
        return direction.ToViewDir(side, rig, out var dir, out var fallback)
            ? dir.ToWorldViewPlaneVector()
            : fallback;
    }
    public static bool ToViewDir(
        this Direction direction,
        Side side,
        PlayerRig rig,
        out ViewDir viewDir,
        out Vector3 fallback) {
        fallback = Vector3.zero;
        viewDir = ViewDir.Invalid;
        viewDir = direction switch {
            Direction.Backward => ViewDir.Back,
            Direction.Forward => ViewDir.Forward,
            Direction.Inwards => side == Side.Left ? ViewDir.Right : ViewDir.Left,
            Direction.Outwards => side == Side.Left ? ViewDir.Left : ViewDir.Right,
            Direction.Up => ViewDir.Up,
            Direction.Down => ViewDir.Down,
            _ => ViewDir.Invalid
        };
        if (viewDir != ViewDir.Invalid) return true;

        var together
            = (rig.GetHand(side.Other()).position - rig.GetHand(side).position)
            .normalized;
        fallback = direction switch {
            Direction.Together => together,
            Direction.Apart => -together,
            _ => Vector3.zero
        };
        return false;
    }

}
//     public static bool InDirection(this Vector3 vec, ViewDir direction, float amount = 0) {
//         return vec.Mostly(direction.GetAxis()) && direction.Compare(vec, amount);
//     }
// 
//     public static float Abs(this float num) => Mathf.Abs(num);
// 
//     public static int Sign(this ViewDir dir) {
//         return dir switch {
//             ViewDir.Right or ViewDir.Up or ViewDir.Forward => 1,
//             ViewDir.Left or ViewDir.Down or ViewDir.Back => -1,
//             _ => 0
//         };
//     }
// 
//     public static Axis GetAxis(this ViewDir dir) {
//         return dir switch {
//             ViewDir.X or ViewDir.Left or ViewDir.Right => Axis.X,
//             ViewDir.Y or ViewDir.Up or ViewDir.Down => Axis.Y,
//             ViewDir.Z or ViewDir.Forward or ViewDir.Back => Axis.Z,
//             _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
//         };
//     }
// 
//     public static float GetAxis(this Vector3 vec, Axis axis) {
//         return axis switch {
//             Axis.X => vec.x,
//             Axis.Y => vec.y,
//             Axis.Z => vec.z,
//             _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
//         };
//     }
// 
//     public static bool Compare(this ViewDir dir, Vector3 vec, float amount) {
//         return dir.Sign() switch {
//             1 => vec.GetAxis(dir.GetAxis()) > amount,
//             0 => vec.GetAxis(dir.GetAxis()).Abs() > amount,
//             -1 => vec.GetAxis(dir.GetAxis()) < -amount,
//             _ => false
//         };
//     }
// 
//     public static bool Triggering(this RagdollHand hand) => hand?.playerHand?.controlHand?.usePressed ?? false;
//     public static bool Gripping(this RagdollHand hand) => hand?.playerHand?.controlHand?.gripPressed ?? false;
//     public static bool MostlyX(this Vector3 vec) => vec.x.Abs() > vec.y.Abs() && vec.x.Abs() > vec.z.Abs();
// 
//     /// <summary>
//     /// Returns true if the vector's Y component is its largest component
//     /// </summary>
//     public static bool MostlyY(this Vector3 vec) => vec.y.Abs() > vec.x.Abs() && vec.y.Abs() > vec.z.Abs();
// 
//     /// <summary>
//     /// Returns true if the vector's Z component is its largest component
//     /// </summary>
//     public static bool MostlyZ(this Vector3 vec) => vec.z.Abs() > vec.x.Abs() && vec.z.Abs() > vec.y.Abs();
// 
//     public static string Join(this string delimiter, params object[] strings) {
//         return string.Join(delimiter, strings.Where(str => str != null).ToList());
//     }
// 
//     public static bool Mostly(this Vector3 vec, Axis axis) {
//         return axis switch {
//             Axis.X => vec.MostlyX(),
//             Axis.Y => vec.MostlyY(),
//             Axis.Z => vec.MostlyZ(),
//             _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
//         };
//     }
// 
//     public static Vector3 LocalVelocity(this RagdollHand hand)
//         => hand.Velocity() - Player.local.locomotion.rb.GetPointVelocity(hand.transform.position);
// 
//     public static Vector3 ToUnitVector(this ViewDir dir) => dir switch {
//         ViewDir.Forward => Vector3.forward,
//         ViewDir.Back => Vector3.back,
//         ViewDir.Up => Vector3.up,
//         ViewDir.Down => Vector3.down,
//         ViewDir.Left => Vector3.left,
//         ViewDir.Right => Vector3.right,
//         _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
//     };
// 
//     public static Vector3 WorldToViewSpace(this Vector3 vec) => Player.local.head.transform.InverseTransformVector(vec);
// 
//     public static Vector3 WorldToViewPlaneSpace(this Vector3 vec)
//         => Quaternion.Inverse(Player.local.head.transform.rotation) * vec;
// 
//     public static Vector3 LocalToViewSpace(this Vector3 vec) => Player.local.head.transform.TransformVector(vec);
// 
//     public static Vector3 LocalToViewPlaneSpace(this Vector3 vec) => Quaternion.LookRotation(
//                                                                          Vector3.ProjectOnPlane(
//                                                                              Player.local.head.transform.forward,
//                                                                              Vector3.up),
//                                                                          Vector3.up)
//                                                                      * vec;
// 
//     public static Vector3 ToWorldViewVector(this ViewDir dir) => dir.ToUnitVector().LocalToViewSpace().normalized;
// 
//     public static Vector3 ToWorldViewPlaneVector(this ViewDir dir)
//         => dir.ToUnitVector().LocalToViewPlaneSpace().normalized;
// }

/// <summary>
/// A rig for the player. Defaults to the actual player rig and positions (for use in gesture testing),
/// but can be set custom to display gestures elsewhere using GestureStep.UpdateTargets.
/// </summary>
public struct PlayerRig {
    public GameObject left;
    public GameObject right;
    public Transform chest;
    public Transform waist;
    public Transform head;
    public Vector3 root;

    public PlayerRig() : this(Player.local.handLeft.ragdollHand.grip.gameObject,
        Player.local.handRight.ragdollHand.grip.gameObject,
        Player.currentCreature.ragdoll.rootPart.transform, Player.local.head.transform) { }


    public Transform GetHand(Side side) => side switch {
        Side.Left => left.transform,
        Side.Right => right.transform,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
    };

    public PlayerRig(GameObject left, GameObject right, Vector3 root) {
        this.left = left;
        this.right = right;
        this.root = root;
        chest = null;
        waist = null;
        head = null;
    }

    public PlayerRig(GameObject left, GameObject right, Transform waist, Transform head = null) {
        this.left = left;
        this.right = right;
        this.waist = waist;
        this.head = head;
        chest = null;
        root = default;
    }

    public Vector3 Chest => chest?.position ?? Vector3.Lerp(Waist, Head, 0.5f);
    public Vector3 Waist => waist?.position ?? root;
    public Vector3 Head => head?.position ?? Waist + Vector3.up * 0.6f;
}


/// <summary>
/// A gesture on one or both hands.
/// </summary>
public class Gesture {
    /// <summary>
    /// Set the handedness of the tester. If you change this to Side.Left,
    /// it will swap which hands Gesture.Left or Gesture.Right point to.
    /// </summary>
    public static Side handedness = Side.Right;
    /// <summary>
    /// Either hand, or both.
    /// </summary>
    public enum HandSide {
        Left,
        Right,
        Both
    }

    protected Func<HandSide> side;
    protected HandSide GetSide => side();
    protected Direction palm = Direction.Any;
    protected Direction thumb = Direction.Any;
    protected Direction point = Direction.Any;
    protected (Direction direction, float speed) moving;
    public Position position;
    protected bool still = false;
    protected bool? gripping = null;
    protected bool? triggering = null;
    protected List<(Direction direction, float amount)> offsets;

    /// <summary>
    /// Create a gesture on a particular side.
    /// </summary>
    public Gesture(HandSide side) { this.side = () => side; }
    
    /// <summary>
    /// Create a gesture on a side that is dynamically determined at runtime.
    /// </summary>
    /// <example>
    /// <code>
    /// var gesture = new Gesture(() => item.mainHandler.side);
    /// </code>
    /// </example>
    public Gesture(Func<HandSide> sideFunc) { side = sideFunc; }

    /// <summary>
    /// Take in a Side and switch it depending on Gesture.handedness.
    /// </summary>
    public static Side FixHandedness(Side side) => handedness == Side.Right
        ? side
        : side switch {
            Side.Left => Side.Right,
            Side.Right => Side.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
        };
        
    /// <summary>
    /// Take in a HandSide and switch it depending on Gesture.handedness.
    /// </summary>
    public static HandSide FixHandedness(HandSide side) => handedness == Side.Right
        ? side
        : side switch {
            HandSide.Left => HandSide.Right,
            HandSide.Right => HandSide.Left,
            HandSide.Both => HandSide.Both,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
        };

    /// <summary>
    /// A gesture on the left hand.
    /// </summary>
    public static Gesture Left => new(FixHandedness(HandSide.Left));
    
    /// <summary>
    /// A gesture on the right hand.
    /// </summary>
    public static Gesture Right => new(FixHandedness(HandSide.Right));
    
    /// <summary>
    /// A gesture on both hands.
    /// </summary>
    public static Gesture Both => new(HandSide.Both);
    
    /// <summary>
    /// A gesture on a particular side.
    /// </summary>
    public static Gesture OnSide(Side side)
        => new(FixHandedness(side == Side.Left ? HandSide.Left : HandSide.Right));

    /// <summary>
    /// Test whether the palm is facing a direction.
    /// </summary>
    public Gesture Palm(Direction direction) {
        palm = direction;
        return this;
    }

    /// <summary>
    /// Test whether the thumb is pointing in a direction.
    /// </summary>
    public Gesture Thumb(Direction direction) {
        thumb = direction;
        return this;
    }

    /// <summary>
    /// Test whether the hand is pointing in a direction.
    /// </summary>
    public Gesture Point(Direction direction) {
        point = direction;
        return this;
    }

    /// <summary>
    /// Test whether the hand is moving in a direction.
    /// </summary>
    public Gesture Moving(Direction direction, float velocity = 3) {
        moving = (direction, velocity);
        return this;
    }

    /// <summary>
    /// Test whether the hand is gripping.
    /// </summary>
    public Gesture Gripping {
        get {
            gripping = true;
            return this;
        }
    }
    
    /// <summary>
    /// Test whether the hand is triggering.
    /// </summary>
    public Gesture Triggering {
        get {
            triggering = true;
            return this;
        }
    }

    /// <summary>
    /// Test whether the hand is open (not gripping or triggering).
    /// </summary>
    public Gesture Open {
        get {
            gripping = false;
            triggering = false;
            return this;
        }
    }

    /// <summary>
    /// Test whether the hand is making a fist (gripping and triggering).
    /// </summary>
    public Gesture Fist {
        get {
            gripping = true;
            triggering = true;
            return this;
        }
    }

    /// <summary>
    /// Test whether the hand is still (not moving).
    /// </summary>
    public Gesture Still {
        get {
            still = true;
            return this;
        }
    }

    /// <summary>
    /// Test whether the hand is at a position relative to the player's body.
    /// </summary>
    public Gesture At(Position position) {
        this.position = position;
        return this;
    }

    /// <summary>
    /// If using .At(), offset the target position in a direction by an amount.
    /// </summary>
    public Gesture Offset(Direction direction, float amount = 0.2f) {
        offsets ??= new List<(Direction direction, float amount)>();
        offsets.Add((direction, amount));
        return this;
    }

    protected bool ForValidHands(Func<RagdollHand, bool> func) {
        return GetSide switch {
            HandSide.Both => func(Player.currentCreature.handLeft) && func(Player.currentCreature.handRight),
            HandSide.Left => func(Player.currentCreature.handLeft),
            HandSide.Right => func(Player.currentCreature.handRight),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    protected Vector3 CalculateOffset(Side side, PlayerRig rig) {
        if (offsets == null) return Vector3.zero;
        var amount = Vector3.zero;
        for (var i = 0; i < offsets.Count; i++) {
            amount += offsets[i].direction.ToVector(side, rig) * offsets[i].amount;
        }

        return amount;
    }

    protected Vector3 AnimateDirection(Side side, Direction direction, float animation, PlayerRig rig) {
        return (moving == default ? Vector3.zero : direction.ToVector(side, rig))
               * (animation * 0.2f);
    }

    protected (Vector3 position, float radius) GetPosition(Side side, PlayerRig rig, float animation = 0) => position switch {
        Position.Any or Position.Chest => (
            rig.Chest
            + new Vector3(side == Side.Right ? 0.23f : -0.23f, 0f, 0.5f).LocalToViewPlaneSpace()
            + AnimateDirection(side, moving.direction, animation, rig), 0.4f),
        Position.Waist => (
            rig.Waist
            + new Vector3(side == Side.Right ? 0.25f : -0.25f, 0.1f, 0.3f).LocalToViewPlaneSpace()
            + AnimateDirection(side, moving.direction, animation, rig), 0.4f),
        Position.Face => (
            rig.Head
            + new Vector3(side == Side.Right ? 0.1f : -0.1f, 0, 0.1f).LocalToViewPlaneSpace()
            + AnimateDirection(side, moving.direction, animation, rig), 0.2f),
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null)
    };

    protected Quaternion GetRotation(Side side, Vector3 position, PlayerRig rig) {
        
        // We will attempt to derive the forwards and up directions of the hand through educated guesses
        Vector3 forward = default, up = default;
        
        float sideMult = side == Side.Left ? -1 : 1;
        
        // Whether we have fixes on any of the directions
        bool hasPoint = point != Direction.Any;
        bool hasPalm = palm != Direction.Any;
        bool hasThumb = thumb != Direction.Any;
        
        // Try orienting by explicit or derived point direction
        if (hasPoint) {
            forward = point.ToVector(side, rig);
        } else if (hasPalm && hasThumb) {
            forward = Vector3.Cross(thumb.ToVector(side, rig), sideMult * palm.ToVector(side, rig));
        }

        // Try orienting by explicit or derived thumb direction
        if (hasThumb) {
            up = thumb.ToVector(side, rig);
        } else if (hasPalm && hasPoint) {
            up = Vector3.Cross(sideMult * palm.ToVector(side, rig), point.ToVector(side, rig));
        }

        // If we don't have a bi-axial fix and we know the palm direction
        if (palm != Direction.Any && forward == default || up == default) {
            var palmDir = palm.ToVector(side, rig);
            if (forward != default) {
                up = Vector3.Cross(forward, palmDir) * sideMult;
            } else if (up != default) {
                forward = Vector3.Cross(up, palmDir) * sideMult;
            } else {
                // If we literally only have palm, guess the rest based on what a hand would naturally do
                var viewForward = ViewDir.Forward.ToWorldViewPlaneVector();
                var viewUp = ViewDir.Up.ToWorldViewPlaneVector();
                (forward, up) = palm switch {
                    Direction.Up or Direction.Down => (viewForward,
                        Vector3.Cross(palmDir, viewForward) * sideMult),
                    Direction.Forward or Direction.Backward => (viewUp, Vector3.Cross(palmDir, viewUp) * sideMult),
                    _ => (viewForward, Vector3.Cross(palmDir, viewForward) * sideMult)
                };
            }
        }

        if (forward != default && up != default) {
            return Quaternion.LookRotation(forward, up);
        }

        if (forward != default) {
            return Quaternion.LookRotation(forward);
        }

        if (up != default) {
            return Quaternion.LookRotation(
                Vector3.Cross(up,
                    Vector3.Cross(position - rig.Waist, up)), up);
        }

        return Quaternion.LookRotation(position - rig.Waist);
    }

    /// <summary>
    /// Returns a GestureTarget that can show where the hand needs to be located.
    /// </summary>
    public GestureTarget? GetTarget(Side side, PlayerRig rig, float animation = 0) {
        if (ToSide(GetSide) == side || GetSide == HandSide.Both) {
            (var pos, float rad) = GetPosition(side, rig, animation);
            var offsetPosition = pos + CalculateOffset(side, rig);
            var rotation = GetRotation(side, offsetPosition, rig);
            return new GestureTarget(offsetPosition, rotation, gripping ?? false, triggering ?? false, rad);
        }

        return null;
    }

    /// <summary>
    /// Turn a HandSide into a Side. Returns null for HandSide.Both.
    /// </summary>
    public static Side? ToSide(HandSide side) => side switch {
        HandSide.Both => null,
        HandSide.Left => Side.Left,
        HandSide.Right => Side.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
    };

    /// <summary>
    /// Turn a Side into a HandSide.
    /// </summary>
    public static HandSide ToHandSide(Side side) => side switch {
        Side.Left => HandSide.Left,
        Side.Right => HandSide.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
    };

    protected bool IsAtPosition(Vector3 handPos, Side side, PlayerRig rig) {
        (var target, float radius) = GetPosition(side, rig);
        return (handPos - (CalculateOffset(side, rig) + target)).sqrMagnitude < radius * radius;
    }

    protected Func<bool> Tester => () => ForValidHands(hand
        => (position == Position.Any || IsAtPosition(hand.grip.position, hand.side, new PlayerRig()))
           && (!still || hand.LocalVelocity().magnitude < 0.5f)
           && (gripping == null || hand.Gripping() == gripping)
           && (triggering == null || hand.Triggering() == triggering)
           && (moving.direction == Direction.Any
               || CheckHandAgainst(hand, hand.LocalVelocity(), moving.direction, moving.speed))
           && (palm == Direction.Any || CheckHandAgainst(hand, hand.PalmDir, palm))
           && (thumb == Direction.Any || CheckHandAgainst(hand, hand.ThumbDir, thumb))
           && (point == Direction.Any || CheckHandAgainst(hand, hand.PointDir, point))
    );

    public bool CheckHandAgainst(RagdollHand hand, Vector3 source, Direction direction, float amount = 0) {
        return direction.ToViewDir(hand.side, new PlayerRig(), out var dir, out var fallback)
            ? source.WorldToViewPlaneSpace().InDirection(dir, amount)
            : source.IsFacing(fallback, 30) && source.magnitude >= amount;
    }

    /// <summary>
    /// Take a Side and Direction and return a ViewDir (player view-aligned direction)
    /// </summary>
    /// <summary>
    /// Get a human-readable description of the gesture.
    /// </summary>
    public string Description {
        get {
            return ", ".Join(GetSide switch {
                    HandSide.Both => "Both hands",
                    HandSide.Left => "Left hand",
                    HandSide.Right => "Right hand",
                    _ => throw new ArgumentOutOfRangeException()
                },
                position switch {
                    Position.Any => null,
                    Position.Face => "beside face",
                    Position.Waist => "beside waist",
                    Position.Chest => "near chest",
                    _ => throw new ArgumentOutOfRangeException()
                },
                offsets is { Count: > 0 }
                    ? $"offset [ {string.Join(", ", offsets.Select(offset => $"{offset.direction.ToString().ToLower()} by {offset.amount}m"))} ]"
                    : null,
                DirectionString("palm", palm),
                DirectionString("thumb", thumb),
                DirectionString("point", point),
                DirectionString("moving", moving.direction), still ? "still" : null);
        }
    }

    protected string DirectionString(string prefix, Direction direction) => direction switch {
        Direction.Any => null,
        _ => prefix + " " + direction.ToString().ToLower()
    };

    /// <summary>
    /// Test whether the gesture is being performed.
    /// </summary>
    public bool Test() => Tester();

    /// <summary>
    /// Test the gesture on a particular hand.
    /// </summary>
    public bool TestOn(Side side) => TestOn(ToHandSide(side));
        
    
    /// <summary>
    /// Test the gesture on a particular hand.
    /// </summary>
    public bool TestOn(HandSide side) {
        var oldSide = this.side;
        this.side = () => side;
        bool value = Test();
        this.side = oldSide;
        return value;
    }
    
    /// <summary>
    /// Test whether the gesture is not being performed.
    /// </summary>
    public bool Not() => !Test();

    public static implicit operator NamedCondition(Gesture gesture) => new NamedCondition(gesture.Description, gesture.Tester);
    public static implicit operator GestureStep(Gesture gesture) => new GestureStep(gesture);
        
    /// <summary>
    /// Add another gesture requirement to this gesture.
    /// </summary>
    public GestureStep And(Gesture gesture) => new GestureStep(this).And(gesture);
    
    /// <summary>
    /// Add another gesture requirement to this gesture.
    /// </summary>
    public GestureStep And(GestureStep step) => new GestureStep(this).And(step);

    public static Gesture FromString(string step) {
        string[] constraints = step.Split(',');
        Gesture gesture;
        try {
            gesture = constraints[0] switch {
                "Left" => Left,
                "Right" => Right,
                "Both" => Both,
                _ => throw new ArgumentOutOfRangeException()
            };
        } catch (ArgumentOutOfRangeException) {
            return null;
        }
        for (var i = 1; i < constraints.Length; i++) {
            switch (constraints[i]) {
                case "Gripping":
                    gesture = gesture.Gripping;
                    continue;
                case "Triggering":
                    gesture = gesture.Triggering;
                    continue;
                case "Fist":
                    gesture = gesture.Fist;
                    continue;
                case "Open":
                    gesture = gesture.Open;
                    continue;
                case "Still":
                    gesture = gesture.Still;
                    continue;
            }

            string[] parts = constraints[i].Split(':');
            if (parts.Length > 1)
                switch (parts[0]) {
                    case "At" when Enum.TryParse(parts[1], out Position position):
                        gesture = gesture.At(position);
                        continue;
                    case "Moving" when Enum.TryParse(parts[1], out Direction direction):
                        gesture = gesture.Moving(direction);
                        continue;
                    case "Offset" when Enum.TryParse(parts[1], out Direction direction):
                        gesture = gesture.Offset(direction);
                        continue;
                    case "Offset" when parts.Length == 3 && Enum.TryParse(parts[1], out Direction direction) && float.TryParse(parts[2], out float amount):
                        gesture = gesture.Offset(direction, amount);
                        continue;
                    case "Palm" when Enum.TryParse(parts[1], out Direction direction):
                        gesture = gesture.Palm(direction);
                        continue;
                    case "Point" when Enum.TryParse(parts[1], out Direction direction):
                        gesture = gesture.Point(direction);
                        continue;
                    case "Thumb" when Enum.TryParse(parts[1], out Direction direction):
                        gesture = gesture.Thumb(direction);
                        continue;
                }
        }
        return gesture;
    }
}

/// <summary>
/// A step in a sequence of gestures. Can contain one or multiple gestures.
/// </summary>
/// <remarks>
/// You should not need to use this, the engine will convert between Gesture and GestureStep seamlessly.
/// </remarks>
public class GestureStep {
    public List<Gesture> gestures;
    public GestureStep(params Gesture[] gestures) {
        this.gestures = gestures.ToList();
    }

    public static GestureStep FromString(string data) {
        if (string.IsNullOrWhiteSpace(data)) return null;
        data = data.Replace(" ", "");
        string[] steps = data.Split(';');
        var gestures = new Gesture[steps.Length];
        for (var i = 0; i < steps.Length; i++) {
            gestures[i] = Gesture.FromString(steps[i]);
        }

        return new GestureStep(gestures);
    }

    public string Description => ", ".Join(gestures.Select(gesture => gesture?.Description));

    /// <summary>
    /// Get a human-readable description of the gesture.
    /// </summary>
    public static string ToSentence(string input) {
        if (input.Length < 1)
            return input;

        string sentence = input.ToLower();
        return sentence[0].ToString().ToUpper() + sentence.Substring(1);
    }

    /// <summary>
    /// Convert to a type understandable by the Sequence Tracker.
    /// </summary>
    public static implicit operator NamedCondition(GestureStep step) {
        return new NamedCondition(
            ToSentence(string.Join(" and ", step.gestures.Select(gesture => gesture?.Description.ToLower()))),
            () => step.Tester());
    }

    /// <summary>
    /// Get the GestureTarget for a hand.
    /// </summary>
    public GestureTarget? GetTargetForHand(Side side, PlayerRig rig, float animation = 0) {
        GestureTarget? target = null;
        for (var i = 0; i < gestures.Count; i++) {
            if (gestures[i]?.GetTarget(side, rig, animation) is GestureTarget eachTarget) {
                target = eachTarget;
            }
        }

        return target;
    }

    private static readonly int Flex = Animator.StringToHash("Flex");

    /// <summary>
    /// Orient two GameObjects to the positions of where the hands should be.
    /// </summary>
    /// <remarks>
    /// Deactivates the object if the hand is not used in the gesture.
    /// </remarks>
    public void UpdateTargets(PlayerRig rig, float animation = 0, Gesture.HandSide? forceGripping = null) {
        if (GetTargetForHand(Side.Left, rig, animation) is GestureTarget left) {
            rig.left.GetComponentInChildren<Animator>()?.SetFloat(Flex,
                Mathf.Lerp(rig.left.GetComponentInChildren<Animator>().GetFloat(Flex),
                    left.Gripping || forceGripping is Gesture.HandSide.Both or Gesture.HandSide.Left ? 1 : 0,
                    rig.left.activeSelf ? Time.deltaTime * 10 : 1));
            if (!rig.left.activeSelf) {
                rig.left.SetActive(true);
                rig.left.transform.SetPositionAndRotation(left.Position, left.Rotation);
            } else {
                rig.left.transform.SetPositionAndRotation(
                    Vector3.Lerp(rig.left.transform.position, left.Position, Time.deltaTime * 10),
                    Quaternion.Slerp(rig.left.transform.rotation, left.Rotation, Time.deltaTime * 10));
            }
        } else {
            rig.left.SetActive(false);
        }

        if (GetTargetForHand(Side.Right, rig, animation) is GestureTarget right) {
            rig.right.GetComponentInChildren<Animator>()?.SetFloat(Flex,
                Mathf.Lerp(rig.right.GetComponentInChildren<Animator>().GetFloat(Flex),
                    right.Gripping || forceGripping is Gesture.HandSide.Both or Gesture.HandSide.Right ? 1 : 0,
                    rig.right.activeSelf ? Time.deltaTime * 10 : 1));
            if (!rig.right.activeSelf) {
                rig.right.SetActive(true);
                rig.right.transform.SetPositionAndRotation(right.Position, right.Rotation);
            } else {
                rig.right.transform.SetPositionAndRotation(
                    Vector3.Lerp(rig.right.transform.position, right.Position, Time.deltaTime * 10),
                    Quaternion.Slerp(rig.right.transform.rotation, right.Rotation, Time.deltaTime * 10));
            }
        } else {
            rig.right.SetActive(false);
        }
    }

    /// <summary>
    /// Add another gesture requirement to this gesture.
    /// </summary>
    public GestureStep And(Gesture gesture) {
        gestures.Add(gesture);
        return this;
    }

    /// <summary>
    /// Add another gesture requirement to this gesture.
    /// </summary>
    public GestureStep And(GestureStep step) {
        gestures.AddRange(step.gestures);
        return this;
    }

    /// <summary>
    /// Test all the gestures in this step.
    /// </summary>
    public Func<bool> Tester => () => gestures.All(gesture => gesture?.Test() ?? true);
        
    /// <summary>
    /// Return a SequenceTracker step testing the inverse of this gesture.
    /// </summary>
    public NamedCondition Not() {
        return new NamedCondition(
            ToSentence("NOT " + string.Join(" and ", gestures.Select(gesture => gesture?.Description.ToLower()))),
            () => !Tester());
    }
}

/// <summary>
/// A player-aligned direction. Inwards is to the right for the left hand and to the left from the right,
/// and vice versa for outwards.
/// </summary>
public enum Direction {
    Any,
    Forward,
    Backward,
    Inwards,
    Outwards,
    Up,
    Down,
    Together,
    Apart
}

public enum Position {
    Any,
    Waist,
    Face,
    Chest
}
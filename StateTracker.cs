// lyneca

using System;
using System.Collections.Generic;
using System.Linq;
using ThunderRoad.AI.Decorator;
using Unity.Profiling;
using UnityEngine;

public class StateTracker {
    public enum TimeMode {
        Cooldown,
        Tap,
        Hold
    }
    public class TrackedState {
        public Func<bool> state;
        public Action action;

        public readonly bool trigger;
        public readonly TimeMode mode;
        public readonly float duration;

        private bool lastState;
        private float lastTrigger;

        public TrackedState(Func<bool> state, Action action, bool trigger, float duration, TimeMode mode = TimeMode.Cooldown) {
            this.state = state;
            this.action = action;
            this.trigger = trigger;
            this.duration = duration;
            this.mode = mode;
            lastTrigger = 0;
            lastState = state();
        }

        public void Evaluate() {
            bool newState = state();
            if ((duration == 0
                 || (mode == TimeMode.Tap && (Time.time - lastTrigger) < duration)
                 || ((mode == TimeMode.Cooldown || mode == TimeMode.Hold) && (Time.time - lastTrigger) > duration))
                && lastState != newState
                && newState == trigger) {
                lastTrigger = Time.time;
                action();
            }

            if (mode != TimeMode.Cooldown && lastState != newState && newState != trigger) lastTrigger = Time.time;
            lastState = newState;
        }
    }

    public class ChildState {
        public Func<bool> condition;
        public StateTracker child;

        public ChildState(Func<bool> condition, StateTracker child) {
            this.condition = condition;
            this.child = child;
        }
    }

    private StateTracker parent;
    private readonly List<TrackedState> states;
    private readonly List<ChildState> children;
    private readonly List<ITrackedValue> values;
    private readonly List<Action> actions;

    public StateTracker() {
        states = new List<TrackedState>();
        children = new List<ChildState>();
        values = new List<ITrackedValue>();
        actions = new List<Action>();
    }

    public StateTracker(StateTracker parent) : this() {
        this.parent = parent;
    }

    public interface ITrackedValue {
        void Update();
    }

    public class TrackedValue<T> : ITrackedValue {
        public static implicit operator T(TrackedValue<T> instance) => instance.value;
        public T value;

        // variable definition, call this to get the current value
        public Func<T> definition;

        public TrackedValue(Func<T> definition) {
            this.definition = definition;
            value = definition();
        }

        // update value
        public void Update() { value = definition(); }
    }

    public TrackedValue<T> Value<T>(Func<T> definition) {
        var value = new TrackedValue<T>(definition);
        values.Add(value);
        return value;
    }

    public StateTracker If(Func<bool> condition) {
        var child = new StateTracker(this);
        children.Add(new ChildState(condition, child));
        return child;
    }

    public StateTracker If(TrackedValue<bool> condition) => If(() => condition);
    public StateTracker And => parent;
    public StateTracker On(Func<bool> state, Action action, float cooldown = 0) => OnTrue(state, action, cooldown);
    public StateTracker On(Func<bool?> state, Action action, float cooldown = 0) => OnTrue(() => state() == true, action, cooldown);
    public StateTracker Do(Action action) {
        actions.Add(action);
        return this;
    }

    public StateTracker While(Func<bool> condition, Action actionTrue, Action actionFalse = null) {
        If(condition).Do(actionTrue);
        if (actionFalse != null)
            If(() => !condition()).Do(actionFalse);
        return this;
    }

    public StateTracker OnTap(Func<bool> state, Action action, float holdDuration = 0.5f) {
        var trackedState = new TrackedState(state, action, false, holdDuration, TimeMode.Tap);
        states.Add(trackedState);
        return this;
    }

    public StateTracker OnTap(TrackedValue<bool> state, Action action, float holdDuration = 0.5f)
        => OnTap(() => state, action, holdDuration);
    public StateTracker OnHold(Func<bool> state, Action action, float holdDuration = 0.5f) {
        var trackedState = new TrackedState(state, action, false, holdDuration, TimeMode.Hold);
        states.Add(trackedState);
        return this;
    }
    public StateTracker OnHold(TrackedValue<bool> state, Action action, float holdDuration = 0.5f)
        => OnHold(() => state, action, holdDuration);
    public StateTracker On(TrackedValue<bool> state, Action action, float cooldown = 0)
        => OnTrue(() => state, action, cooldown);

    public StateTracker Toggle(Func<bool> state, Action actionTrue, Action actionFalse, float cooldown = 0)
        => OnTrue(state, actionTrue, cooldown).OnFalse(state, actionFalse, cooldown);

    public StateTracker Toggle(TrackedValue<bool> state, Action actionTrue, Action actionFalse, float cooldown = 0)
        => Toggle(() => state, actionTrue, actionFalse, cooldown);

    public StateTracker Else(Action action) {
        if (states.Count > 0) {
            var lastState = states.LastOrDefault();
            return lastState.trigger
                ? OnFalse(lastState.state, action)
                : OnTrue(lastState.state, action);
        }

        return this;
    }

    public StateTracker OnTrue(TrackedValue<bool> state, Action action, float cooldown = 0)
        => OnTrue(() => state, action, cooldown);

    public StateTracker OnFalse(TrackedValue<bool> state, Action action, float cooldown = 0)
        => OnFalse(() => state, action, cooldown);

    public StateTracker OnTrue(Func<bool> state, Action action, float cooldown = 0) {
        var trackedState = new TrackedState(state, action, true, cooldown);
        states.Add(trackedState);
        return this;
    }

    public StateTracker OnFalse(Func<bool> state, Action action, float cooldown = 0) {
        var trackedState = new TrackedState(state, action, false, cooldown);
        states.Add(trackedState);
        return this;
    }

    public void Remove(TrackedState state) => states.Remove(state);

    public void Update() {
        foreach (var value in values) {
            value.Update();
        }

        foreach (var child in children) {
            if (child.condition())
                child.child.Update();
        }

        foreach (var state in states) {
            state.Evaluate();
        }

        foreach (var action in actions) {
            action();
        }
    }
}

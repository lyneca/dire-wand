using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SequenceTracker;

using NamedConditionSet = Tuple<string, Func<bool>[]>;
using NamedCondition = Tuple<string, Func<bool>>;

/// <summary>
/// A step in a sequence of tests. The sequence is represented as an upside-down tree,
/// where each step can branch off into different steps and actions.
/// </summary>
/// <remarks>
/// You create a sequence by method chaining. Make sure you call root.Update() every frame for the sequence to run!
/// </remarks>
/// <example>
/// <code>
/// root = Step.Start()
///     .Then(() => conditionA)
///     .Then(() => conditionB)
///     .Do(() => actionA());
/// </code>
/// </example>
public class Step {
    protected Step skipTo;
    protected Step parent;
    private Condition condition;
    List<Step> children;
    private Action action;
    private Action onStateChange;
    private string actionName;
    private string name;
    public string label;
    private float lastChangedToTime;
    private float delay;

    /// <summary>
    /// Create the root node of a sequence.
    /// </summary>
    /// <param name="onStateChange">Run this function whenever a step is completed.</param>
    /// <returns>A root Step</returns>
    public static Step Start(Action onStateChange = null) {
        var step = new Step(new Condition(() => true), null, onStateChange) { name = "Start" };
        return step;
    }

    protected class Condition {
        protected Func<bool> start;
        protected Func<bool> end;
        protected float duration;
        protected bool hasStarted;
        protected float startTime;
        public bool runOnChange;
        public DurationMode mode;

        public Condition(
            Func<bool> start,
            Func<bool> end = null,
            float duration = 0,
            DurationMode mode = DurationMode.After,
            bool runOnChange = true) {
            this.start = start;
            this.end = end;
            this.duration = duration;
            this.mode = mode;
            this.runOnChange = runOnChange;
        }

        public Condition And(Func<bool> test) {
            var old = start.Clone() as Func<bool>;
            start = () => (old?.Invoke() ?? true) && test();
            return this;
        }


        public bool Evaluate() {
            if (!hasStarted && start()) {
                hasStarted = true;
                startTime = Time.time;
            }

            // Has not hit leading edge
            if (!hasStarted) return false;

            // No duration, return end condition
            if (duration == 0) return end?.Invoke() ?? true;

            float sinceStart = Time.time - startTime;

            bool hasEnded = end?.Invoke() ?? true;

            // Time limit exceeded, reset
            if (mode == DurationMode.Before && sinceStart > duration && hasEnded) {
                hasStarted = false;
                return false;
            }

            // Check whether time within duration
            bool durationCheck = mode == DurationMode.After
                ? sinceStart >= duration
                : sinceStart <= duration;

            return durationCheck && hasEnded;
        }

        public void Reset() { hasStarted = false; }
    }

    protected Step(Condition condition, Step parent = null, Action onStateChange = null) {
        this.parent = parent;
        this.condition = condition;
        this.onStateChange = onStateChange ?? parent?.onStateChange;
        children = new List<Step>();
    }

    /// <summary>
    /// Add a step in the sequence.
    /// </summary>
    /// <param name="startCondition">The condition to test.</param>
    /// <param name="name">A human-readable description of the step.</param>
    /// <param name="duration">Optional - how long the step should be true.</param>
    /// <param name="endCondition">Optional - a condition for the end of the step.</param>
    /// <param name="mode">Optional - whether to check if the condition occurs before or after the duration has passed</param>
    /// <param name="runOnChange">Optional - set to false to skip the onChange function defined in Step.Start().</param>
    public Step Then(
        Func<bool> startCondition,
        string name = "",
        float duration = 0,
        Func<bool> endCondition = null,
        DurationMode mode = DurationMode.After,
        bool runOnChange = true,
        bool toggle = false) {
        var trigger = new Condition(startCondition,
            toggle ? () => !startCondition() : endCondition,
            duration,
            mode,
            runOnChange);

        var step = new Step(trigger, this) {
            name = name
        };
        children.Add(step);
        return step;
    }

    /// <summary>
    /// Add an extra condition to the step.
    /// </summary>
    /// <param name="name">Name of the step</param>
    /// <param name="newCondition">Condition to add</param>
    public Step And(string name, Func<bool> newCondition) {
        condition.And(newCondition);
        this.name += $". {name}";
        return this;
    }

    /// <summary>
    /// Add a set of extra conditions to the step.
    /// </summary>
    /// <param name="conditions">
    /// One or more NamedConditions, a tuple of string name and Func&lt;bool&gt; tester function
    /// </param>
    public Step And(params NamedCondition[] conditions) {
        for (var i = 0; i < conditions.Length; i++) {
            var newCondition = conditions[i];
            condition.And(newCondition.Item2);
            name += $". {newCondition.Item1}";
        }

        return this;
    }

    /// <summary>
    /// Add one or more child steps.
    /// </summary>
    /// <param name="conditions">
    /// One or more NamedConditions, a tuple of string name and Func&lt;bool&gt; tester function
    /// </param>
    public Step Then(params NamedCondition[] conditions) {
        if (conditions.Length == 0) return this;
        var next = Then(conditions[0].Item2, conditions[0].Item1);
        return conditions.Length > 1 ? next.Then(conditions.Skip(1).ToArray()) : next;
    }

    /// <summary>
    /// Add one or more sets of child steps.
    /// </summary>
    /// <param name="conditions">
    /// One or more NamedConditionSets, a list of NamedConditions.
    /// </param>
    /// <returns></returns>
    public Step Then(params NamedConditionSet[] conditions) {
        if (conditions.Length == 0) return this;
        var next = Then(conditions[0].Item1, conditions[0].Item2);
        return conditions.Length > 1 ? next.Then(conditions.Skip(1).ToArray()) : next;
    }

    /// <summary>
    /// Add a sequence of child steps under one name.
    /// </summary>
    /// <param name="name">Human-readable name of the step</param>
    /// <param name="conditions">Set of conditions</param>
    public Step Then(string name = "", params Func<bool>[] conditions) {
        var list = new Queue<Func<bool>>(conditions);
        var next = Then(list.Dequeue(), name);

        while (list.Any()) {
            next = next.Then(list.Dequeue());
        }

        return next;
    }

    /// <summary>
    /// Add an action for the sequence to run when it gets to this step.
    /// </summary>
    /// <param name="action">Function to run</param>
    /// <param name="actionName">Human-readable name of the action</param>
    public Step Do(Action action, string actionName = "") {
        this.action = action;
        this.actionName = actionName;
        return this;
    }

    /// <summary>
    /// Add a delay step
    /// </summary>
    /// <param name="delay">Time to wait</param>
    public Step After(float delay) {
        var step = Then(() => true, $"Wait {delay:0.##}s", runOnChange: true);
        step.delay = delay;

        return step;
    }

    /// <summary>
    /// Reset the sequence to the root step.
    /// </summary>
    public void Reset() {
        skipTo = null;
        condition.Reset();
        for (var index = 0; index < children.Count; index++) {
            var child = children[index];
            child.Reset();
        }
    }

    protected bool Check() { return condition.Evaluate(); }

    protected void DoUpdate() {
        if (skipTo == null && (parent == null || Time.time - parent.lastChangedToTime > delay)) {
            for (var index = 0; index < children.Count; index++) {
                var child = children[index];
                if (child.Check()) {
                    child.action?.Invoke();
                    if (child.condition.runOnChange)
                        onStateChange?.Invoke();
                    skipTo = child;
                    lastChangedToTime = Time.time;
                    break;
                }
            }
        }

        if (skipTo != null) {
            skipTo.DoUpdate();
        }
    }

    /// <summary>
    /// Update the entire sequence, testing the children of the current step.
    /// </summary>
    public void Update() {
        if (parent == null) DoUpdate();
        else parent.Update();
    }

    /// <summary>
    /// Show the current path of the sequence.
    /// </summary>
    public string GetCurrentPath() {
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(actionName)) {
            return skipTo != null ? skipTo.GetCurrentPath() : "";
        }

        if (string.IsNullOrEmpty(name)) {
            return $"[{actionName}]" + (skipTo != null ? " > " + skipTo.GetCurrentPath() : "");
        }

        if (string.IsNullOrEmpty(actionName)) {
            return name + (skipTo != null ? " > " + skipTo.GetCurrentPath() : "");
        }

        return "";
    }

    /// <summary>
    /// Get the current active step.
    /// </summary>
    /// <returns></returns>
    public Step GetCurrent() { return skipTo?.GetCurrent() ?? this; }

    /// <summary>
    /// Returns true if there are no children for the current step to pass to.
    /// </summary>
    /// <remarks>
    /// If this is true, you might want to call root.Reset() when you would like to restart the sequence from the top.
    /// </remarks>
    /// <returns></returns>
    public bool AtEnd() { return skipTo?.AtEnd() ?? children.Count == 0; }

    /// <summary>
    /// Return a representation of the sequence tree.
    /// </summary>
    /// <param name="indent">Internal recursive use</param>
    public string DisplayTree(int indent = 0) {
        var output = "";
        bool increaseIndent = true;
        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(actionName)) {
            output += new string(' ', indent * 2)
                      + $"- {(string.IsNullOrEmpty(label) ? "" : label + ": ")}{name}\n"
                      + new String(' ', (indent + 1) * 2)
                      + $"- Action: {actionName}\n";
        } else if (!string.IsNullOrEmpty(name)) {
            output += new string(' ', indent * 2) + $"- {(string.IsNullOrEmpty(label) ? "" : label + ": ")}{name}\n";
        } else if (!string.IsNullOrEmpty(actionName)) {
            output += new string(' ', indent * 2) + $"- Action: {actionName}\n";
            increaseIndent = false;
        } else {
            increaseIndent = false;
        }

        foreach (var child in children) {
            string childTree = child.DisplayTree(indent + (increaseIndent ? 1 : 0));
            if (!string.IsNullOrWhiteSpace(childTree))
                output += childTree;
        }

        return output;
    }
}

public enum DurationMode {
    Before,
    After
}
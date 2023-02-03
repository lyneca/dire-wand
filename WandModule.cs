using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using SequenceTracker;

namespace Wand {
    public enum SpellType {
        Button,
        Trigger
    }

    public class WandModule {
        public WandBehaviour wand;
        public Item item;
        public string iconAddress;
        public List<string> videoAddresses;
        public string title;
        public string description;
        public SpellType type;
        public Color color;

        public virtual WandModule Clone() {
            return MemberwiseClone() as WandModule;
        }

        public void Begin(WandBehaviour wand) {
            this.wand = wand;
            item = wand.item;
            videoAddresses ??= new List<string>();
            if (color == default) {
                switch (type) {
                    case SpellType.Button:
                        color = Utils.HexColor(191, 119, 30, 3);
                        break;
                    case SpellType.Trigger:
                        color = Utils.HexColor(40, 30, 191, 3);
                        break;
                }
            }
        }

        public virtual void OnInit() {}
        public virtual void OnUpdate() {}
        public virtual void OnReset() {}
    }

}

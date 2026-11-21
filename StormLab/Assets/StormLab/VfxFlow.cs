using System;
using System.Collections.Generic;
using UnityEngine;

namespace StormLab
{
    [CreateAssetMenu(menuName = "Storm Lab/VFX Flow")]
    public sealed class VfxFlow : ScriptableObject
    {
        public enum Stage { Charge, Release, Impact }
        public enum Effect { Ring, Arc, Particles, Bolt, Hit }

        [Serializable]
        public sealed class Layer
        {
            public Effect effect;
            [Min(0)] public float delay;
            [Min(0)] public float duration;
        }

        [Serializable]
        public sealed class Node
        {
            public string id;
            public string next;
            public Stage stage;
            public Vector2 position;
            [Min(0.05f)] public float duration = 1f;
            public List<Layer> layers = new();

            public Node() { }

            public Node(Stage stage, Vector2 position, float duration, params Effect[] effects)
            {
                id = Guid.NewGuid().ToString("N");
                this.stage = stage;
                this.position = position;
                this.duration = duration;
                foreach (var effect in effects) layers.Add(new Layer { effect = effect });
            }
        }

        [HideInInspector] public string entry;
        [HideInInspector] public List<Node> nodes = new();

        public Node Find(string id) => string.IsNullOrEmpty(id) ? null : nodes.Find(node => node.id == id);

        public bool TryConnect(Node source, Node destination)
        {
            if (source == null || destination == null || !nodes.Contains(source) || !nodes.Contains(destination))
                return false;

            var visited = new HashSet<string>();
            for (var node = destination; node != null; node = Find(node.next))
                if (node == source || !visited.Add(node.id)) return false;

            source.next = destination.id;
            return true;
        }

        public void Remove(Node node)
        {
            nodes.Remove(node);
            foreach (var other in nodes)
                if (other.next == node.id) other.next = null;
            if (entry == node.id) entry = nodes.Count == 0 ? null : nodes[0].id;
        }

        public bool TryGetPath(List<Node> path, out string error)
        {
            path.Clear();
            var node = Find(entry);
            if (node == null)
            {
                error = "Set an entry stage.";
                return false;
            }

            var visited = new HashSet<string>();
            while (node != null)
            {
                if (!visited.Add(node.id))
                {
                    error = "The flow contains a cycle.";
                    path.Clear();
                    return false;
                }
                path.Add(node);
                if (string.IsNullOrEmpty(node.next)) break;
                node = Find(node.next);
                if (node == null)
                {
                    error = "A connection points to a missing stage.";
                    path.Clear();
                    return false;
                }
            }

            error = null;
            return true;
        }

        public void Seed(float charge = 1.45f, float release = 0.28f, float impact = 0.72f)
        {
            var first = new Node(Stage.Charge, new Vector2(54, 88), charge, Effect.Ring, Effect.Arc, Effect.Particles);
            var middle = new Node(Stage.Release, new Vector2(330, 88), release, Effect.Bolt);
            var last = new Node(Stage.Impact, new Vector2(606, 88), impact, Effect.Hit, Effect.Ring);
            nodes.AddRange(new[] { first, middle, last });
            entry = first.id;
            first.next = middle.id;
            last.layers[1].duration = 0.39f;
            middle.next = last.id;
        }
    }
}


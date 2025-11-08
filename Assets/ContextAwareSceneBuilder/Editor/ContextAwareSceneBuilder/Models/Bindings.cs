using System;
using System.Collections.Generic;

namespace ContextAwareSceneBuilder.Editor
{
    /// <summary>
    /// Placement relationships computed during object creation.
    /// Records contact, adjacency, and room associations.
    /// </summary>
    [Serializable]
    public class Bindings
    {
        /// <summary>
        /// Primary physical contact defining alignment.
        /// </summary>
        public ContactBinding contact;

        /// <summary>
        /// Lateral relationships from collision offset resolution.
        /// Empty list if no adjacency constraints.
        /// </summary>
        public List<AdjacentBinding> adjacent;

        /// <summary>
        /// Room label copied from contacted surface's roomBindings.
        /// Can be room name, "outside", or "blocked".
        /// </summary>
        public string room;
    }

    /// <summary>
    /// Primary contact binding between object and target surface.
    /// </summary>
    [Serializable]
    public class ContactBinding
    {
        /// <summary>
        /// Which semantic point was used for alignment.
        /// One of: bottom, top, left, right, front, back
        /// </summary>
        public string side;

        /// <summary>
        /// Target query string to resolve instance.
        /// Examples: "id:173", "name:Wall_01", "room:Kitchen type:Wall"
        /// </summary>
        public string target;

        /// <summary>
        /// Which face of target to align to (optional, auto-resolved from roomBindings).
        /// One of: bottom, top, left, right, front, back
        /// </summary>
        public string targetSide;
    }

    /// <summary>
    /// Lateral adjacency relationship (side-by-side placement).
    /// </summary>
    [Serializable]
    public class AdjacentBinding
    {
        /// <summary>
        /// Which side of this object faces the adjacent object.
        /// </summary>
        public string mySide;

        /// <summary>
        /// Target instance identifier (name or ID).
        /// </summary>
        public string target;

        /// <summary>
        /// Which side of target faces this object.
        /// </summary>
        public string theirSide;

        /// <summary>
        /// Distance between objects in meters. Default: 0.05m
        /// </summary>
        public float gap = 0.05f;
    }
}

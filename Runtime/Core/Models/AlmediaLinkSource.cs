using System;

namespace AlmediaSDK
{
    /// <summary>
    /// Where a link popup open came from. <see cref="Almedia.ShowLink(AlmediaLinkSource)"/> takes it and
    /// the popup's events carry it. <see cref="Name"/> is the game's label for the place. <see cref="Kind"/>
    /// is set by the SDK. <see cref="Payload"/> is any object the game attaches; the SDK never reads,
    /// logs or sends it.
    /// </summary>
    public sealed class AlmediaLinkSource : IEquatable<AlmediaLinkSource>
    {
        /// <summary>The source of <see cref="Almedia.ShowLink()"/> called without an argument. Name <c>show_link</c>.</summary>
        public static readonly AlmediaLinkSource ShowLink = new AlmediaLinkSource("show_link", null, AlmediaLinkSourceKind.ShowLink);

        /// <summary>A source of kind <see cref="AlmediaLinkSourceKind.ShowLink"/>. A null or empty name reads as <c>custom</c>.</summary>
        public AlmediaLinkSource(string name) : this(name, null, AlmediaLinkSourceKind.ShowLink) { }

        /// <summary>
        /// A source of kind <see cref="AlmediaLinkSourceKind.ShowLink"/> with a payload the popup and
        /// the events hand back as given. A null or empty name reads as <c>custom</c>.
        /// </summary>
        public AlmediaLinkSource(string name, object payload) : this(name, payload, AlmediaLinkSourceKind.ShowLink) { }

        internal AlmediaLinkSource(string name, object payload, AlmediaLinkSourceKind kind)
        {
            Name = string.IsNullOrEmpty(name) ? "custom" : name;
            Payload = payload;
            Kind = kind ?? AlmediaLinkSourceKind.ShowLink;
        }

        /// <summary>The label as given. Never null or empty.</summary>
        public string Name { get; }

        /// <summary>What opened the popup. Never null.</summary>
        public AlmediaLinkSourceKind Kind { get; }

        /// <summary>The object the game attached, or null.</summary>
        public object Payload { get; }

        /// <summary>Equal when <see cref="Kind"/> and <see cref="Name"/> are equal. The payload takes no part.</summary>
        public bool Equals(AlmediaLinkSource other)
            => !(other is null) && Kind == other.Kind && string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as AlmediaLinkSource);

        public override int GetHashCode() => ValueEquality.Hash(Kind, Name);

        public static bool operator ==(AlmediaLinkSource a, AlmediaLinkSource b) => a is null ? b is null : a.Equals(b);

        public static bool operator !=(AlmediaLinkSource a, AlmediaLinkSource b) => !(a == b);

        public override string ToString() => Name;
    }
}

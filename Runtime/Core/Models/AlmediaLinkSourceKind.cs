using System;

namespace AlmediaSDK
{
    /// <summary>
    /// What opened the link popup. <see cref="AlmediaLinkSource.Kind"/> carries it. The SDK sets it:
    /// a source the game creates always reads <see cref="ShowLink"/>. Only the static members below
    /// exist. Compare them with <c>==</c>. <c>ToString()</c> returns the member name.
    /// </summary>
    public sealed class AlmediaLinkSourceKind : IEquatable<AlmediaLinkSourceKind>
    {
        /// <summary>The game called <see cref="Almedia.ShowLink()"/> or <see cref="Almedia.ShowLink(AlmediaLinkSource)"/>.</summary>
        public static readonly AlmediaLinkSourceKind ShowLink = new AlmediaLinkSourceKind("show_link", nameof(ShowLink));

        /// <summary>The player tapped a LinkButton prefab.</summary>
        public static readonly AlmediaLinkSourceKind LinkButton = new AlmediaLinkSourceKind("link_button", nameof(LinkButton));

        private readonly string _wireName;
        private readonly string _name;

        private AlmediaLinkSourceKind(string wireName, string name)
        {
            _wireName = wireName;
            _name = name;
        }

        internal string WireName => _wireName;

        public bool Equals(AlmediaLinkSourceKind other)
            => !(other is null) && string.Equals(_wireName, other._wireName, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as AlmediaLinkSourceKind);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_wireName);

        public static bool operator ==(AlmediaLinkSourceKind a, AlmediaLinkSourceKind b) => a is null ? b is null : a.Equals(b);

        public static bool operator !=(AlmediaLinkSourceKind a, AlmediaLinkSourceKind b) => !(a == b);

        public override string ToString() => _name;
    }
}

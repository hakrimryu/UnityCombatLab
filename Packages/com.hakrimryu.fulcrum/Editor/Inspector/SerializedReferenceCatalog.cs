using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Fulcrum.Editor.Inspector
{
    /// <summary>
    /// Discovers the concrete types that can fill a <c>[SerializeReference]</c> slot.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="TypeCache"/> rather than a hand-maintained list, so a model a
    /// user writes in their own project appears in the dropdown without touching this
    /// package. That is what makes the extension points in the README real rather than
    /// aspirational.
    ///
    /// Types without a public parameterless constructor are excluded: the dropdown has to
    /// be able to create one, and a type it cannot construct would be an entry that fails
    /// when picked.
    /// </remarks>
    internal static class SerializedReferenceCatalog
    {
        /// <summary>Concrete, constructible subclasses of <typeparamref name="T"/>, name-sorted.</summary>
        public static IReadOnlyList<Type> Implementations<T>() => Cache<T>.Types;

        /// <summary>
        /// The label shown for a type: its <see cref="FulcrumModelAttribute"/> name when it
        /// has one, otherwise the class name with its suffix trimmed.
        /// </summary>
        /// <remarks>
        /// A dropdown reading <c>Power</c> and <c>Exponential</c> assumes the reader already
        /// knows what those do. The attribute lets a type say "Gets steeper" instead, and a
        /// model written elsewhere describes itself the same way.
        /// </remarks>
        public static string DisplayName(Type type, string suffixToTrim)
        {
            if (type == null) return "(none)";

            var described = Describe(type);
            if (described != null) return described.DisplayName;

            var name = type.Name;
            if (!string.IsNullOrEmpty(suffixToTrim)
                && name.Length > suffixToTrim.Length
                && name.EndsWith(suffixToTrim, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffixToTrim.Length);
            }

            return Humanize(name);
        }

        /// <summary>Splits <c>MinDelta</c> into <c>Min Delta</c>.</summary>
        private static string Humanize(string name)
        {
            var result = new System.Text.StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])) result.Append(' ');
                result.Append(c);
            }

            return result.ToString();
        }

        /// <summary>The description a type carries, or null.</summary>
        public static FulcrumModelAttribute Describe(Type type)
            => type == null
                ? null
                : (FulcrumModelAttribute)Attribute.GetCustomAttribute(type, typeof(FulcrumModelAttribute));

        /// <summary>One-line summary for a type, or an empty string.</summary>
        public static string Summary(Type type) => Describe(type)?.Summary ?? string.Empty;

        private static class Cache<T>
        {
            public static readonly Type[] Types = TypeCache.GetTypesDerivedFrom<T>()
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition)
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
